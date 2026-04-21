using System;
using System.Collections.Generic;
using System.Text;

namespace Dem_v2
{
    internal class Procesamiento
    {
        public static void Procesar(string input, bool ext)
        {
            List<(int Index, int Value)> encontrados = new List<(int, int)>();
            int i = 0;
            byte h = 1;
            bool sincronizado = false;

            // Ventana deslizante: busca y consume caracteres de phasing
            while (!sincronizado)
            {
                if (i + 10 > input.Length) break;

                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);

                if (Decodificador.TryDecodificarMensaje(mensajeInt, out int valor))
                {
                    if (PhasingSequence.TryCaracter(valor))
                    {
                        encontrados.Add((i, valor));
                        i += 10;

                        if (encontrados.Count >= 3 &&
                            PhasingSequence.TryDetect(encontrados, out var pattern))
                        {
                            sincronizado = true;
                        }
                    }
                    else
                        i += 1;
                }
                else
                    i += 1;
            }

            // ── Format specifier ─────────────────────────────────────────────────────
            bool formatConfirmed = false;
            bool dxrxConfirmed = false;
            int form = 0;

            while (sincronizado && !formatConfirmed)
            {
                if (i + 10 > input.Length) break;

                string ventana = input.Substring(i, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);

                form = FormatSpecifier.Filtro2(valor, out int j);

                bool esBroadcast = (form == 112 || form == 116);
                dxrxConfirmed = esBroadcast || Decodificador.DxRx(input, i);

                i += 10;

                if (j == 1 && dxrxConfirmed)
                {
                    formatConfirmed = true;
                }
            }

            i -= 10; // Retroceder para que el switch lea el format specifier

            // ── Mensaje ─────────────────────────────────────────────────────

            Decodificador.Mensaje(input, i, out List<int> MESSAGE);

            List<int> MENSAJE = MESSAGE.ToList();
            List<int> ECC = MESSAGE.ToList();

            string mensaje_string = string.Join(" ", MENSAJE.Select(x => x.ToString("D2")));
            Console.Write("MENSAJE: ");
            Console.WriteLine(mensaje_string);

            Geografica.EliminarPosicionesImpares(ECC); // Obtengo los DX
            ECC = PrepararECC(ECC);
            //string ecc_string = string.Join(" ", ECC.Select(x => x.ToString("D2")));
            //Console.Write("Para calculo de ECC :");
            //Console.WriteLine(ecc_string);

            if (VerificarECC(MENSAJE, ECC))
            {
                Console.WriteLine("ECC correcto");
            }
            else
            {
                Console.WriteLine("Error en ECC"); // SI FALLA ECC TERMINA EL PROCESAMIENTO
                return;
            }

            switch (MENSAJE[0])
            {
                case 102:
                    //MetodoGeografica();
                    break;
                case 112:
                    Metodos.MSocorro(MENSAJE);
                    break;
                case 114:
                    //MetodoGrupos();
                    break;
                case 116:
                    //MetodoAllShips();
                    break;
                case 120:
                    //MetodoIndividual();
                    break;
                case 123:
                    //MetodoAutomatico(); //ESTE LO TENGO QUE DESARROLLAR ???
                    break;
                default:
                    break;
            }

        }


        public static bool VerificarECC(List<int> MESSAGE, List<int> ECC)
        {
            // El ECC recibido está en dos posiciones de MENSAJE:
            //   posición Count-6  → copia Dx (primer EOS omitido, luego ECC)
            //   posición Count-1  → último caracter (copia Rx)
            if (MESSAGE.Count < 6)
            {
                Console.WriteLine("MENSAJE demasiado corto para leer ECC.");
                return false;
            }

            int eccDx = MESSAGE[MESSAGE.Count - 6]; // 6 posiciones antes del final
            int eccRx = MESSAGE[MESSAGE.Count - 1]; // último caracter

            // Calcular ECC local (XOR acumulativo, 7 bits)
            int calculated = 0;
            foreach (int v in ECC)
                calculated ^= v;
            calculated &= 0x7F;

            // Comparar contra cualquiera de los recibidos 
            if (calculated == eccDx || calculated == eccRx)
                return true;
            else
                return false;
        }
        public static List<int> PrepararECC(List<int> list)
        {
            if (list.Count < 4)
            {
                Console.WriteLine("MENSAJE demasiado corto para preparar ECC.");
                return new List<int>();
            }

            // Eliminar el primer elemento y los últimos 2
            List<int> ecc = list
                .Skip(1)                        // elimina el primero
                .Take(list.Count - 4)        // elimina los últimos 2 (y el primero ya saltado)
                .ToList();

            return ecc;
        }
    }

    internal class Metodos
    {
        public static void MGeografica()
        {
            Console.WriteLine("Procesando mensaje de tipo Geográfica...");
            // Implementar lógica específica para mensajes de tipo Geográfica
        }

        public static void MSocorro(List<int> mensaje)
        {
            int format = 0;
            string mmsi = string.Empty;
            int tipoEmergencia = 0;
            List<int> coords= new List<int>();
            bool sigoutc = false;
            string utc = string.Empty;
            int sig_comunicaciones = 0;
            // Segun la norma debo recibir 2 veces el caracter de formato para evitar falsas alarmas
            if (mensaje[0] == mensaje[2])
                format = mensaje[0];
            else 
                return;
        
            mmsi = General.newMMSI(mensaje, 4); // El MMSI empieza en la posición 4 del mensaje (después de los 4 caracteres de encabezado)
            // Luego de 10 caracteres que conienten la informacion del MMSI, el mensaje de socorro tiene un caracter que indica el tipo de emergencia (posicion 14 del mensaje)
            tipoEmergencia = mensaje[14];
            
            (coords, sigoutc) = Geografica.Coordenadas(mensaje, 16); // 16 + 10

            if(sigoutc)
            {
                utc = Geografica.newUTC(mensaje, 26);
            }
            else
            {
                utc = "88:88"; // Manejar error o coordenadas inválidas
            }

            // 30 comunicaciones siguiente
            sig_comunicaciones = mensaje[30];
            
            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(format)}");
            Console.WriteLine($"MMSI: {mmsi}");
            Console.WriteLine($"Tipo de Emergencia: {Socorro.Peligro(tipoEmergencia)}");
            Console.WriteLine($"Coordenadas: {Geografica.Posicion(coords)}"); // DESARROLLAR METODO PARA POSICIONES
            Console.WriteLine($"UTC: {utc}");
            Console.WriteLine($"Siguiente Comunicación: {Socorro.PosteriorCom(sig_comunicaciones)}");

        }

        public static void MGrupos()
        {
            Console.WriteLine("Procesando mensaje de tipo Grupos...");
            // Implementar lógica específica para mensajes de tipo Grupos
        }
        public static void MAllShips()
        {
            Console.WriteLine("Procesando mensaje de tipo All Ships...");
            // Implementar lógica específica para mensajes de tipo All Ships
        }
    }
}
