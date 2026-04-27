using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dem_v2
{
    internal class Geografica
    {
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

        public static (List<int>, bool) Coordenadas(List<int> mensaje, int i)
        {
            bool mismoContenido = true;
            List<int> CORDENADAS = new List<int>();
            List<int> FAIL = new List<int> { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 };

            for (int k = i; k < i + 10; k += 1)
            {
                if (k % 2 == 0)  // Verifica si k es par
                {
                    int valor = mensaje[k];

                    if (valor == mensaje[k + 5])
                        CORDENADAS.Add(valor);
                    else
                        mismoContenido = false;
                }
            }

            if (mismoContenido)
                return (CORDENADAS, true);
            else
                return (FAIL, false);

        }

        public static string newUTC(List<int> mensaje, int i)
        {
            string utc = string.Empty;
            List<int> UTC = new List<int>();

            for (int k = i; k < i + 4; k += 1)
            {
                if (k % 2 == 0)  // Verifica si k es par
                {
                    int valor = mensaje[k];
                    UTC.Add(valor);
                }
            }

            utc = string.Join(":", UTC.Select(x => x.ToString("D2")));

            return utc;
        }

        public static string Posicion(List<int> coords)
        {
            string s = string.Empty;

            if (coords.Count == 5) // Pos1
            {
                string todos = string.Join("", coords.Select(x => x.ToString("D2")));
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

                s = $"{referencia} - Latitud {lat_g}° {lat_m}' - Longitud {long_g}° {long_m}'";
                return s;
            }

            if (coords[0] == 126) //Pos4 (ACK denegado)
            {
                s = $"Petición de Posición denegada";
            }
            else // Pos4 
            {
                string todos = string.Join("", coords.Select(x => x.ToString("D2")));
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

                s = $"{referencia} - Latitud {lat_g}° {lat_m}' - Longitud {long_g}° {long_m}'";
                return s;
            }

            if (coords[0] ==55)
            {
                s = $"Ubicacion desconocida";
            }

            return s;
        }

        public static string Area(List<int> area, int i)
        {
            string area_string = string.Empty;
            List<int> coords = new List<int>();


            for (int k = i; k < i + 10; k += 1)
            {
                if (k % 2 == 0)  // Verifica si k es par
                {
                    coords.Add(area[k]); // Solo agrego los DX
                }
            }

            if (coords.Count == 5)
            {
                string todos = string.Join("", coords.Select(x => x.ToString("D2")));
                string referencia = todos.Substring(0, 1);
                string lat = todos.Substring(1, 2);
                string log = todos.Substring(3, 3);
                string delta_lat = todos.Substring(6, 2);
                string delta_log = todos.Substring(8, 2);
                int.TryParse(lat, out int latInt); int.TryParse(delta_lat, out int delta_latInt); int result = latInt + delta_latInt;
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
                area_string = $"{referencia} - Latitud {lat} .. {result} ° - Longitud {log} .. {result2} °";
                return area_string;
            }
            return area_string;

        }
    }
}