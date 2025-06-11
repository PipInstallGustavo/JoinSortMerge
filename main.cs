using System;

//namespaces
using Tabela;
using Tupla;
using Pagina;
using Operador;

// VOCÊ DEVE INCLUIR SEUS NAMESPACES AQUI, CASO NECESSÁRIO!!!

class Program
{
    static void Main(string[] args)
    {

        //o usuário provê o path do csv para o programa
        string tabela1 = Console.ReadLine();
        string tabela2 = Console.ReadLine();

        //o usuário tbm irá dizer qual coluna de cada tabela será usada para a operação
        string coluna1 = Console.ReadLine();
        string coluna2 = Console.ReadLine();

        Tabela.Tabela tabela_1 = new Tabela.Tabela(tabela1); // cria estrutura necessária para a tabela
        Tabela.Tabela tabela_2 = new Tabela.Tabela(tabela2);


        tabela_1.CarregarDados(); // lê os dados do csv e adiciona na estrutura da tabela, caso necessário
        tabela_2.CarregarDados();

        // IMPLEMENTE O OPERADOR E DEPOIS EXECUTE AQUI
        Operador.Operador op = new Operador.Operador(tabela_1, tabela_2, coluna1, coluna2, "saida.csv");
        // significa: SELECT * FROM Vinho V, Uva U WHERE V.vinho_id = U.uva_id
        // IMPORTANTE: isso é só um exemplo, podem ser tabelas/colunas distintas.
        // genericamente: Operador(tabela_1, tabela_2, col_tab_1, col_tab_2):
        // significa: SELECT * FROM tabela_1, tabela_2 WHERE col_tab_1 = col_tab_2

        op.Executar(); // Realiza a operação desejada

        Console.WriteLine($"#Pags: {op.NumPagsGeradas}");// Retorna a quantidade de tuplas geradas pela operação
        Console.WriteLine($"#IOs: {op.NumIOExecutados}"); // Retorna a quantidade de IOs geradas pela operação
        Console.WriteLine($"#Tups: {op.NumTuplasGeradas}");
        // op.SalvarTuplasGeradas("selecao_vinho_ano_colheita_1990.csv"); // Retorna as tuplas geradas pela operação e salva em um csv

        // Console.WriteLine(vinho);sGeradas}"); // Retorna a quantidade de páginas geradas pela operação
    }
}

