using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buffer;

namespace Tabela
{
    // Representa uma tabela, que é uma coleção de páginas
    public class Tabela
    {
        public List<Pagina.Pagina> Pags { get; set; }
        public int QtdPags => Pags.Count;
        public int QtdCols { get; private set; }
        public string NomeArquivo { get; set; }
        public string[] Headers { get; private set; }

        public int IoLeituraPaginas { get; set; } = 0;
        public int PaginasGravadasDisco { get; set; } = 0;

        public Tabela(string nomeArquivo)
        {
            NomeArquivo = nomeArquivo;
            Pags = new List<Pagina.Pagina>();
            Headers = ReadHeaders(nomeArquivo);
            QtdCols = Headers.Length;
        }

        // lê cabeçalho
        private string[] ReadHeaders(string filePath)
        {
            if (!File.Exists(filePath))
                return Array.Empty<string>();

            using var reader = new StreamReader(filePath);
            var firstLine = reader.ReadLine();
            return string.IsNullOrEmpty(firstLine) ? Array.Empty<string>() : firstLine.Split(',');
        }

        public void CarregarDados(string delimitador = ",")
        {
            Pags.Clear();
            IoLeituraPaginas = 0;

            if (!File.Exists(NomeArquivo))
            {
                Console.WriteLine($"Aviso: Arquivo {NomeArquivo} não encontrado.");
                return;
            }

            var paginaAtual = new Pagina.Pagina();
            var linhas = File.ReadLines(NomeArquivo).Skip(1); // Pular cabeçalho
            foreach (var linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (paginaAtual.QtdTuplasOcup == 0)
                    IoLeituraPaginas++;

                try
                {
                    var tupla = Tupla.Tupla.DaLinhaArquivo(linha, QtdCols, delimitador);
                    if (!paginaAtual.AdicionarTupla(tupla))
                    {
                        Pags.Add(paginaAtual);
                        paginaAtual = new Pagina.Pagina();
                        IoLeituraPaginas++;
                        paginaAtual.AdicionarTupla(tupla);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Erro ao converter linha: {linha}\n{e.Message}");
                }
            }

            if (paginaAtual.QtdTuplasOcup > 0)
                Pags.Add(paginaAtual);
        }

        public void GravarEmArquivo(string delimitador = ",")
        {
            PaginasGravadasDisco = 0;
            using var sw = new StreamWriter(NomeArquivo);
            
            // Write headers first
            sw.WriteLine(string.Join(delimitador, Headers));
            
            foreach (var pagina in Pags.Where(p => p.QtdTuplasOcup > 0))
            {
                foreach (var tupla in pagina.Tuplas)
                    sw.WriteLine(tupla.ParaLinhaArquivo(delimitador));
                
                PaginasGravadasDisco++;
            }
        }

        // Método para gravar tuplas diretamente em um arquivo (útil para resultados de junção ou tabelas ordenadas)
        // Este método é mais flexível para arquivos grandes, pois não mantém tudo em memória.
        public static void GravarTuplasEmArquivo(string nomeArquivo, IEnumerable<Tupla.Tupla> tuplasParaGravar, 
            Action<int> atualizarContadorPaginas, string delimitador = ",", bool append = false, string[]? headers = null)
        {
            int tuplasNaPaginaAtual = 0;
            int paginasGravadas = 0;

            using var sw = new StreamWriter(nomeArquivo, append);
            
            // Write headers if this is a new file
            if (!append && headers != null)
                sw.WriteLine(string.Join(delimitador, headers));

            foreach (var tupla in tuplasParaGravar)
            {
                if (tuplasNaPaginaAtual == 0)
                    paginasGravadas++;

                sw.WriteLine(tupla.ParaLinhaArquivo(delimitador));
                tuplasNaPaginaAtual++;

                if (tuplasNaPaginaAtual >= Pagina.Pagina.MaxTuplasPorPagina)
                    tuplasNaPaginaAtual = 0;
            }

            atualizarContadorPaginas(paginasGravadas);
        }


        // Método para ler tuplas de um arquivo de forma iterativa
        public static IEnumerable<Tupla.Tupla> LerTuplasDeArquivoInterativo(string nomeArquivo, int qtdCols, 
            Action incrementarIO, string delimitador = ",")
        {
            if (!File.Exists(nomeArquivo))
                yield break;

            int tuplasLidasNaPaginaAtual = 0;

            using var sr = new StreamReader(nomeArquivo);
            sr.ReadLine(); // Pular linha do cabeçalho
            
            string? linha;
            while ((linha = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (tuplasLidasNaPaginaAtual == 0)
                    incrementarIO();

                yield return Tupla.Tupla.DaLinhaArquivo(linha, qtdCols, delimitador);
                tuplasLidasNaPaginaAtual++;

                if (tuplasLidasNaPaginaAtual >= Pagina.Pagina.MaxTuplasPorPagina)
                    tuplasLidasNaPaginaAtual = 0;
            }
        }


        public List<string> SortExternalRunsFromLoadedPages(
            out int totalPaginasGeradas,
            out int totalPaginasLidas,
            out int totalPaginasEscritas,
            string delimitador = ",",
            string colunaOrdenacao = "vinho_id"
        )
        {
            var runs = new List<string>();
            int runCount = 0;
            totalPaginasGeradas = 0;
            totalPaginasLidas = 0;
            totalPaginasEscritas = 0;

            string tmpDir = "CSVtmp";
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);
            int idxOrdenacao = Array.IndexOf(Headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");

            var buffer = new Buffer.BufferPaginas();

            using var reader = new StreamReader(NomeArquivo);
            reader.ReadLine(); // Pular cabeçalho

            while (!reader.EndOfStream)
            {
                buffer.Resetar();
                // Preenche o buffer com as páginas
                for (int j = 0; j < Buffer.BufferPaginas.Tamanho && !reader.EndOfStream; j++)
                {
                    bool paginaFoiLida = false;
                    while (buffer[j].QtdTuplasOcup < Pagina.Pagina.MaxTuplasPorPagina && !reader.EndOfStream)
                    {
                        var linha = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(linha)) continue;
                        var tupla = Tupla.Tupla.DaLinhaArquivo(linha, QtdCols, delimitador);
                        buffer[j].AdicionarTupla(tupla);
                        paginaFoiLida = true;
                    }
                    if (paginaFoiLida)
                        totalPaginasLidas++;
                }

                // Junta todas as tuplas do buffer
                var tuplasBuffer = buffer.Paginas.SelectMany(p => p.Tuplas).ToList();

                if (tuplasBuffer.Count == 0)
                    break;

                bool colunaNumerica = tuplasBuffer.All(t => int.TryParse(t.Cols[idxOrdenacao], out _));

                tuplasBuffer.Sort((a, b) =>
                {
                    var va = a.Cols[idxOrdenacao] ?? string.Empty;
                    var vb = b.Cols[idxOrdenacao] ?? string.Empty;
                    if (colunaNumerica)
                    {
                        int ia = int.Parse(va);
                        int ib = int.Parse(vb);
                        return ia.CompareTo(ib);
                    }
                    else
                    {
                        return string.Compare(va, vb, StringComparison.Ordinal);
                    }
                });

                runCount++;
                string runFile = Path.Combine(tmpDir, $"{Path.GetFileNameWithoutExtension(NomeArquivo)}_run_{runCount}.csv");
                runs.Add(runFile);

                int paginasGeradasNesteRun = 0;
                GravarTuplasEmArquivo(
                    runFile,
                    tuplasBuffer,
                    p => {
                        paginasGeradasNesteRun = p;
                    },
                    delimitador,
                    append: false,
                    headers: Headers
                );
                totalPaginasGeradas += paginasGeradasNesteRun;
            }
            totalPaginasEscritas = totalPaginasGeradas;
            buffer.Limpar();
            return runs;
        }


        public static string MultiwayMerge(
            out int totalPaginasGeradas,
            out int totalPaginasLidas,
            out int totalPaginasEscritas,
            List<string> listOfRuns,
            string[] headers,
            string colunaOrdenacao,
            string nomeTabela,
            string delimitador = ",")
        {
            int idxOrdenacao = Array.IndexOf(headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");

            int passo = 1;
            var listaAtual = new List<string>(listOfRuns);

            // Usa BufferPaginas em vez de List<Pagina.Pagina>
            var buffer = new Buffer.BufferPaginas();

            totalPaginasGeradas = 0;
            totalPaginasLidas = 0;
            totalPaginasEscritas = 0;
            while (listaAtual.Count > 1)
            {
                var listaNova = new List<string>();
                for (int i = 0; i < listaAtual.Count; i += Buffer.BufferPaginas.Tamanho - 1)
                {
                    // runFileGroup contém apenas os nomes dos arquivos das runs a serem mescladas
                    var runFileGroupNames = listaAtual.Skip(i).Take(Buffer.BufferPaginas.Tamanho - 1).ToList();
                    int paginasGeradasNesteMerge, paginasLidasNesteMerge, paginasEscritasNesteMerge;
                    string mergedRunFileName = MergeRunsWithBuffer(
                        runFileGroupNames, headers, idxOrdenacao, delimitador, passo, i / (Buffer.BufferPaginas.Tamanho - 1) + 1, buffer, nomeTabela,
                        out paginasGeradasNesteMerge, out paginasLidasNesteMerge, out paginasEscritasNesteMerge
                    );
                    listaNova.Add(mergedRunFileName);
                    totalPaginasGeradas += paginasGeradasNesteMerge;
                    totalPaginasLidas += paginasLidasNesteMerge;
                    totalPaginasEscritas += paginasEscritasNesteMerge;
                }
                listaAtual = listaNova;
                passo++;
            }
            return listaAtual[0];
        }

        // Função auxiliar para merge de runs usando buffer compartilhado
        private static string MergeRunsWithBuffer(
            List<string> runFiles,
            string[] headers,
            int idxOrdenacao,
            string delimitador,
            int passo,
            int grupo,
            Buffer.BufferPaginas buffer,
            string nomeTabela,
            out int paginasGeradas,
            out int paginasLidas,
            out int paginasEscritas)
        {
            bool colunaNumerica = true;
            foreach (var run in runFiles)
            {
                using var sr = new StreamReader(run);
                sr.ReadLine(); // pula cabeçalho
                var linha = sr.ReadLine();
                if (linha != null)
                {
                    var valor = linha.Split(delimitador)[idxOrdenacao];
                    if (!int.TryParse(valor, out _))
                    {
                        colunaNumerica = false;
                        break;
                    }
                }
            }
            int numEntradas = BufferPaginas.Tamanho - 1; // 3 páginas de entrada

            string tmpDir = "CSVtmp";
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);

            // Limpa o buffer antes de usar
            buffer.Limpar();

            // Abre um reader para cada run
            var readers = runFiles.Select(f => new StreamReader(f)).ToList();
            foreach (var r in readers) r.ReadLine(); // Pula cabeçalho

            paginasLidas = 0;
            paginasEscritas = 0;
            // Preenche as páginas de entrada do buffer
            for (int i = 0; i < runFiles.Count; i++)
            {
                while (buffer[i].QtdTuplasOcup < Pagina.Pagina.MaxTuplasPorPagina)
                {
                    var linha = readers[i].ReadLine();
                    if (linha == null) break;
                    var tupla = new Tupla.Tupla(linha.Split(delimitador));
                    buffer[i].AdicionarTupla(tupla);
                }
                if (buffer[i].QtdTuplasOcup > 0)
                    paginasLidas++;
            }

            string mergedFile = Path.Combine(tmpDir, $"{nomeTabela}_merged_passo{passo}_grupo{grupo}.csv");
            using var sw = new StreamWriter(mergedFile);
            sw.WriteLine(string.Join(delimitador, headers));

            int[] idxs = new int[numEntradas];  
            int paginasGravadas = 0;
            while (true)
            {
                var candidatos = new List<(int idxRun, Tupla.Tupla tupla)>();
                for (int i = 0; i < runFiles.Count; i++)
                {
                    if (idxs[i] < buffer[i].QtdTuplasOcup)
                        candidatos.Add((i, buffer[i].Tuplas[idxs[i]]));
                }

                if (candidatos.Count == 0)
                    break;

                (int idxRun, Tupla.Tupla tupla) menor;
                if (colunaNumerica)
                {
                    menor = candidatos.OrderBy(x => int.Parse(x.tupla.Cols[idxOrdenacao])).First();
                }
                else
                {
                    menor = candidatos.OrderBy(x => x.tupla.Cols[idxOrdenacao], StringComparer.Ordinal).First();
                }

                if (!buffer[BufferPaginas.Tamanho - 1].AdicionarTupla(menor.tupla))
                {
                    foreach (var t in buffer[BufferPaginas.Tamanho - 1].Tuplas)
                        sw.WriteLine(t.ParaLinhaArquivo(delimitador));
                    paginasGravadas++;
                    paginasEscritas++;
                    buffer[BufferPaginas.Tamanho - 1].Tuplas.Clear();
                    buffer[BufferPaginas.Tamanho - 1].AdicionarTupla(menor.tupla);
                }

                idxs[menor.idxRun]++;

                if (idxs[menor.idxRun] >= buffer[menor.idxRun].QtdTuplasOcup)
                {
                    buffer[menor.idxRun].Tuplas.Clear();
                    idxs[menor.idxRun] = 0;
                    while (buffer[menor.idxRun].QtdTuplasOcup < Pagina.Pagina.MaxTuplasPorPagina)
                    {
                        var linha = readers[menor.idxRun].ReadLine();
                        if (linha == null) break;
                        var tupla = new Tupla.Tupla(linha.Split(delimitador));
                        buffer[menor.idxRun].AdicionarTupla(tupla);
                    }
                    if (buffer[menor.idxRun].QtdTuplasOcup > 0)
                        paginasLidas++;
                }
            }

            if (buffer[BufferPaginas.Tamanho - 1].QtdTuplasOcup > 0)
            {
                foreach (var t in buffer[BufferPaginas.Tamanho - 1].Tuplas)
                    sw.WriteLine(t.ParaLinhaArquivo(delimitador));
                paginasGravadas++;
                paginasEscritas++;
            }

            foreach (var r in readers) r.Dispose();
            paginasGeradas = paginasGravadas;
            // Limpa o buffer para evitar problemas
            buffer.Limpar();
            return mergedFile;
        }
    }
}