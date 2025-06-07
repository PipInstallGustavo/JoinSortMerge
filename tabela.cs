using System.Collections.Generic;
using System.IO; // Necessário para operações de arquivo
using System.Linq; // Para facilidades como string.Join
//classes
using Pagina;
using Tupla; 

namespace Tabela{
    // Representa uma tabela, que é uma coleção de páginas
    public class Tabela{
        // Lista de páginas na tabela 
        public List<Pagina> Pags { get; set; }
        // Quantidade de páginas na tabela 
        public int QtdPags { get { return Pags.Count; } }
        // Quantidade de colunas esperada para as tuplas desta tabela 
        public int QtdCols { get; private set; }
        public string NomeArquivo { get; private set; } // Para saber de/onde ler/gravar

        // Contadores para as métricas solicitadas 
        public int IoLeituraPaginas { get; set; } = 0;
        public int PaginasGravadasDisco { get; set; } = 0;


        public Tabela(string nomeArquivo)
        {
            NomeArquivo = nomeArquivo;
            Pags = new List<Pagina>();
        }

        public static int GetColumnCountSimple(string filePath, char delimiter = ',')
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"O arquivo não foi encontrado: {filePath}");
            }

            using (StreamReader reader = new StreamReader(filePath))
            {
                string firstLine = reader.ReadLine();
                if (string.IsNullOrEmpty(firstLine))
                {
                    return 0; // Arquivo vazio ou primeira linha vazia
                }

                // Conta o número de delimitadores e adiciona 1 para ter o número de colunas
                qtdCols = firstLine.Count(c => c == delimiter) + 1;
                return qtdCols;
            }

        // Carregar uma tabela inteira do disco para a memória (para tabelas pequenas)
        public void CarregarDados(string delimitador = ",")
        {
            Pags.Clear();
            IoLeituraPaginas = 0;

            if (!File.Exists(NomeArquivo))
            {
                // Tratar caso o arquivo não exista (pode ser uma tabela nova/vazia)
                System.Console.WriteLine($"Aviso: Arquivo {NomeArquivo} não encontrado.");
                return;
            }

            Pagina paginaAtual = new Pagina();
            string[] linhas = File.ReadAllLines(NomeArquivo); // Lê todas as linhas de uma vez

            foreach (string linha in linhas)
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                if (paginaAtual.QtdTuplasOcup == 0) // Início de uma nova "página lógica" lida
                {
                    IoLeituraPaginas++;
                }

                Tupla tupla = Tupla.DaLinhaArquivo(linha, QtdCols, delimitador);
                if (!paginaAtual.AdicionarTupla(tupla)) // Se a página atual está cheia
                {
                    Pags.Add(paginaAtual); // Adiciona a página cheia à tabela
                    paginaAtual = new Pagina(); // Cria uma nova página
                    IoLeituraPaginas++; // Contabiliza a leitura da "próxima" página lógica
                    paginaAtual.AdicionarTupla(tupla); // Adiciona a tupla à nova página
                }
            }

            // Adiciona a última página se ela tiver tuplas
            if (paginaAtual.QtdTuplasOcup > 0)
            {
                Pags.Add(paginaAtual);
            }
        }

        // Gravar uma tabela inteira da memória para o disco 
        public void GravarEmArquivo(string delimitador = ",")
        {
            PaginasGravadasDisco = 0;
            using (StreamWriter sw = new StreamWriter(NomeArquivo)) // Sobrescreve o arquivo se existir
            {
                foreach (Pagina pagina in Pags)
                {
                    if (pagina.QtdTuplasOcup > 0)
                    {
                        foreach (Tupla tupla in pagina.Tuplas)
                        {
                            sw.WriteLine(tupla.ParaLinhaArquivo(delimitador));
                        }
                        PaginasGravadasDisco++; // Contabiliza uma página gravada
                    }
                }
            }
        }

        // Método para gravar tuplas diretamente em um arquivo (útil para resultados de junção ou tabelas ordenadas)
        // Este método é mais flexível para arquivos grandes, pois não mantém tudo em memória.
        public static void GravarTuplasEmArquivo(string nomeArquivo, IEnumerable<Tupla> tuplasParaGravar, ref int paginasGravadasContador, string delimitador = ",")
        {
            int tuplasNaPaginaAtual = 0;
            using (StreamWriter sw = new StreamWriter(nomeArquivo))
            {
                foreach (Tupla tupla in tuplasParaGravar)
                {
                    if (tuplasNaPaginaAtual == 0) // Início de uma nova página a ser escrita
                    {
                        paginasGravadasContador++;
                    }
                    sw.WriteLine(tupla.ParaLinhaArquivo(delimitador));
                    tuplasNaPaginaAtual++;
                    if (tuplasNaPaginaAtual >= Pagina.MaxTuplasPorPagina)
                    {
                        tuplasNaPaginaAtual = 0; // Reseta para a próxima página
                    }
                }
            }
        }

        // Método para ler tuplas de um arquivo de forma iterativa (útil para External Sort e Merge Join)
        public static IEnumerable<Tupla> LerTuplasDeArquivoInterativo(string nomeArquivo, int qtdCols, ref int ioLeituraPaginasContador, string delimitador = ",")
        {
            if (!File.Exists(nomeArquivo))
            {
                // System.Console.WriteLine($"Aviso: Arquivo {nomeArquivo} não encontrado para leitura iterativa.");
                yield break; // Retorna um enumerador vazio
            }

            int tuplasLidasNaPaginaAtual = 0;
            using (StreamReader sr = new StreamReader(nomeArquivo))
            {
                string linha;
                while ((linha = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(linha)) continue;

                    if (tuplasLidasNaPaginaAtual == 0) // Início de leitura de uma nova página lógica
                    {
                        ioLeituraPaginasContador++;
                    }

                    yield return Tupla.DaLinhaArquivo(linha, qtdCols, delimitador);
                    tuplasLidasNaPaginaAtual++;

                    if (tuplasLidasNaPaginaAtual >= Pagina.MaxTuplasPorPagina)
                    {
                        tuplasLidasNaPaginaAtual = 0; // Reseta para a próxima página
                    }
                }
            }
        }
        private string OrdenacaoExterna(Tabela tabela, string colunaOrdenacao)
        {
            int maxPaginasMemoria = 4;
            int indexCol = GetColumnIndex(tabela, colunaOrdenacao);
            if (indexCol == -1)
                throw new System.Exception("Coluna de ordenação não encontrada.");

            List<string> arquivosRodadas = new List<string>();
            string header;

            // Buffer de até 4 páginas
            List<Pagina.Pagina> buffer = new List<Pagina.Pagina>(maxPaginasMemoria);

            // 1. Fase de rodadas iniciais
            using (var sr = new StreamReader(tabela.NomeArquivo))
            {
                header = sr.ReadLine();
                int rodada = 0;
                while (!sr.EndOfStream)
                {
                    buffer.Clear();
                    // Preenche o buffer com até 4 páginas
                    for (int p = 0; p < maxPaginasMemoria && !sr.EndOfStream; p++)
                    {
                        Pagina.Pagina pagina = new Pagina.Pagina();
                        int tuplasLidas = 0;
                        while (tuplasLidas < Pagina.Pagina.MaxTuplasPorPagina && !sr.EndOfStream)
                        {
                            string linha = sr.ReadLine();
                            if (linha == null) break;
                            var tupla = Tupla.Tupla.DaLinhaArquivo(linha, tabela.QtdCols);
                            pagina.AdicionarTupla(tupla);
                            tuplasLidas++;
                        }
                        if (pagina.QtdTuplasOcup > 0)
                            buffer.Add(pagina);
                    }
                    // Ordena todas as tuplas do buffer e redivide em páginas
                    var todasTuplas = buffer.SelectMany(pg => pg.Tuplas)
                                            .OrderBy(t => t.Cols[indexCol])
                                            .ToList();

                    string nomeRodada = $"{tabela.NomeArquivo}_rodada_{rodada}.tmp";
                    using (var sw = new StreamWriter(nomeRodada))
                    {
                        sw.WriteLine(header);
                        int count = 0;
                        Pagina.Pagina paginaEscrita = new Pagina.Pagina();
                        foreach (var tupla in todasTuplas)
                        {
                            paginaEscrita.AdicionarTupla(tupla);
                            if (paginaEscrita.QtdTuplasOcup == Pagina.Pagina.MaxTuplasPorPagina)
                            {
                                foreach (var t in paginaEscrita.Tuplas)
                                    sw.WriteLine(t.ParaLinhaArquivo());
                                paginaEscrita = new Pagina.Pagina();
                            }
                        }
                        // Grava o restante da última página
                        foreach (var t in paginaEscrita.Tuplas)
                            sw.WriteLine(t.ParaLinhaArquivo());
                    }
                    arquivosRodadas.Add(nomeRodada);
                    rodada++;
                }
            }

            // 2. Fase de merge (sempre 3 páginas para leitura, 1 para escrita)
            int fase = 0;
            while (arquivosRodadas.Count > 1)
            {
                List<string> novosArquivos = new List<string>();
                for (int i = 0; i < arquivosRodadas.Count; i += (maxPaginasMemoria - 1))
                {
                    var rodadasParaMerge = arquivosRodadas.Skip(i).Take(maxPaginasMemoria - 1).ToList();
                    if (rodadasParaMerge.Count == 1)
                    {
                        novosArquivos.Add(rodadasParaMerge[0]);
                        continue;
                    }
                    string nomeMerge = $"{tabela.NomeArquivo}_merge_{fase}_{i}.tmp";
                    using (var sw = new StreamWriter(nomeMerge))
                    {
                        // Abre todos os arquivos de rodada para merge
                        var readers = rodadasParaMerge.Select(r => new StreamReader(r)).ToList();
                        string mergeHeader = readers[0].ReadLine();
                        sw.WriteLine(mergeHeader);
                        foreach (var r in readers.Skip(1)) r.ReadLine(); // descarta header dos outros

                        // Buffer único: 3 páginas para leitura, 1 para escrita
                        buffer.Clear();
                        for (int r = 0; r < readers.Count; r++)
                        {
                            Pagina.Pagina paginaLeitura = new Pagina.Pagina();
                            int lidas = 0;
                            while (lidas < Pagina.Pagina.MaxTuplasPorPagina && !readers[r].EndOfStream)
                            {
                                string linha = readers[r].ReadLine();
                                if (linha == null) break;
                                paginaLeitura.AdicionarTupla(Tupla.Tupla.DaLinhaArquivo(linha, tabela.QtdCols));
                                lidas++;
                            }
                            buffer.Add(paginaLeitura);
                        }
                        // Completa o buffer com páginas vazias se necessário
                        while (buffer.Count < maxPaginasMemoria)
                            buffer.Add(new Pagina.Pagina());

                        // buffer[0..N-2]: leitura, buffer[N-1]: escrita
                        Pagina.Pagina bufferEscrita = buffer[maxPaginasMemoria - 1];

                        // Índices para rastrear posição de leitura em cada página
                        int[] idxs = new int[rodadasParaMerge.Count];
                        bool[] fimArquivo = new bool[rodadasParaMerge.Count];

                        while (fimArquivo.Any(f => !f))
                        {
                            // Encontra a menor tupla entre as páginas de leitura
                            int menorIdx = -1;
                            string menorValor = null;
                            for (int j = 0; j < rodadasParaMerge.Count; j++)
                            {
                                if (fimArquivo[j]) continue;
                                if (idxs[j] >= buffer[j].QtdTuplasOcup)
                                {
                                    // Tenta recarregar a próxima página do arquivo
                                    buffer[j] = new Pagina.Pagina();
                                    idxs[j] = 0;
                                    int lidas = 0;
                                    while (lidas < Pagina.Pagina.MaxTuplasPorPagina && !readers[j].EndOfStream)
                                    {
                                        string linha = readers[j].ReadLine();
                                        if (linha == null) break;
                                        buffer[j].AdicionarTupla(Tupla.Tupla.DaLinhaArquivo(linha, tabela.QtdCols));
                                        lidas++;
                                    }
                                    if (buffer[j].QtdTuplasOcup == 0)
                                    {
                                        fimArquivo[j] = true;
                                        continue;
                                    }
                                }
                                var tupla = buffer[j].Tuplas[idxs[j]];
                                if (menorIdx == -1 || string.Compare(tupla.Cols[indexCol], menorValor) < 0)
                                {
                                    menorIdx = j;
                                    menorValor = tupla.Cols[indexCol];
                                }
                            }
                            if (menorIdx == -1) break;

                            // Adiciona a menor tupla ao buffer de escrita
                            bufferEscrita.AdicionarTupla(buffer[menorIdx].Tuplas[idxs[menorIdx]]);
                            idxs[menorIdx]++;

                            // Se buffer de escrita encheu, grava e limpa
                            if (bufferEscrita.QtdTuplasOcup == Pagina.Pagina.MaxTuplasPorPagina)
                            {
                                foreach (var t in bufferEscrita.Tuplas)
                                    sw.WriteLine(t.ParaLinhaArquivo());
                                bufferEscrita = new Pagina.Pagina();
                                buffer[maxPaginasMemoria - 1] = bufferEscrita;
                            }
                        }
                        // Grava o restante da última página
                        foreach (var t in bufferEscrita.Tuplas)
                            sw.WriteLine(t.ParaLinhaArquivo());

                        foreach (var r in readers) r.Close();
                        foreach (var r in rodadasParaMerge) File.Delete(r);
                    }
                    novosArquivos.Add(nomeMerge);
                }
                arquivosRodadas = novosArquivos;
                fase++;
            }

            // O arquivo final ordenado é o único que restou
            return arquivosRodadas[0];
        }
    }
}
