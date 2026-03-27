using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dem_v2
{
    internal class Geografica
    {
        public static void AreaGeografica(int i, string input)
        {
            // obtengo latitud (paralelo al ecuador)

            // luego obtengo longitud (paralelo a greenwich)

        }

        public static int PuntoGeografico(int i, string input, List<int> ECC, out bool valid) // lo uso para socorro (grados y minutos)
        {
            int j = 0;
            List<int> PuntoGeo = new List<int>();
            List<int> same = new List<int>();
            List<int> fail = new List<int> { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 };
            string ventana;
            int mensajeInt;

            for (int k = 0; k < 100; k += 10)
            {
                ventana = input.Substring(i + k, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                PuntoGeo.Add(valor);  // aca obtengo el 1234567890 ahora debo aplicar "mascaras" / elimino las posiciones impares
                if (Decodificador.DxRx(input, i + k))
                {
                    same.Add(valor);
                }
            }

            EliminarPosicionesImpares(PuntoGeo);
            bool mismoContenido = !PuntoGeo.Except(same).Any() && !same.Except(PuntoGeo).Any();

            foreach (int vaal in PuntoGeo)
            {
                ECC.Add(vaal);
            }

            // Ahora con PuntoGeo puedo decodificar toda la data

            // Formato lindo para cada uno de los valores NE/NW/SE/SW
            // AGREGAR: si no cumple con subtring's lanzar error o desconocido

            string todos = string.Concat(PuntoGeo.Select(n => n.ToString()));
            string referencia = todos.Substring(0, 1); // posición 0
            string lat_g = todos.Substring(1, 2); // posiciones 1-2
            string lat_m = todos.Substring(3, 2); // posiciones 3-4
            string long_g = todos.Substring(5, 3); // posiciones 5-7
            string long_m = todos.Substring(8, 2); // posiciones 8-9

            switch (referencia)
            {
                case "0":
                    referencia = "NE";
                    break;
                case "1":
                    referencia = "NW";
                    break;
                case "2":
                    referencia = "SE";
                    break;
                case "3":
                    referencia = "SW";
                    break;
                default:
                    referencia = "??";
                    break;
            }

            if (mismoContenido)
            {
                //Console.WriteLine("Coordenadas DX/RX coinciden");
                //Console.WriteLine($"Ubicacion: {string.Join(" | ", PuntoGeo)}");
                Console.WriteLine($"Ubicacion: {referencia} - Latitud {lat_g}° {lat_m}' - Longitud {long_g}° {long_m}'");
                valid = true;
            }
            else
            {
                //Console.WriteLine("Coordenadas DX/RX NO coinciden");
                Console.WriteLine($"Ubicacion desconocida: {string.Join(" | ", fail)}");
                valid = false;
            }

            j = i + 100;
            return j;





        }

        public static void EliminarPosicionesImpares(List<int> lista)
        {
            // Recorrer de atrás hacia adelante para evitar problemas al eliminar
            for (int i = lista.Count - 1; i >= 0; i--)
            {
                if (i % 2 != 0) // Si la posición es impar
                {
                    lista.RemoveAt(i);
                }
            }
        }

        public static int UTC(int i, string input, List<int> ECC)
        {
            // obtengo hora UTC
            int j = 0;
            List<int> UTC = new List<int>();
            string ventana;
            int mensajeInt;

            for (int k = 0; k < 40; k += 10)
            {
                ventana = input.Substring(i + k, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                UTC.Add(valor);
            }
            EliminarPosicionesImpares(UTC);

            foreach (int val in UTC)
            {
                ECC.Add(val);
            }

            Console.WriteLine($"Hora UTC: {string.Join(" | ", UTC)}");
            j = i + 40;

            return j;
        }



    }
}