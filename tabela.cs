using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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


        // Método para ler tuplas de um arquivo de forma iterativa (útil para External Sort e Merge Join)
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
            int bufferTamPaginas = 4, 
            string delimitador = ",", 
            string colunaOrdenacao = "vinho_id")
        {
            var runs = new List<string>();
            int runCount = 0;

            string tmpDir = "CSVtmp";
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);
            // Descobre o índice da coluna de ordenação
            int idxOrdenacao = Array.IndexOf(Headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");

            for (int i = 0; i < Pags.Count; i += bufferTamPaginas)
            {
                var bufferPaginas = Pags.Skip(i).Take(bufferTamPaginas).ToList();
                var tuplasBuffer = bufferPaginas.SelectMany(p => p.Tuplas).ToList();

                // Detecta se a coluna de ordenação é numérica
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

                GravarTuplasEmArquivo(
                    runFile,
                    tuplasBuffer,
                    _ => { },
                    delimitador,
                    append: false,
                    headers: Headers
                );
            }

            return runs;
        }

        public static string MultiwayMerge(
            List<string> listOfRuns,
            string[] headers,
            string colunaOrdenacao,
            string nomeTabela,
            int bufferTamPaginas = 4,
            string delimitador = ",")
        {
            int idxOrdenacao = Array.IndexOf(headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");

            int passo = 1;
            var listaAtual = new List<string>(listOfRuns);

            // Cria o buffer compartilhado (3 entradas + 1 saída)
            var buffer = new List<Pagina.Pagina>();
            for (int i = 0; i < bufferTamPaginas; i++)
                buffer.Add(new Pagina.Pagina());

            while (listaAtual.Count > 1)
            {
                var listaNova = new List<string>();
                for (int i = 0; i < listaAtual.Count; i += bufferTamPaginas - 1)
                {
                    // runFileGroup contém apenas os nomes dos arquivos das runs a serem mescladas
                    var runFileGroupNames = listaAtual.Skip(i).Take(bufferTamPaginas - 1).ToList();
                    string mergedRunFileName = MergeRunsWithBuffer(
                        runFileGroupNames, headers, idxOrdenacao, delimitador, passo, i / (bufferTamPaginas - 1) + 1, buffer, bufferTamPaginas,
                        nomeTabela
                    );
                    listaNova.Add(mergedRunFileName);
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
            List<Pagina.Pagina> buffer,
            int bufferTamPaginas,
            string nomeTabela)
            
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
            int numEntradas = bufferTamPaginas - 1; // 3 páginas de entrada

            string tmpDir = "CSVtmp";
                if (!Directory.Exists(tmpDir))
                    Directory.CreateDirectory(tmpDir);
            // Limpa o buffer antes de usar
            foreach (var pagina in buffer)
                pagina.Tuplas.Clear();

            // Abre um reader para cada run
            var readers = runFiles.Select(f => new StreamReader(f)).ToList();
            foreach (var r in readers) r.ReadLine(); // Pula cabeçalho

            // Preenche as páginas de entrada do buffer
            for (int i = 0; i < runFiles.Count; i++)
            {
                var pagina = buffer[i];
                while (pagina.QtdTuplasOcup < Pagina.Pagina.MaxTuplasPorPagina)
                {
                    var linha = readers[i].ReadLine();
                    if (linha == null) break;
                    var tupla = new Tupla.Tupla(linha.Split(delimitador));
                    pagina.AdicionarTupla(tupla);
                }
            }

            string mergedFile = Path.Combine(tmpDir, $"{nomeTabela}_merged_passo{passo}_grupo{grupo}.csv");
            using var sw = new StreamWriter(mergedFile);
            sw.WriteLine(string.Join(delimitador, headers));

            int[] idxs = new int[numEntradas];

            while (true)
            {
                var candidatos = new List<(int idxRun, Tupla.Tupla tupla)>();
                for (int i = 0; i < runFiles.Count; i++)
                {
                    var pagina = buffer[i];
                    if (idxs[i] < pagina.QtdTuplasOcup)
                        candidatos.Add((i, pagina.Tuplas[idxs[i]]));
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

                var paginaSaida = buffer[bufferTamPaginas - 1];
                if (!paginaSaida.AdicionarTupla(menor.tupla))
                {
                    foreach (var t in paginaSaida.Tuplas)
                        sw.WriteLine(t.ParaLinhaArquivo(delimitador));
                    paginaSaida.Tuplas.Clear();
                    paginaSaida.AdicionarTupla(menor.tupla);
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
                }
            }

            var paginaFinal = buffer[bufferTamPaginas - 1];
            if (paginaFinal.QtdTuplasOcup > 0)
            {
                foreach (var t in paginaFinal.Tuplas)
                    sw.WriteLine(t.ParaLinhaArquivo(delimitador));
            }

            foreach (var r in readers) r.Dispose();
            return mergedFile;
        }
    }
}