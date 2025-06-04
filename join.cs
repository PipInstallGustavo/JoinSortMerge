using System.Collections.Generic;
using System.IO; 
using System.Linq; 
//namespaces
using Pagina; 
using Tupla;   
using Tabela;  

namespace SortMergeJoin{
    public class SortMergeJoin{
        private Tabela.Tabela _tabela1; // Tabela 1
        private Tabela.Tabela _tabela2; // Tabela 2
        private string _colunaTabela1; // Nome da coluna de junção na Tabela 1
        private string _colunaTabela2; // Nome da coluna de junção na Tabela 2
        private string _arquivoSaida; // Nome do arquivo para o resultado 

        // Métricas de desempenho
        public int NumPagsGeradas { get; private set; } // Número de páginas de resultado geradas
        public int NumIOExecutados { get; private set; } // Número total de operações de IO (leitura e escrita)
        public int NumTuplasGeradas { get; private set; } // Número de tuplas resultantes da junção

        // Construtor
        public SortMergeJoin(Tabela.Tabela tabela1, Tabela.Tabela tabela2, string colunaTabela1, string colunaTabela2, string arquivoSaida)
        {
            _tabela1 = tabela1;
            _tabela2 = tabela2;
            _colunaTabela1 = colunaTabela1;
            _colunaTabela2 = colunaTabela2;
            _arquivoSaida = arquivoSaida;

            NumPagsGeradas = 0;
            NumIOExecutados = 0;
            NumTuplasGeradas = 0;
        }

        public void Executar(){
            //Assumindo que as tabelas já estão ordenadas
            //(IMPLEMENTAR)
            // arquivo.OrdenacaoExterna(tabela1);
            // arquivo.OrdenacaoExterna(tabela2);

            // Obter os índices das colunas de junção para acesso eficiente
            int indexCol1 = GetColumnIndex(_tabela1, _colunaTabela1);
            int indexCol2 = GetColumnIndex(_tabela2, _colunaTabela2);

            if (indexCol1 == -1 || indexCol2 == -1){
                System.Console.WriteLine("Erro: Coluna de junção não encontrada em uma das tabelas.");
                return;
            }

            // Usar iteradores para ler tuplas dos arquivos ordenados
            IEnumerator<Tupla.Tupla> enumerator1 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela1.NomeArquivo, _tabela1.QtdCols, ref NumIOExecutados).GetEnumerator();
            IEnumerator<Tupla.Tupla> enumerator2 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela2.NomeArquivo, _tabela2.QtdCols, ref NumIOExecutados).GetEnumerator();

            bool Next1 = enumerator1.MoveNext();
            bool Next2 = enumerator2.MoveNext();

            List<Tupla.Tupla> tuplasResultantes = new List<Tupla.Tupla>();

            while (Next1 && Next2){
                Tupla.Tupla tupla1 = enumerator1.Current;
                Tupla.Tupla tupla2 = enumerator2.Current;

                string valorCol1 = tupla1.Cols[indexCol1];
                string valorCol2 = tupla2.Cols[indexCol2];

                int comparisonResult = string.Compare(valorCol1, valorCol2);

                if (comparisonResult < 0) { // tupla1.col < tupla2.col
                    Next1 = enumerator1.MoveNext(); // Avança na Tabela 1
                }
                else if (comparisonResult > 0) {// tupla1.col > tupla2.col
                    Next2 = enumerator2.MoveNext(); // Avança na Tabela 2
                }
                else {// Chaves iguais, realizar junção{
                    // Lidar com tuplas duplicadas na chave de junção. Para um Sort Merge Join robusto, precisamos coletar todas as tuplas de ambas as tabelas que correspondem ao valor da chave atual.
                    List<Tupla.Tupla> matchingTuplas1 = new List<Tupla.Tupla>();
                    List<Tupla.Tupla> matchingTuplas2 = new List<Tupla.Tupla>();

                    // Coleta todas as tuplas de Tabela 1 com o valor de chave atual
                    matchingTuplas1.Add(tupla1);
                    Next1 = enumerator1.MoveNext();
                    while (Next1 && string.Compare(enumerator1.Current.Cols[indexCol1], valorCol1) == 0){
                        matchingTuplas1.Add(enumerator1.Current);
                        Next1 = enumerator1.MoveNext();
                    }

                    // Coleta todas as tuplas de Tabela 2 com o valor de chave atual
                    matchingTuplas2.Add(tupla2);
                    Next2 = enumerator2.MoveNext();
                    while (Next2 && string.Compare(enumerator2.Current.Cols[indexCol2], valorCol2) == 0){
                        matchingTuplas2.Add(enumerator2.Current);
                        Next2 = enumerator2.MoveNext();
                    }

                    // Combina todas as tuplas correspondentes
                    foreach (var mTupla1 in matchingTuplas1){
                        foreach (var mTupla2 in matchingTuplas2){
                            // Combina as colunas das duas tuplas.
                            string[] combinedCols = new string[mTupla1.QtdCols + mTupla2.QtdCols];
                            mTupla1.Cols.CopyTo(combinedCols, 0);
                            mTupla2.Cols.CopyTo(combinedCols, mTupla1.QtdCols);
                            
                            tuplasResultantes.Add(new Tupla.Tupla(combinedCols));
                            NumTuplasGeradas++;
                        }
                    }
                }
            }
            
            // Grava as tuplas resultantes em um novo arquivo.
            // O contador de páginas gravadas é atualizado dentro deste método estático.
            Tabela.Tabela.GravarTuplasEmArquivo(_arquivoSaida, tuplasResultantes, ref NumPagsGeradas);
            // A contagem de IOs para escrita já está sendo adicionada em GravarTuplasEmArquivo via NumPagsGeradas
            NumIOExecutados += NumPagsGeradas;
        }

        // Método auxiliar para encontrar o índice de uma coluna na tabela
        private int GetColumnIndex(Tabela.Tabela tabela, string columnName)
        {
            // Para obter o índice da coluna, precisaríamos do cabeçalho do CSV ou
            // de um esquema definido na classe Tabela.
            // Como Tabela não armazena nomes de colunas explicitamente neste exemplo,
            // vamos simular lendo a primeira linha do arquivo para descobrir os cabeçalhos.
            // Isso adiciona um IO extra se a Tabela não carregar cabeçalhos.
            if (!File.Exists(tabela.NomeArquivo))
            {
                return -1; // Arquivo não existe
            }

            using (StreamReader sr = new StreamReader(tabela.NomeArquivo))
            {
                string headerLine = sr.ReadLine();
                if (string.IsNullOrEmpty(headerLine))
                {
                    return -1; // Arquivo vazio
                }
                string[] headers = headerLine.Split(','); // Assumindo vírgula como delimitador
                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i].Trim().Equals(columnName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return -1; // Coluna não encontrada
        }
    }
}