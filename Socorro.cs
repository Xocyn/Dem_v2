using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dem_v2
{
    internal class Socorro
    {
        // Helper: lee 10 bits desde input[i], devuelve false si no hay suficientes
        public static bool TryLeer(string input, int i, out int valor)
        {
            valor = 0;
            if (i + 10 > input.Length) return false;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
            return true;
        }

        public static int MMSI(int i, int form, string input, List<int> ECC)
        {
            int j = 0;
            List<int> MMSI = new List<int>();
            List<int> same = new List<int>();
            List<string> fail = new List<string> { "X", "X", "X", "X", "X", "X", "X", "X", "X" };

            if (!TryLeer(input, i + 20, out int valor))
            {
                Console.WriteLine("MMSI: stream corto (format check).");
                return i + 120;
            }

            if (form == valor) // es el primer format recibido
                i = i + 40;
            else
                i = i + 20;

            for (int k = 0; k < 100; k += 10)
            {
                if (!TryLeer(input, i + k, out valor)) break;
                int mensajeInt = Convert.ToInt32(input.Substring(i + k, 10), 2);
                MMSI.Add(valor);
                if (Decodificador.DxRx(input, i + k))
                    same.Add(valor);
            }

            Geografica.EliminarPosicionesImpares(MMSI);
            bool mismoContenido = !MMSI.Except(same).Any() && !same.Except(MMSI).Any();

            foreach (int valorMMSI in MMSI)
                ECC.Add(valorMMSI);

            string mm = string.Join("",MMSI.Select(x => x.ToString("D2")));

            if (mismoContenido)
            {
                //Console.WriteLine("MMSI DX/RX coinciden");
                Console.WriteLine($"MMSI: {mm}");
            }
            else
            {
                //Console.WriteLine("MMSI DX/RX NO coinciden");
                Console.WriteLine($"MMSI desconocido: {string.Join(" | ", fail)}");
            }

            j = i + 100;
            return j;
        }

        public static int FirstTelecommand(int i, string input, List<int> ECC)
        {
            if (!TryLeer(input, i, out int valor))
            {
                Console.WriteLine("FirstTelecommand: stream corto.");
                return i + 20;
            }
            ECC.Add(valor);
            switch (valor)
            {
                case 100: Console.WriteLine("Comunicaciones siguientes: F3E/G3E ALL MODES TP"); break;
                case 101: Console.WriteLine("Comunicaciones siguientes: F3E/G3E DUPLEX TP"); break;
                case 109: Console.WriteLine("Comunicaciones siguientes: J3E TP"); break;
                case 113: Console.WriteLine("Comunicaciones siguientes: F1B/J2B TTY-FEC"); break;
                case 115: Console.WriteLine("Comunicaciones siguientes: F1B/J2B TTY-ARQ"); break;
                case 126: Console.WriteLine("Comunicaciones siguientes: Sin información"); break;
            }
            return i + 20;
        }

        public static int NatureofDistress(int i, string input, List<int> ECC)
        {
            if (!TryLeer(input, i, out int valor))
            {
                Console.WriteLine("NatureofDistress: stream corto.");
                return i + 20;
            }
            ECC.Add(valor);
            switch (valor)
            {
                case 100: Console.WriteLine("Incendio/Explosión"); break;
                case 101: Console.WriteLine("Inundación"); break;
                case 102: Console.WriteLine("Colision"); break;
                case 103: Console.WriteLine("Encallado"); break;
                case 104: Console.WriteLine("Peligro de zozobra"); break;
                case 105: Console.WriteLine("Naufragio"); break;
                case 106: Console.WriteLine("Deshabilitado y a la deriva"); break;
                case 107: Console.WriteLine("Socorro sin designar"); break;
                case 108: Console.WriteLine("Abandonando la nave"); break;
                case 109: Console.WriteLine("Pirateria/Robo a mano armada"); break;
                case 110: Console.WriteLine("Hombre al agua"); break;
                case 112: Console.WriteLine("EPIRB emitido"); break;
                default: break;
            }
            return i + 20;
        }

        public static string Peligro(int valor)
        {
            return valor switch
            {
                100 => "Incendio/Explosión",
                101 => "Inundación",
                102 => "Colision",
                103 => "Encallado",
                104 => "Peligro de zozobra",
                105 => "Naufragio",
                106 => "Deshabilitado y a la deriva",
                107 => "Socorro sin designar",
                108 => "Abandonando la nave",
                109 => "Pirateria/Robo a mano armada",
                110 => "Hombre al agua",
                112 => "EPIRB emitido",
                _ => "PELIGRO NO IDENTIFICADO" // Caso por defecto
            };
        }
        public static string PosteriorCom(int valor)
        {
            return valor switch
            {
                100 => "F3E/G3E ALL MODES TP",
                101 => "F3E/G3E DUPLEX TP",
                109 => "J3E TP",
                113 => "F1B/J2B TTY-FEC",
                115 => "F1B/J2B TTY-ARQ",
                126 => "Sin información",
                _ => "¿?" // Caso por defecto
            };
        }
    }
}