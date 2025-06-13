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
            
            // escrever cabeçalhos primeiro
            sw.WriteLine(string.Join(delimitador, Headers));
            
            foreach (var pagina in Pags.Where(p => p.QtdTuplasOcup > 0))
            {
                foreach (var tupla in pagina.Tuplas)
                    sw.WriteLine(tupla.ParaLinhaArquivo(delimitador));
                
                PaginasGravadasDisco++;
            }
        }

        // Método para gravar tuplas diretamente em um arquivo
        public static void GravarTuplasEmArquivo(string nomeArquivo, IEnumerable<Tupla.Tupla> tuplasParaGravar, 
            Action<int> atualizarContadorPaginas, string delimitador = ",", bool append = false, string[]? headers = null)
        {
            int tuplasNaPaginaAtual = 0;
            int paginasGravadas = 0;

            using var sw = new StreamWriter(nomeArquivo, append);
            
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
            int tuplasLidasTotal = 0;

            using var sr = new StreamReader(nomeArquivo);
            sr.ReadLine(); // Pular linha do cabeçalho
            
            string? linha;
            while ((linha = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (tuplasLidasNaPaginaAtual == 0)
                    incrementarIO(); //incrementa o quantidade de I/Os 

                yield return Tupla.Tupla.DaLinhaArquivo(linha, qtdCols, delimitador);
                tuplasLidasNaPaginaAtual++;
                tuplasLidasTotal++;

                if (tuplasLidasNaPaginaAtual >= Pagina.Pagina.MaxTuplasPorPagina)
                    tuplasLidasNaPaginaAtual=0;

            }


        }


        public List<string> SortExternalRuns(
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

            // Diretório dos arquivos temporários
            string tmpDir = "CSVtmp";
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);
            // Verifica se a coluna de ordenação existe
            int idxOrdenacao = Array.IndexOf(Headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");
            // Inicializa o buffer
            var buffer = new Buffer.BufferPaginas();

            using var reader = new StreamReader(NomeArquivo);
            reader.ReadLine(); // Pular cabeçalho

            while (!reader.EndOfStream)
            {
                buffer.Resetar();
                // Preenche o buffer com até 4 páginas e adiciona as tuplas nas páginas
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

                // Verifica se a coluna de ordenação é numérica ou alfanumérica
                bool colunaNumerica = tuplasBuffer.All(t => int.TryParse(t.Cols[idxOrdenacao], out _));
                // Ordena as tuplas no buffer usando a coluna de ordenação
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
                // Cria o nome do arquivo para a run
                string runFile = Path.Combine(tmpDir, $"{Path.GetFileNameWithoutExtension(NomeArquivo)}_run_{runCount}.csv");
                // Adiciona o nome do arquivo a lista de runs 
                runs.Add(runFile);

                // Grava as tuplas ordenadas em um arquivo
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
            // Verifica se a coluna de ordenação existe no cabeçalho
            int idxOrdenacao = Array.IndexOf(headers, colunaOrdenacao);
            if (idxOrdenacao == -1)
                throw new ArgumentException($"Coluna '{colunaOrdenacao}' não encontrada no cabeçalho.");

            int passo = 1;
            // Inicializa a lista atual com os nomes dos arquivos das runs
            // listOfRuns contém os nomes dos arquivos das runs a serem mescladas
            var listaAtual = new List<string>(listOfRuns);

            // Inicializa o buffer
            var buffer = new Buffer.BufferPaginas();

            totalPaginasGeradas = 0;
            totalPaginasLidas = 0;
            totalPaginasEscritas = 0;
            while (listaAtual.Count > 1)
            {
                var listaNova = new List<string>();
                for (int i = 0; i < listaAtual.Count; i += Buffer.BufferPaginas.Tamanho - 1)
                {
                    // Agrupa os arquivos de run em grupos de tamanho Buffer.BufferPaginas.Tamanho - 1
                    var runFileGroupNames = listaAtual.Skip(i).Take(Buffer.BufferPaginas.Tamanho - 1).ToList();

                    int paginasGeradasNesteMerge, paginasLidasNesteMerge, paginasEscritasNesteMerge;
                    // Realiza o merge dos arquivos de run usando o buffer
                    string mergedRunFileName = MergeRunsWithBuffer(
                        runFileGroupNames, headers, idxOrdenacao, delimitador, passo, i / (Buffer.BufferPaginas.Tamanho - 1) + 1, buffer, nomeTabela,
                        out paginasGeradasNesteMerge, out paginasLidasNesteMerge, out paginasEscritasNesteMerge
                    );
                    // Adiciona o nome do arquivo mesclado à lista nova
                    listaNova.Add(mergedRunFileName);
                    totalPaginasGeradas += paginasGeradasNesteMerge;
                    totalPaginasLidas += paginasLidasNesteMerge;
                    totalPaginasEscritas += paginasEscritasNesteMerge;
                }
                // Se a lista nova tiver apenas um arquivo, significa que o merge está completo
                listaAtual = listaNova;
                passo++;
            }
            // Retorna o nome do arquivo final que contém a tabela ordenada
            return listaAtual[0];
        }

        // Função auxiliar para merge de runs
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
            // Verifica se há runs suficientes para o merge
            bool colunaNumerica = true;
            foreach (var run in runFiles)
            {
                using var sr = new StreamReader(run);
                sr.ReadLine(); // pula cabeçalho
                var linha = sr.ReadLine();
                if (linha != null)
                {
                    var valor = linha.Split(delimitador)[idxOrdenacao];
                    // Verifica se o valor é numérico
                    if (!int.TryParse(valor, out _))
                    {
                        colunaNumerica = false;
                        break;
                    }
                }
            }
            int numEntradas = BufferPaginas.Tamanho - 1; // 3 páginas de entrada
            // Define o diretorio dos arquivos temporários
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
            // Preenche as páginas de entrada do buffer com tuplas
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
            // Define  o nome do arquivo mesclado
            // que será gerado após o merge
            string mergedFile = Path.Combine(tmpDir, $"{nomeTabela}_merged_passo{passo}_grupo{grupo}.csv");
            using var sw = new StreamWriter(mergedFile);
            sw.WriteLine(string.Join(delimitador, headers));

            int[] idxs = new int[numEntradas];  
            int paginasGravadas = 0;
            while (true)
            {
                // Cria uma lista de candidatos para a próxima tupla a ser escrita
                // Cada candidato é uma tupla de (índice da run, tupla) que representa a próxima tupla de cada run 
                // que ainda tem tuplas disponíveis nas páginas do buffer
                // que será usada para determinar qual tupla é a menor entre as páginas no buffer
                // Se não houver candidatos, sai do loop
                var candidatos = new List<(int idxRun, Tupla.Tupla tupla)>();
                for (int i = 0; i < runFiles.Count; i++)
                {
                    if (idxs[i] < buffer[i].QtdTuplasOcup)
                        candidatos.Add((i, buffer[i].Tuplas[idxs[i]]));
                }

                if (candidatos.Count == 0)
                    break;
                // Encontra a menor tupla entre os candidatos
                (int idxRun, Tupla.Tupla tupla) menor;
                if (colunaNumerica)
                {
                    menor = candidatos.OrderBy(x => int.Parse(x.tupla.Cols[idxOrdenacao])).First();
                }
                else
                {
                    menor = candidatos.OrderBy(x => x.tupla.Cols[idxOrdenacao], StringComparer.Ordinal).First();
                }
                // Adiciona a menor tupla à última página do buffer
                // Se a última página do buffer estiver cheia, grava ela no arquivo
                if (!buffer[BufferPaginas.Tamanho - 1].AdicionarTupla(menor.tupla))
                {
                    // Grava a última página no arquivo
                    foreach (var t in buffer[BufferPaginas.Tamanho - 1].Tuplas)
                        sw.WriteLine(t.ParaLinhaArquivo(delimitador));
                    // Reseta a última página do buffer e adiciona a menor tupla
                    paginasGravadas++;
                    paginasEscritas++;
                    buffer[BufferPaginas.Tamanho - 1].Tuplas.Clear();
                    buffer[BufferPaginas.Tamanho - 1].AdicionarTupla(menor.tupla);
                }
                // Incrementa o índice da run da menor tupla
                // e lê a próxima tupla dessa run
                idxs[menor.idxRun]++;
                // Quando todas as tuplas da página de uma run já foram usadas
                // menor.idx aponta para a run de onde saiu a menor tupla
                if (idxs[menor.idxRun] >= buffer[menor.idxRun].QtdTuplasOcup)
                {
                    // Limpa a página de buffer corresponodente aquela run
                    buffer[menor.idxRun].Tuplas.Clear();
                    idxs[menor.idxRun] = 0;
                    // Lê mais tuplas dessa run para preencher a página de buffer
                    // até que ela esteja cheia ou não haja mais tuplas
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
            // Verifica se ainda há tuplas não gravadas na última página do buffer
            // Se houver, grava elas no arquivo
            if (buffer[BufferPaginas.Tamanho - 1].QtdTuplasOcup > 0)
            {
                foreach (var t in buffer[BufferPaginas.Tamanho - 1].Tuplas)
                    sw.WriteLine(t.ParaLinhaArquivo(delimitador));
                paginasGravadas++;
                paginasEscritas++;
            }
            // Fecha os readers abertos
            foreach (var r in readers) r.Dispose();
            
            paginasGeradas = paginasGravadas;
            // Limpa o buffer para evitar problemas
            buffer.Limpar();
            return mergedFile;
        }
    }
}