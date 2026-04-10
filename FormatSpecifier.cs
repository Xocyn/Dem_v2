using Dem_v2;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dem_v2
{
    internal static class FormatSpecifier
    {
        // necesito filtrar primero los valores que pueden quedar del phasing
        public static int Filtro(int f_msj, out int j)
        {
            j = 0; // Inicializar obligatoriamente

            if (PhasingSequence.TryCaracter(f_msj))
            {
                j = 0; // Mantener el While
                return 0;
            }
            else
            {
                Console.Write("Formato: "); Console.WriteLine(Formato(f_msj));
                j = 1; // Salir del while
                return f_msj;
            }
        }

        public static string Formato(int valor)
        {
            return valor switch
            {
                112 => "Socorro (112)",
                116 => "AllShips (116)",
                114 => "Llama a grupo de barcos (114)",
                120 => "LLamada Individual (120)",
                102 => "LLamada a Area Geografica (102)",
                123 => "Individual2 (123)",
                _ => "Valor no reconocido" // Caso por defecto
            };
        }

    }
}

