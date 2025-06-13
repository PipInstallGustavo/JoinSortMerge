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

        /*  
            Exemplo de uso do programa:
CSVs/uva.csv
CSVs/vinho.csv
uva_id
vinho_id
        */
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
        // Salva o resultado da operação em um arquivo CSV chamado "saida.csv"
        Operador.Operador op = new Operador.Operador(tabela_1, tabela_2, coluna1, coluna2, "saida.csv");
        // significa: SELECT * FROM Vinho V, Uva U WHERE V.vinho_id = U.uva_id
        // IMPORTANTE: isso é só um exemplo, podem ser tabelas/colunas distintas.
        // genericamente: Operador(tabela_1, tabela_2, col_tab_1, col_tab_2):
        // significa: SELECT * FROM tabela_1, tabela_2 WHERE col_tab_1 = col_tab_2

        op.Executar(); // Realiza a operação desejada

        Console.WriteLine($"#Pags: {op.NumPagsGeradas}"); // Retorna a quantidade de páginas geradas pela operação
        Console.WriteLine($"#IOs: {op.NumIOExecutados}"); // Retorna a quantidade de IOs geradas pela operação
        Console.WriteLine($"#Tups: {op.NumTuplasGeradas}");// Retorna a quantidade de tuplas geradas pela operação
        

         // Pergunta ao usuário se deseja excluir a pasta de arquivos temporários
        Console.WriteLine("Deseja excluir a pasta dos arquivos temporários 'CSVtmp'? (s/n)");
        string resposta = Console.ReadLine();
        if (resposta.Trim().ToLower() == "s")
        {
            try
            {
                if (System.IO.Directory.Exists("CSVtmp"))
                {
                    System.IO.Directory.Delete("CSVtmp", true);
                    Console.WriteLine("Pasta 'CSVtmp' excluída com sucesso.");
                }
                else
                {
                    Console.WriteLine("A pasta 'CSVtmp' não existe.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao excluir a pasta: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("A pasta 'CSVtmp' foi mantida.");
        }
    }
}

