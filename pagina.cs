using System.Collections.Generic;
using System.IO; 
//classes
using Tupla;

namespace Pagina {
    // Representa uma página, que contém um número fixo de tuplas 
    public class Pagina
    {
        // Lista de tuplas na página 
        public List<Tupla.Tupla> Tuplas { get; set; }
        // Número máximo de tuplas por página 
        public const int MaxTuplasPorPagina = 10;
        // Quantidade de tuplas atualmente ocupadas na página 
        public int QtdTuplasOcup { get { return Tuplas.Count; } }

        public Pagina()
        {
            Tuplas = new List<Tupla.Tupla>(MaxTuplasPorPagina);
        }

        public bool AdicionarTupla(Tupla.Tupla tupla)
        {
            if (QtdTuplasOcup < MaxTuplasPorPagina)
            {
                Tuplas.Add(tupla);
                return true;
            }
            return false; // Página cheia
        }
    }
}
