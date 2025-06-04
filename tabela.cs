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
    }
}
