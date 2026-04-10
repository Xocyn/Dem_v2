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
        public static int AreaGeografica(int i, string input, List<int> ECC)
        {
            // obtengo latitud (paralelo al ecuador)
            // luego obtengo longitud (paralelo a greenwich)
            int j = 0;
            List<int> AreaGeo = new List<int>();
            List<int> same = new List<int>();
            List<int> fail = new List<int> { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 };
            string ventana;
            int mensajeInt;

            for (int k = 0; k < 100; k += 10)
            {
                ventana = input.Substring(i + k, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                AreaGeo.Add(valor);  // aca obtengo el 1234567890 ahora debo aplicar "mascaras" / elimino las posiciones impares
                if (Decodificador.DxRx(input, i + k))
                {
                    same.Add(valor);
                }
            }

            EliminarPosicionesImpares(AreaGeo);
            bool mismoContenido = !AreaGeo.Except(same).Any() && !same.Except(AreaGeo).Any();

            foreach (int vaal in AreaGeo)
            {
                ECC.Add(vaal);
            }

            // Ahora con AreaGeo puedo decodificar toda la data

            // Formato lindo para cada uno de los valores NE/NW/SE/SW
          
            //string todos = string.Concat(AreaGeo.Select(n => n.ToString()));
            string todos = string.Join("", AreaGeo.Select(x => x.ToString("D2")));

            if (todos.Length < 10)
            {
                Console.WriteLine($"Ubicacion desconocida: {string.Join(" | ", fail)}");
                return i + 100;
            }

            string referencia = todos.Substring(0, 1);
            string lat = todos.Substring(1, 2);
            string log = todos.Substring(3, 3);
            string delta_lat = todos.Substring(6, 2);
            string delta_log = todos.Substring(8, 2);

            int.TryParse(lat, out int latInt); int.TryParse(delta_lat, out int delta_latInt); int result = latInt+ delta_latInt;
            int.TryParse(log, out int logInt); int.TryParse(delta_log, out int delta_logInt); int result2 = logInt + delta_logInt;

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
                Console.WriteLine($"Ubicacion: {referencia} - Latitud {lat} .. {result} ° - Longitud {log} .. {result2} °");
            }
            else
            {
                Console.WriteLine($"Ubicacion desconocida: {string.Join(" | ", fail)}");
            }

            j = i + 100;
            return j;
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

            string todos = string.Join("", PuntoGeo.Select(x => x.ToString("D2")));

            //string todos = string.Concat(PuntoGeo.Select(n => n.ToString()));

            if (todos.Length < 10)
            {
                Console.WriteLine($"Ubicacion desconocida: {string.Join(" | ", fail)}");
                valid = false;
                return i + 100;
            }

            string referencia = todos.Substring(0, 1);
            string lat_g = todos.Substring(1, 2);
            string lat_m = todos.Substring(3, 2);
            string long_g = todos.Substring(5, 3);
            string long_m = todos.Substring(8, 2);

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

            string utc = string.Join(":", UTC.Select(x => x.ToString("D2")));

            Console.WriteLine($"Hora UTC: {utc}");
            j = i + 40;

            return j;
        }



    }
}