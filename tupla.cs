using System.Collections.Generic;
using System.IO; // Necessário para operações de arquivo
using System.Linq; // Para facilidades como string.Join

namespace Tupla{
    // Representa uma tupla (linha) de uma tabela
    public class Tupla
    {
        // Array de strings para armazenar os valores das colunas
        public string[] Cols { get; set; }
        // Quantidade de colunas nesta tupla
        public int QtdCols { get; private set; }

        public Tupla(int qtdCols)
        {
            QtdCols = qtdCols;
            Cols = new string[qtdCols];
        }

        public Tupla(string[] valores)
        {
            Cols = valores;
            QtdCols = valores.Length;
        }

        // Método para formatar a tupla para gravação em arquivo (ex: CSV)
        public string ParaLinhaArquivo(string delimitador = ",")
        {
            return string.Join(delimitador, Cols);
        }

        // Método estático para criar uma tupla a partir de uma linha do arquivo
        public static Tupla DaLinhaArquivo(string linha, int qtdEsperadaCols, string delimitador = ",")
        {
            string[] valores = linha.Split(new[] { delimitador }, System.StringSplitOptions.None);
            // Adicionar verificação se valores.Length == qtdEsperadaCols se necessário
            return new Tupla(valores);
        }
    }
}