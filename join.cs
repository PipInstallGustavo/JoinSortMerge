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
            // Ordenar as tabelas primeiro
            string arquivoOrdenado1 = _tabela1.OrdenacaoExterna(_tabela1, _colunaTabela1);
            string arquivoOrdenado2 = _tabela2.OrdenacaoExterna(_tabela2, _colunaTabela2);

            // Recriar as tabelas ordenadas
            _tabela1 = new Tabela.Tabela(arquivoOrdenado1);
            _tabela2 = new Tabela.Tabela(arquivoOrdenado2);

            //pegar o index da coluna do join nos cabeçalhos
            int indexCol1 = Array.FindIndex(_tabela1.Headers, h => h.Equals(_colunaTabela1, StringComparison.OrdinalIgnoreCase));
            int indexCol2 = Array.FindIndex(_tabela2.Headers, h => h.Equals(_colunaTabela2, StringComparison.OrdinalIgnoreCase));

            if (indexCol1 == -1 || indexCol2 == -1)
            {
                Console.WriteLine("Erro: Coluna de junção não encontrada em uma das tabelas.");
                return;
            }

            int ioLeituraContador = 0;
            Action incrementarIO = () => ioLeituraContador++;

            var enumerator1 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela1.NomeArquivo, _tabela1.QtdCols, incrementarIO).GetEnumerator();

            var enumerator2 = Tabela.Tabela.LerTuplasDeArquivoInterativo(
                _tabela2.NomeArquivo, _tabela2.QtdCols  , incrementarIO).GetEnumerator();

            NumIOExecutados += ioLeituraContador;

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
                paginas => NumPagsGeradas = paginas, append: true);
            
            NumIOExecutados += NumPagsGeradas;

            // Deletar arquivos temporários
            try { File.Delete(arquivoOrdenado1); } catch { }
            try { File.Delete(arquivoOrdenado2); } catch { }
        }
    }
}