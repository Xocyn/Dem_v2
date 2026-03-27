using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dem_v2
{
    internal class Socorro
    {
        // El socorro es broadcast, no necesita el MMSI del receptor
        // la funcion me devuelve un int que representa cuantas posiciones debo avanzar luego de leer el MMSI
        public static int MMSI(int i, int form, string input, List<int> ECC)
        {
            int j = 0;
            List<int> MMSI = new List<int>();
            List<int> same = new List<int>();
            List<string> fail = new List<string> { "X", "X", "X", "X", "X", "X", "X", "X", "X" };

            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);

            if (form == valor) // es el primer format recibido
            {
                // decodifico el MMSI en base a esta posicion
                i = i + 40;
                for (int k = 0; k < 100; k += 10)
                {
                    ventana = input.Substring(i + k, 10);
                    mensajeInt = Convert.ToInt32(ventana, 2);
                    Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                    MMSI.Add(valor);
                    if (Decodificador.DxRx(input, i + k))
                    {
                        same.Add(valor);
                    }
                }
            }
            else // es el segundo format recibido
            {
                // decodifico el MMSI en base a esta posicion
                i = i + 20;
                for (int k = 0; k < 100; k += 10)
                {
                    ventana = input.Substring(i + k, 10);
                    mensajeInt = Convert.ToInt32(ventana, 2);
                    Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                    MMSI.Add(valor);
                    if (Decodificador.DxRx(input, i + k))
                    {
                        same.Add(valor);
                    }
                }
            }

            Geografica.EliminarPosicionesImpares(MMSI);
            bool mismoContenido = !MMSI.Except(same).Any() && !same.Except(MMSI).Any();

            foreach (int valorMMSI in MMSI)
            {
                ECC.Add(valorMMSI);
            }

            if (mismoContenido)
            {
                Console.WriteLine("MMSI DX/RX coinciden");
                Console.WriteLine($"MMSI: {string.Join(" | ", MMSI)}");

            }
            else
            {
                Console.WriteLine("MMSI DX/RX NO coinciden");
                Console.WriteLine($"MMSI desconocido: {string.Join(" | ", fail)}");

            }


            // tambien podria eleminar las posiciones que no me sirven o guardar estos valores en una lista

            //for (int w = 0; w < MMSI.Count; w += 2) // Incrementa de 2 en 2
            //    {
            //    Console.WriteLine($"{MMSI[w]}");
            //    }

            j = i + 100; // lo deveria dejar en la posicion del mensaje 1 (nature of distress)

            return j;

        }

        public static int FirstTelecommand(int i, string input, List<int> ECC)
        {
            int j = 0;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            ECC.Add(valor);
            switch (valor)
            {
                case 100:
                    Console.WriteLine("Comunicaciones siguientes: F3E/G3E ALL MODES TP");
                    break;
                case 101:
                    Console.WriteLine("Comunicaciones siguientes: F3E/G3E DUPLEX TP");
                    break;
                case 109:
                    Console.WriteLine("Comunicaciones siguientes: J3E TP");
                    break;
                case 113:
                    Console.WriteLine("Comunicaciones siguientes: F1B/J2B TTY-FEC");
                    break;
                case 115:
                    Console.WriteLine("Comunicaciones siguientes: F1B/J2B TTY-ARQ");
                    break;
                case 126:
                    Console.WriteLine("Comunicaciones siguientes: Sin información");
                    break;

            }
            j = i + 20; 
            return j;
        } 
        public static int NatureofDistress(int i, string input, List<int> ECC)
        {
            int j = 0;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            ECC.Add(valor);
            switch (valor)
                {
                    case 100: 
                        Console.WriteLine("Incendio/Explosión");
                        break;
                    case 101: 
                        Console.WriteLine("Inundación");
                        break;
                    case 102: 
                        Console.WriteLine("Colision");
                        break;
                    case 103: 
                        Console.WriteLine("Encallado");
                        break;
                    case 104: 
                        Console.WriteLine("Peligro de zozobra");
                        break;
                    case 105: 
                        Console.WriteLine("Naufragio");  
                        break;
                    case 106: 
                        Console.WriteLine("Deshabilitado y a la deriva");
                        break;
                    case 107: 
                        Console.WriteLine("Socorro sin designar");
                        break;
                    case 108: 
                        Console.WriteLine("Abandonando la nave");
                        break;
                    case 109: 
                        Console.WriteLine("Pirateria/Robo a mano armada");
                        break;
                    case 110: 
                        Console.WriteLine("Hombre al agua");
                        break;
                    case 112:
                        Console.WriteLine("EPIRB emitido");
                        break;
                    default:
                    // no hacer nada
                        break;
                }
            j = i + 20;
            return j;
        }

    }
}
