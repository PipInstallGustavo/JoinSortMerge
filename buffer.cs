using System.Collections.Generic;

namespace Buffer
{
    public class BufferPaginas
    {
        public const int Tamanho = 4;
        public List<Pagina.Pagina> Paginas { get; }

        public BufferPaginas()
        {
            Paginas = new List<Pagina.Pagina>(Tamanho);
            for (int i = 0; i < Tamanho; i++)
                Paginas.Add(new Pagina.Pagina());
        }


        public void Limpar()
        {
            foreach (var pagina in Paginas)
                pagina.Tuplas.Clear();
        }

                public bool EstaCheio()
        {
            return Paginas.Count >= Tamanho;
        }

        public void AdicionarPagina(Pagina.Pagina pagina)
        {
            if (Paginas.Count < Tamanho)
                Paginas.Add(pagina);
            else
                throw new System.InvalidOperationException("Buffer cheio.");
        }

        public void Resetar()
        {
            Paginas.Clear();
            for (int i = 0; i < Tamanho; i++)
                Paginas.Add(new Pagina.Pagina());
        }
        
        public Pagina.Pagina this[int idx] => Paginas[idx];
    }
}