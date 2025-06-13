using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Operador
{
    public class Operador
    {
        private Tabela.Tabela _tabela1; //tabela de entrada 1
        private Tabela.Tabela _tabela2; //tabela de entrada 2
        private readonly string _colunaTabela1; //coluna da tabela 1
        private readonly string _colunaTabela2; //coluna da tabela 2
        private readonly string _arquivoSaida; //arquivo de saída(csv)

        public int NumPagsGeradas { get; private set; }
        public int NumIOExecutados { get; private set; }
        public int NumTuplasGeradas { get; private set; }

        //construtor
        public Operador(Tabela.Tabela tabela1, Tabela.Tabela tabela2, 
            string colunaTabela1, string colunaTabela2, string arquivoSaida)
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

        public void Executar()
        {
            int paginasRunsGeradas1, paginasRunsLidas1, paginasRunsEscritas1;
            var runs1 = _tabela1.SortExternalRunsFromLoadedPages(
                out paginasRunsGeradas1, out paginasRunsLidas1, out paginasRunsEscritas1, colunaOrdenacao: _colunaTabela1);

            Console.WriteLine($"[DEBUG] Páginas geradas nos runs da tabela 1: {paginasRunsGeradas1}");
            Console.WriteLine($"[DEBUG] Páginas lidas nos runs da tabela 1: {paginasRunsLidas1}");
            Console.WriteLine($"[DEBUG] Páginas escritas nos runs da tabela 1: {paginasRunsEscritas1}");


            int paginasRunsGeradas2, paginasRunsLidas2, paginasRunsEscritas2;
            var runs2 = _tabela2.SortExternalRunsFromLoadedPages(
                out paginasRunsGeradas2, out paginasRunsLidas2, out paginasRunsEscritas2, colunaOrdenacao: _colunaTabela2);
            Console.WriteLine($"[DEBUG] Páginas geradas nos runs da tabela 2: {paginasRunsGeradas2}");
            Console.WriteLine($"[DEBUG] Páginas lidas nos runs da tabela 2: {paginasRunsLidas2}");
            Console.WriteLine($"[DEBUG] Páginas escritas nos runs da tabela 2: {paginasRunsEscritas2}");

            int paginasMergeGeradas1, paginasMergeLidas1, paginasMergeEscritas1;
            string arquivoOrdenado1 = Tabela.Tabela.MultiwayMerge(
                out paginasMergeGeradas1, out paginasMergeLidas1, out paginasMergeEscritas1,
                runs1,
                _tabela1.Headers,
                _colunaTabela1,
                Path.GetFileNameWithoutExtension(_tabela1.NomeArquivo)
            );
            Console.WriteLine($"[DEBUG] Páginas geradas no merge da tabela 1: {paginasMergeGeradas1}");
            Console.WriteLine($"[DEBUG] Páginas lidas no merge da tabela 1: {paginasMergeLidas1}");
            Console.WriteLine($"[DEBUG] Páginas escritas no merge da tabela 1: {paginasMergeEscritas1}");


            int paginasMergeGeradas2, paginasMergeLidas2, paginasMergeEscritas2;
            string arquivoOrdenado2 = Tabela.Tabela.MultiwayMerge(
                out paginasMergeGeradas2, out paginasMergeLidas2, out paginasMergeEscritas2,
                runs2,
                _tabela2.Headers,
                _colunaTabela2,
                Path.GetFileNameWithoutExtension(_tabela2.NomeArquivo)
            );
            Console.WriteLine($"[DEBUG] Páginas geradas no merge da tabela 2: {paginasMergeGeradas2}");
            Console.WriteLine($"[DEBUG] Páginas lidas no merge da tabela 2: {paginasMergeLidas2}");
            Console.WriteLine($"[DEBUG] Páginas escritas no merge da tabela 2: {paginasMergeEscritas2}");

            NumIOExecutados += paginasRunsLidas1 + paginasRunsEscritas1
                + paginasRunsLidas2 + paginasRunsEscritas2
                + paginasMergeLidas1 + paginasMergeEscritas1
                + paginasMergeLidas2 + paginasMergeEscritas2;
            //pegar o index da coluna do join nos cabeçalhos
            int indexCol1 = Array.FindIndex(_tabela1.Headers, h => h.Equals(_colunaTabela1, StringComparison.OrdinalIgnoreCase));
            int indexCol2 = Array.FindIndex(_tabela2.Headers, h => h.Equals(_colunaTabela2, StringComparison.OrdinalIgnoreCase));

            if (indexCol1 == -1 || indexCol2 == -1)
            {
                Console.WriteLine("Erro: Coluna de junção não encontrada em uma das tabelas.");
                return;
            }

            Action incrementarIO_t1 = () => NumIOExecutados++;
            Action incrementarIO_t2 = () => NumIOExecutados++;

            var enumerator1 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela1.NomeArquivo, _tabela1.QtdCols, incrementarIO_t1).GetEnumerator();

            var enumerator2 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela2.NomeArquivo, _tabela2.QtdCols, incrementarIO_t2).GetEnumerator();


            bool hasNext1 = enumerator1.MoveNext();
            bool hasNext2 = enumerator2.MoveNext();

            var tuplasResultantes = new List<Tupla.Tupla>();

            //combinar cabeçalhos para o output
            var outputHeaders = _tabela1.Headers.Concat(_tabela2.Headers).ToArray();

            while (hasNext1 && hasNext2)
            {
                var tupla1 = enumerator1.Current;
                var tupla2 = enumerator2.Current;

                string valorCol1 = tupla1.Cols[indexCol1];
                string valorCol2 = tupla2.Cols[indexCol2];

                //etapa de comparação
                int comparisonResult = string.Compare(valorCol1, valorCol2);

                if (comparisonResult < 0)
                {
                    hasNext1 = enumerator1.MoveNext();
                }
                else if (comparisonResult > 0)
                {
                    hasNext2 = enumerator2.MoveNext();
                }
                else
                {
                    var matchingTuplas1 = new List<Tupla.Tupla> { tupla1 };
                    hasNext1 = enumerator1.MoveNext();

                    while (hasNext1 && string.Compare(enumerator1.Current.Cols[indexCol1], valorCol1) == 0)
                    {
                        matchingTuplas1.Add(enumerator1.Current);
                        hasNext1 = enumerator1.MoveNext();
                    }

                    var matchingTuplas2 = new List<Tupla.Tupla> { tupla2 };
                    hasNext2 = enumerator2.MoveNext();

                    while (hasNext2 && string.Compare(enumerator2.Current.Cols[indexCol2], valorCol2) == 0)
                    {
                        matchingTuplas2.Add(enumerator2.Current);
                        hasNext2 = enumerator2.MoveNext();
                    }

                    foreach (var mTupla1 in matchingTuplas1)
                    {
                        foreach (var mTupla2 in matchingTuplas2)
                        {
                            var combinedCols = new string[mTupla1.QtdCols + mTupla2.QtdCols];
                            mTupla1.Cols.CopyTo(combinedCols, 0);
                            mTupla2.Cols.CopyTo(combinedCols, mTupla1.QtdCols);

                            tuplasResultantes.Add(new Tupla.Tupla(combinedCols));
                            NumTuplasGeradas++;
                        }
                    }
                }
            }

            // criar o arquivo de saída
            File.WriteAllText(_arquivoSaida, string.Join(",", outputHeaders) + Environment.NewLine);
             Tabela.Tabela.GravarTuplasEmArquivo(_arquivoSaida, tuplasResultantes,
                paginas => {
                    NumPagsGeradas = paginas;
                    Console.WriteLine($"[DEBUG] Páginas geradas na gravação do resultado final: {paginas}");
                },
                append: true
            );
            NumIOExecutados += NumPagsGeradas; // cada página escrita do resultado final conta como IO
            NumPagsGeradas += paginasRunsGeradas1 + paginasRunsGeradas2
                + paginasMergeGeradas1 + paginasMergeGeradas2; // total de páginas geradas é a soma das páginas dos runs e merges
            
        }
    }

}



