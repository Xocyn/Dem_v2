using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dem_v2
{
    internal class Procesamiento
    {
        /// <summary>
        /// Muestra un menú de confirmación para responder a un mensaje de socorro
        /// </summary>
        /// <returns>true si el usuario selecciona Y, false si selecciona N</returns>
        public static bool MostrarMenuSocorro()
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════=══╗");
                Console.WriteLine("║  ¿Desea responder el mensaje de S.O.S?  ║");
                Console.WriteLine("╚═══════════════════════════════════════=═╝");
                Console.Write("Ingrese (Y/N): ");

                string input = Console.ReadLine()?.ToUpper().Trim();

                if (input == "Y" || input == "y")
                {
                    Console.WriteLine("Respondiendo mensaje de socorro...");
                    return true;
                }
                else if (input == "N" || input == "n")
                {
                    Console.WriteLine("Mensaje de socorro ignorado.");
                    return false;
                }
                else
                {
                    Console.WriteLine("Entrada inválida. Por favor ingrese Y o N.");
                }
            }
        }

        public static void Procesar(string input, bool ext)
        {
            List<(int Index, int Value)> encontrados = new List<(int, int)>();
            int i = 0;
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
            List<int> datos_respuesta = new List<int>();

            string mensaje_string = string.Join(" ", MENSAJE.Select(x => x.ToString("D2")));
            Console.Write("MENSAJE: ");
            Console.WriteLine(mensaje_string);

            Geografica.EliminarPosicionesImpares(ECC); // Obtengo los DX

            ECC = PrepararECC(ECC); // ESTO NO ME VA A SERVIR PARA LAS EXPANSIONES

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
                    Metodos.MGeografica(MENSAJE);
                    break;
                case 112:
                    datos_respuesta = Metodos.MSocorro(MENSAJE);
                    // Mostrar menú de confirmación para responder al S.O.
                    if (MostrarMenuSocorro())
                    {
                        Respuesta.RespuestaSocorro(datos_respuesta);
                    }
                    break;
                case 114:
                    Metodos.MGrupos(MENSAJE);
                    break;
                case 116:
                    Metodos.MAllShips(MENSAJE);
                    break;
                case 120:
                    Metodos.MIndividual(MENSAJE);
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
        // ── GEOGRAFICA─────────────────────────────────────────────────────
        public static void MGeografica(List<int> mensaje) // SOLO MF/HF !!!
        {
            string mmsi = string.Empty;
            string area = string.Empty;
            string categoria = string.Empty;
            string primer_tel = string.Empty, segundo_tel = string.Empty; 
            string frec_canal_1 = string.Empty, frec_canal_2 = string.Empty;
            bool ocho = false, ocho2 = false;
            bool canal = false, canal2 = false;
            string ack = string.Empty;
            string mmsi_socorro = string.Empty;
            string tipoEmergencia = string.Empty;
            string coordenadas = string.Empty;
            string utc = string.Empty;
            string sig_comunicaciones = string.Empty;

            area = Geografica.Area(mensaje,4); // El área geográfica empieza en la posición 4 del mensaje (después de los 4 caracteres de encabezado)
            categoria = General.Categoria(mensaje[14]); // Luego de 10 caracteres que contienen la informacion del área geográfica (posicion 14 del mensaje)
            mmsi = General.newMMSI(mensaje, 16); // El MMSI empieza en la posición 16 del mensaje (después de los 14 caracteres de encabezado)
            primer_tel = General.PrimerTelemando(mensaje[26], out bool sol_posicion); // Luego de 10 caracteres que contienen la informacion del MMSI (posicion 26 del mensaje)
            if (mensaje[14] == 112)
            {
                mmsi_socorro = General.newMMSI(mensaje, 28);
                tipoEmergencia = Socorro.Peligro(mensaje[38]);
                coordenadas = Geografica.Posicion(Geografica.Coordenadas(mensaje, 40).Item1);
                if (Geografica.Coordenadas(mensaje, 40).Item2)
                {
                    utc = Geografica.newUTC(mensaje, 50);
                }
                else
                {
                    utc = "88:88"; // Manejar error o coordenadas inválidas
                }
                sig_comunicaciones = Socorro.PosteriorCom(mensaje[54]);
                ack = General.ACK(mensaje[56]);
            }
            else
            {
                segundo_tel = General.SegundoTelemando(mensaje[28]); // Luego de 2 caracteres que contienen la informacion del primer telemando (posicion 28 del mensaje)
                (frec_canal_1, ocho, canal) = General.FrecuenciaCanal(mensaje, 30, out bool _); // Luego de 2 caracteres que contienen la informacion del segundo telemando (posicion 30 del mensaje)
                if (ocho)
                {
                    (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 38, out bool _); // Luego de 8 caracteres que contienen la informacion del primer canal o frecuencia (posicion 38 del mensaje)
                    ack = General.ACK(mensaje[44]);
                }
                else
                {
                    (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 36, out bool _); // Luego de 6 caracteres que contienen la informacion del primer canal o frecuencia (posicion 32 del mensaje)
                    ack = General.ACK(mensaje[42]);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(mensaje[0])}");
            Console.WriteLine($"Área Geográfica: {area}");
            Console.WriteLine($"Categoría: {categoria}");
            Console.WriteLine($"MMSI: {mmsi}");
            Console.WriteLine($"Primer Telemando: {primer_tel}");
            if (mensaje[14] == 112) // Socorro
            {
                Console.WriteLine($"MMSI Socorro: {mmsi_socorro}");
                Console.WriteLine($"Tipo de emergencia: {tipoEmergencia}");
                Console.WriteLine($"Coordenadas: {coordenadas}");
                Console.WriteLine($"UTC: {utc}");
                Console.WriteLine($"Siguiente Comunicación: {sig_comunicaciones}");
                Console.WriteLine(ack);
            }
            else
            {
                Console.WriteLine($"Segundo Telemando: {segundo_tel}");
                if (canal)
                    Console.WriteLine($"Canal Rx: {frec_canal_1}");
                else
                    Console.WriteLine($"Frecuencia Rx: {frec_canal_1}");
                if (canal2)
                    Console.WriteLine($"Canal Tx: {frec_canal_2}");
                else
                    Console.WriteLine($"Frecuencia Tx: {frec_canal_2}");
                Console.WriteLine($"ACK: {ack}");
            }
        }
        // ── INDIVIDUAL────────────────────────────────────────────────────
        public static void MIndividual(List<int> mensaje)
        {
            int format = mensaje[0];
            string mmsi_receptor, mmsi_transmisor = string.Empty;
            string categoria = string.Empty;
            string primer_tel, segundo_tel = string.Empty;
            string frec_canal_1 = string.Empty, frec_canal_2 = string.Empty;
            bool ocho = false, ocho2 = false;
            bool canal = false, canal2 = false;
            List<int> posicion = new List<int>();
            string utc = string.Empty;
            string posicion_string = string.Empty;
            string ack = string.Empty;
            bool Posicion_2 = false;
            string mmsi_socorro = string.Empty;
            string tipoEmergencia = string.Empty;
            string coordenadas = string.Empty;
            string sig_comunicaciones = string.Empty;

            mmsi_receptor = General.newMMSI(mensaje, 4); // El MMSI del receptor empieza en la posición 4 del mensaje (después de los 4 caracteres de encabezado)
            categoria = General.Categoria(mensaje[14]); // Luego de 10 caracteres que contienen la informacion del MMSI del receptor (posicion 14 del mensaje
            mmsi_transmisor = General.newMMSI(mensaje, 16); // El MMSI del transmisor empieza en la posición 16 del mensaje (después de los 14 caracteres de encabezado)
            primer_tel = General.PrimerTelemando(mensaje[26], out bool sol_posicion); // Luego de 10 caracteres que contienen la informacion del MMSI del transmisor (posicion 26 del mensaje)
            if (mensaje[14] == 112)
            {
                mmsi_socorro = General.newMMSI(mensaje, 28);
                tipoEmergencia = Socorro.Peligro(mensaje[38]);
                coordenadas = Geografica.Posicion(Geografica.Coordenadas(mensaje, 40).Item1);
                if (Geografica.Coordenadas(mensaje, 40).Item2)
                {
                    utc = Geografica.newUTC(mensaje, 50);
                }
                else
                {
                    utc = "88:88"; // Manejar error o coordenadas inválidas
                }
                sig_comunicaciones = Socorro.PosteriorCom(mensaje[54]);
                ack = General.ACK(mensaje[56]);
            }
            else
            {
                segundo_tel = General.SegundoTelemando(mensaje[28]); // Luego de 2 caracteres que contienen la informacion del primer telemando (posicion 28 del mensaje)
                if (sol_posicion)
                {
                    for (int k = 30; k < 44; k += 1)
                    {
                        if (k % 2 == 0)  // Verifica si k es par
                        {
                            posicion.Add(mensaje[k]); // Solo agrego los DX
                        }
                    }
                    utc = Geografica.newUTC(mensaje, 42);
                    ack = General.ACK(mensaje[46]);
                }
                else
                {
                    (frec_canal_1, ocho, canal) = General.FrecuenciaCanal(mensaje, 30, out bool pos2); // Luego de 2 caracteres que contienen la informacion del segundo telemando (posicion 30 del mensaje
                    if (ocho)
                    {
                        (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 38, out bool _); // Luego de 8 caracteres que contienen la informacion del primer canal o frecuencia (posicion 38 del mensaje)
                        ack = General.ACK(mensaje[44]);
                    }
                    else if (pos2) // Caso Pos2
                    {
                        ack = General.ACK(mensaje[42]);
                        Posicion_2 = true;
                    }
                    else
                    {
                        (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 36, out bool _); // Luego de 6 caracteres que contienen la informacion del primer canal o frecuencia (posicion 32 del mensaje)
                        ack = General.ACK(mensaje[42]);
                    }

                }
            }

            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(format)}");
            Console.WriteLine($"MMSI Receptor: {mmsi_receptor}");
            Console.WriteLine($"Categoría: {categoria}");
            Console.WriteLine($"MMSI Transmisor: {mmsi_transmisor}");
            Console.WriteLine($"Primer Telemando: {primer_tel}");
            if (mensaje[14] == 112) // Socorro
            {
                Console.WriteLine($"MMSI Socorro: {mmsi_socorro}");
                Console.WriteLine($"Tipo de emergencia: {tipoEmergencia}");
                Console.WriteLine($"Coordenadas: {coordenadas}");
                Console.WriteLine($"UTC: {utc}");
                Console.WriteLine($"Siguiente Comunicación: {sig_comunicaciones}");
                Console.WriteLine(ack);
            }
            else
            {
                Console.WriteLine($"Segundo Telemando: {segundo_tel}");
                if (posicion.Count > 0)
                {
                    if (posicion[posicion.Count - 1] == 117) // Solicitud de posisción
                    {
                        posicion_string = "Solicitud de posición";
                        Console.WriteLine($"Posición: {posicion_string}");
                    }
                    else
                    {
                        posicion_string = Geografica.Posicion(posicion);
                        Console.WriteLine($"Posición: {posicion_string}");
                        Console.WriteLine($"UTC: {utc}");
                    }
                }
                else if (Posicion_2)
                {
                    Console.WriteLine($"Posición: {frec_canal_1}");
                }
                else
                {
                    if (canal)
                        Console.WriteLine($"Canal Rx: {frec_canal_1}");
                    else
                        Console.WriteLine($"Frecuencia Rx: {frec_canal_1}");
                    if (canal2)
                        Console.WriteLine($"Canal Tx: {frec_canal_2}");
                    else
                        Console.WriteLine($"Frecuencia Tx: {frec_canal_2}");
                }
                Console.WriteLine(ack);
            }
        }
        // ── SOCORRO ─────────────────────────────────────────────────────
        public static List<int> MSocorro(List<int> mensaje)
        {
            int format = 0;
            string mmsi = string.Empty;
            string tipoEmergencia = string.Empty;
            List<int> coords= new List<int>();
            bool sigoutc = false;
            string utc = string.Empty;
            int sig_comunicaciones = 0;
            string ack = string.Empty;

            // Segun la norma debo recibir 2 veces el caracter de formato para evitar falsas alarmas
            if (mensaje[0] == mensaje[2])
                format = mensaje[0];
            else 
                return new List<int>();
        
            mmsi = General.newMMSI(mensaje, 4); // El MMSI empieza en la posición 4 del mensaje (después de los 4 caracteres de encabezado)
            // Luego de 10 caracteres que conienten la informacion del MMSI, el mensaje de socorro tiene un caracter que indica el tipo de emergencia (posicion 14 del mensaje)
            tipoEmergencia = Socorro.Peligro(mensaje[14]);
            
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
            ack = General.ACK(mensaje[32]);

            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(format)}");
            Console.WriteLine($"MMSI: {mmsi}");
            Console.WriteLine($"Tipo de Emergencia: {tipoEmergencia}");
            Console.WriteLine($"Coordenadas: {Geografica.Posicion(coords)}"); // DESARROLLAR METODO PARA POSICIONES
            Console.WriteLine($"UTC: {utc}");
            Console.WriteLine($"Siguiente Comunicación: {Socorro.PosteriorCom(sig_comunicaciones)}");
            Console.WriteLine(ack);

            List<int> respuesta = mensaje.GetRange(4,28);
            return respuesta;
        }
        // ── GRUPOS ─────────────────────────────────────────────────────
        public static void MGrupos(List<int> mensaje)
        {
            int format = mensaje[0];
            string mmsi = string.Empty;
            string categoria = string.Empty;
            string mmsi_tx = string.Empty;
            string primer_tel = string.Empty;
            string segundo_tel = string.Empty;
            string frec_canal_1 = string.Empty;
            string ack = string.Empty;
            string mmsi_socorro = string.Empty;
            string tipoEmergencia = string.Empty;
            string coordenadas = string.Empty;
            string utc = string.Empty;
            string sig_comunicaciones = string.Empty;

            mmsi = General.newMMSI(mensaje, 4); 
            categoria = General.Categoria(mensaje[14]);
            mmsi_tx = General.newMMSI(mensaje, 16);
            primer_tel = General.PrimerTelemando(mensaje[26], out bool sol_posicion);
            if (mensaje[14] == 112) // Socorro
            {
                mmsi_socorro = General.newMMSI(mensaje, 28); 
                tipoEmergencia = Socorro.Peligro(mensaje[38]);
                coordenadas = Geografica.Posicion(Geografica.Coordenadas(mensaje, 40).Item1);
                if (Geografica.Coordenadas(mensaje, 40).Item2)
                {
                    utc = Geografica.newUTC(mensaje, 50);
                }
                else
                {
                    utc = "88:88"; // Manejar error o coordenadas inválidas
                }
                sig_comunicaciones = Socorro.PosteriorCom(mensaje[54]);
                ack = General.ACK(mensaje[56]);
            }
            else
            {
                segundo_tel = General.SegundoTelemando(mensaje[28]);
                frec_canal_1 = General.FrecuenciaCanal(mensaje, 30, out bool _).Item1;
                ack = General.ACK(mensaje[42]);
            }

            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(format)}");
            Console.WriteLine($"MMSI: {mmsi}");
            Console.WriteLine($"Categoría: {categoria}");
            Console.WriteLine($"MMSI Transmisor: {mmsi_tx}");
            Console.WriteLine($"Primer Telemando: {primer_tel}");
            if (mensaje[14] == 112)
            {
                Console.WriteLine($"MMSI Socorro: {mmsi_socorro}");
                Console.WriteLine($"Tipo de emergencia: {tipoEmergencia}");
                Console.WriteLine($"Coordenadas: {coordenadas}");
                Console.WriteLine($"UTC: {utc}");
                Console.WriteLine($"Siguiente Comunicación: {sig_comunicaciones}");
                Console.WriteLine(ack);
            }
            else
            {
                Console.WriteLine($"Segundo Telemando: {segundo_tel}");
                Console.WriteLine($"Frecuencia: {frec_canal_1}");
                Console.WriteLine(ack);
            }
        }
        // ── ALL SHIPS─────────────────────────────────────────────────────
        public static void MAllShips(List<int> mensaje)
        {
            int format = mensaje[0];
            string mmsi = string.Empty;
            string categoria = string.Empty;
            string primer_tel = string.Empty;
            string segundo_tel = string.Empty;
            string frec_canal_1 = string.Empty;
            string frec_canal_2 = string.Empty;
            bool ocho = false; bool ocho2 = false;
            bool canal = false; bool canal2 = false;
            string ack = string.Empty;
            string mmsi_socorro = string.Empty;
            string tipoEmergencia = string.Empty;
            string coordenadas = string.Empty; 
            string utc = string.Empty;
            string sig_comunicaciones = string.Empty;

            categoria = General.Categoria(mensaje[4]);
            mmsi = General.newMMSI(mensaje, 6); // El MMSI empieza en la posición 6 del mensaje (después de los 4 caracteres de encabezado)
            // Luego de 10 caracteres que contienen la informacion del MMSI (posicion 16 del mensaje)
            primer_tel= General.PrimerTelemando (mensaje[16], out bool sol_posicion);
            if (mensaje[4]==112)
            {
                mmsi_socorro = General.newMMSI(mensaje, 18); 
                tipoEmergencia = Socorro.Peligro(mensaje[28]);
                coordenadas = Geografica.Posicion(Geografica.Coordenadas(mensaje, 30).Item1);
                if (Geografica.Coordenadas(mensaje, 30).Item2)
                {
                    utc = Geografica.newUTC(mensaje, 40);
                }
                else
                {
                    utc = "88:88"; // Manejar error o coordenadas inválidas
                }
                sig_comunicaciones = Socorro.PosteriorCom(mensaje[44]);
                ack = General.ACK(mensaje[46]);
            }
            else
            {
                segundo_tel = General.SegundoTelemando(mensaje[18]);
                (frec_canal_1, ocho, canal) = General.FrecuenciaCanal(mensaje, 20, out bool _);
                if (ocho)
                {
                    (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 28, out bool _);
                    ack = General.ACK(mensaje[34]);
                }
                else
                {
                    (frec_canal_2, ocho2, canal2) = General.FrecuenciaCanal(mensaje, 26, out bool _);
                    ack = General.ACK(mensaje[32]);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Formato: {FormatSpecifier.Formato(format)}");
            Console.WriteLine($"MMSI: {mmsi}");
            Console.WriteLine($"Categoría: {categoria}");
            Console.WriteLine($"Primer Telemando: {primer_tel}");
            if (mensaje[4] == 112)
            {
                Console.WriteLine($"MMSI Socorro: {mmsi_socorro}");
                Console.WriteLine($"Tipo de emergencia: {tipoEmergencia}");
                Console.WriteLine($"Coordenadas: {coordenadas}"); 
                Console.WriteLine($"UTC: {utc}");
                Console.WriteLine($"Siguiente Comunicación: {sig_comunicaciones}");
                Console.WriteLine(ack);
            }
            else
            {
                Console.WriteLine($"Segundo Telemando: {segundo_tel}");
                if (canal)
                    Console.WriteLine($"Canal Rx: {frec_canal_1}");
                else
                    Console.WriteLine($"Frecuencia Rx: {frec_canal_1}");
                if (canal2)
                    Console.WriteLine($"Canal Tx: {frec_canal_2}");
                else
                    Console.WriteLine($"Frecuencia Tx: {frec_canal_2}");
                Console.WriteLine(ack);
            }
        }
        
    }
}
