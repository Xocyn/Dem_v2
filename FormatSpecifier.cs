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
                // descartar
                Console.WriteLine("Valor descartado");
                j = 0; // Mantener el While
                return 0;
            }
            else
            {
                Console.WriteLine(Formato(f_msj));
                j = 1; // Salir del while
                return f_msj;
            }
        }

        public static string Formato(int valor)
        {
            return valor switch
            {
                112 => "Socorro",
                116 => "AllShips",
                114 => "Grupo",
                120 => "Individual",
                102 => "Geografica",
                123 => "Individual2",
                _ => "Valor no reconocido" // Caso por defecto
            };
        }

    }
}

