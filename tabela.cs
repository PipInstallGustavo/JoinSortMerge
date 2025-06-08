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

        //Método da Ordenação Externa com Buffer de 4 frames
        public string OrdenacaoExterna(Tabela tabela, string colunaOrdenacao)
        {
            const int maxPaginasMemoria = 4;
            int indexCol = Array.FindIndex(tabela.Headers, h => h.Equals(colunaOrdenacao, StringComparison.OrdinalIgnoreCase));
            
            if (indexCol == -1)
                throw new Exception($"Coluna de ordenação '{colunaOrdenacao}' não encontrada.");

            var arquivosRodadas = new List<string>();
            string header = string.Join(",", tabela.Headers);

            using (var sr = new StreamReader(tabela.NomeArquivo))
            {
                sr.ReadLine(); // Skip header
                int rodada = 0;
                
                while (!sr.EndOfStream)
                {
                    var buffer = new List<Pagina.Pagina>(maxPaginasMemoria);
                    
                    for (int p = 0; p < maxPaginasMemoria && !sr.EndOfStream; p++)
                    {
                        var pagina = new Pagina.Pagina();
                        int tuplasLidas = 0;
                        
                        while (tuplasLidas < Pagina.Pagina.MaxTuplasPorPagina && !sr.EndOfStream)
                        {
                            var linha = sr.ReadLine();
                            if (linha == null) break;
                            
                            var tupla = Tupla.Tupla.DaLinhaArquivo(linha, tabela.QtdCols);
                            pagina.AdicionarTupla(tupla);
                            tuplasLidas++;
                        }
                        
                        if (pagina.QtdTuplasOcup > 0)
                            buffer.Add(pagina);
                    }

                    var todasTuplas = buffer.SelectMany(pg => pg.Tuplas)
                                        .OrderBy(t => t.Cols[indexCol])
                                        .ToList();

                    string nomeRodada = $"{tabela.NomeArquivo}_rodada_{rodada}.tmp";
                    using (var sw = new StreamWriter(nomeRodada))
                    {
                        sw.WriteLine(header);
                        var paginaEscrita = new Pagina.Pagina();
                        
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
                        
                        foreach (var t in paginaEscrita.Tuplas)
                            sw.WriteLine(t.ParaLinhaArquivo());
                    }
                    
                    arquivosRodadas.Add(nomeRodada);
                    rodada++;
                }
            }

            int fase = 0;
            while (arquivosRodadas.Count > 1)
            {
                var novosArquivos = new List<string>();
                
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
                        var readers = rodadasParaMerge.Select(r => new StreamReader(r)).ToList();
                        var mergeHeader = readers[0].ReadLine();
                        sw.WriteLine(mergeHeader);
                        
                        foreach (var r in readers.Skip(1)) 
                            r.ReadLine();

                        var buffer = new List<Pagina.Pagina>();
                        for (int r = 0; r < readers.Count; r++)
                        {
                            var paginaLeitura = new Pagina.Pagina();
                            int lidas = 0;
                            
                            while (lidas < Pagina.Pagina.MaxTuplasPorPagina && !readers[r].EndOfStream)
                            {
                                var linha = readers[r].ReadLine();
                                if (linha == null) break;
                                
                                paginaLeitura.AdicionarTupla(Tupla.Tupla.DaLinhaArquivo(linha, tabela.QtdCols));
                                lidas++;
                            }
                            
                            buffer.Add(paginaLeitura);
                        }
                        
                        while (buffer.Count < maxPaginasMemoria)
                            buffer.Add(new Pagina.Pagina());

                        var bufferEscrita = buffer[maxPaginasMemoria - 1];
                        var idxs = new int[rodadasParaMerge.Count];
                        var fimArquivo = new bool[rodadasParaMerge.Count];

                        while (fimArquivo.Any(f => !f))
                        {
                            int menorIdx = -1;
                            string? menorValor = null;
                            
                            for (int j = 0; j < rodadasParaMerge.Count; j++)
                            {
                                if (fimArquivo[j]) continue;
                                
                                if (idxs[j] >= buffer[j].QtdTuplasOcup)
                                {
                                    buffer[j] = new Pagina.Pagina();
                                    idxs[j] = 0;
                                    int lidas = 0;
                                    
                                    while (lidas < Pagina.Pagina.MaxTuplasPorPagina && !readers[j].EndOfStream)
                                    {
                                        var linha = readers[j].ReadLine();
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

                            bufferEscrita.AdicionarTupla(buffer[menorIdx].Tuplas[idxs[menorIdx]]);
                            idxs[menorIdx]++;

                            if (bufferEscrita.QtdTuplasOcup == Pagina.Pagina.MaxTuplasPorPagina)
                            {
                                foreach (var t in bufferEscrita.Tuplas)
                                    sw.WriteLine(t.ParaLinhaArquivo());
                                
                                bufferEscrita = new Pagina.Pagina();
                                buffer[maxPaginasMemoria - 1] = bufferEscrita;
                            }
                        }
                        
                        foreach (var t in bufferEscrita.Tuplas)
                            sw.WriteLine(t.ParaLinhaArquivo());

                        foreach (var r in readers) 
                            r.Close();
                    }
                    
                    novosArquivos.Add(nomeMerge);
                }
                
                arquivosRodadas = novosArquivos;
                fase++;
            }

            return arquivosRodadas[0];
        }
    }
}