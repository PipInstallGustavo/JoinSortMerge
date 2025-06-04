using System;

// VOCÊ DEVE INCLUIR SEUS NAMESPACES AQUI, CASO NECESSÁRIO!!!

class Program
{
    static void Main(string[] args)
    {
        Tabela vinho = new Tabela("CSVs/vinho.csv"); // cria estrutura necessária para a tabela
        Tabela uva = new Tabela("CSVs/uva.csv");
        Tabela pais = new Tabela("CSVs/pais.csv");

        vinho.CarregarDados(); // lê os dados do csv e adiciona na estrutura da tabela, caso necessário
        uva.CarregarDados();
        pais.CarregarDados();

        // IMPLEMENTE O OPERADOR E DEPOIS EXECUTE AQUI
        Operador op = new Operador(vinho, uva, "vinho_id", "uva_id");
        // significa: SELECT * FROM Vinho V, Uva U WHERE V.vinho_id = U.uva_id
        // IMPORTANTE: isso é só um exemplo, podem ser tabelas/colunas distintas.
        // genericamente: Operador(tabela_1, tabela_2, col_tab_1, col_tab_2):
        // significa: SELECT * FROM tabela_1, tabela_2 WHERE col_tab_1 = col_tab_2

        // op.Executar(); // Realiza a operação desejada

        // Console.WriteLine($"#Pags: {op.NumPagsGeradas()}"); // Retorna a quantidade de páginas geradas pela operação
        // Console.WriteLine($"#IOs: {op.NumIOExecutados()}"); // Retorna a quantidade de IOs geradas pela operação
        // Console.WriteLine($"#Tups: {op.NumTuplasGeradas()}"); // Retorna a quantidade de tuplas geradas pela operação

        // op.SalvarTuplasGeradas("selecao_vinho_ano_colheita_1990.csv"); // Retorna as tuplas geradas pela operação e salva em um csv

        Console.WriteLine(vinho)
    }
}

