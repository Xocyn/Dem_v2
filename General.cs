using MathNet.Numerics.Providers.LinearAlgebra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Linq;

namespace Dem_v2
{
    internal class General
    {
        public static List<int> SplitDigits2(List<string> input)
        {
            var result = new List<int>();

            foreach (string item in input)
            {
                foreach (char c in item)
                {
                    result.Add(c - '0'); // convierte char a int
                }
            }

            return result;
        }
        static public string newMMSI(List<int> mensaje, int i)
        {
            string mmsi;
            bool mismoContenido = true;
            List<int> MMSI = new List<int>();

            for (int k = i; k < i + 10; k += 1)
            {
                if (k % 2 == 0)  // Verifica si k es par
                {
                    int valor = mensaje[k];

                    if (valor == mensaje[k + 5])
                        MMSI.Add(valor);
                    else
                        mismoContenido = false;
                }
            }

            if (mismoContenido)
                mmsi = string.Join("", MMSI.Select(x => x.ToString("D2")));
            else
                mmsi = "XXXXXXXXXX";

            return mmsi;
        }

        public static string PrimerTelemando(int valor, out bool posicion)
        {
            posicion = valor == 121;

            return valor switch
            {
                100 => "Todos los modos F3E/G3E TP",
                101 => "Dúplex F3E/G3E TP",
                103 => "Interrogación secuencial",
                104 => "Incapaz de cumplimentar",
                105 => "Fin de llamada",
                106 => "Datos",
                109 => "J3E TP",
                110 => "Acuse de recibo de socorro",
                112 => "Retransmisión de alerta de socorro",
                113 => "F1B/J2B TTY-FEC",
                115 => "F1B/J2B TTY-ARQ",
                118 => "Prueba",
                121 => "Actualización del registro de posición o ubicación del barco",
                126 => "Ninguna información",
                _ => "¿?"
            };
        }

        public static string SegundoTelemando(int valor)
        {
            return valor switch
            {
                100 => "No se indica el motivo",
                101 => "Congestión en el centro de conmutación marítima",
                102 => "Ocupado",
                103 => "Indicación de la cola de espera",
                104 => "Estación prohibida",
                105 => "No hay operador disponible",
                106 => "Operador temporalmente no disponible",
                107 => "Equipo desconectado",
                108 => "Incapaz de utilizar el canal propuesto",
                109 => "Incapaz de utilizar el modo propuesto",
                110 => "Barcos y aeronaves, de Estados que no son parte de un conflicto armado",
                111 => "Transportes médicos",
                112 => "Oficina pública de llamada de previo pago",
                113 => "Facsímil/datos",
                120 => "No queda transmisión secuencial de SCA",
                121 => "1 vez la transmisión secuencial de SCA restante",
                122 => "2 veces la transmisión secuencial de SCA restante",
                123 => "3 veces la transmisión secuencial de SCA restante",
                124 => "4 veces la transmisión secuencial de SCA restante",
                125 => "5 veces la transmisión secuencial de SCA restante",
                126 => "Ninguna información",
                _ => "¿?" // Caso por defecto
            };
        }

        public static string Categoria(int valor)
        {
            return valor switch
            {
                100 => "Rutina",
                108 => "Seguridad",
                110 => "Urgencia",
                112 => "Socorro",
                _ => "¿?" // Caso por defecto
            };
        }

        public static string ACK(int valor)
        {
            return valor switch
            {
                117 => "Esperando ACK",
                122 => "ACK",
                127 => "EOS",
                _ => "¿?" // Caso por defecto
            };
        }

        public static (string, bool, bool) FrecuenciaCanal(List<int> mensaje, int i, out bool posicion)
        {
            string frec_canal = string.Empty;
            bool ocho_caracteres = false;
            bool canal = false;
            posicion = false;
            List<int> mensaje_canal = new List<int>();


            for (int k = i; k < i + 12; k += 1)
            {
                if (k % 2 == 0)  // Verifica si k es par
                {
                    mensaje_canal.Add(mensaje[k]); // Solo agrego los DX
                }
            }

            if (mensaje_canal[0] == 126) // caso especial de no data
            {
                frec_canal = "Sin información";
                return (frec_canal, ocho_caracteres, canal);
            }
            if ((mensaje_canal[0] == 55)) // caso especial de Pos2 en mensaje 2
            {
                mensaje_canal.RemoveAt(0); // elimino el 55 para que no me moleste en la decodificacion de la posicion geografica
                frec_canal = Geografica.Posicion(mensaje_canal);
                posicion= true;
                return (frec_canal, ocho_caracteres, canal);
            } 

            List<int> mensaje_separado = Separar(mensaje_canal);

            switch (mensaje_separado[0])
            {
                // FRECUENCIA DE RECEPCIÓN O TRANSMISIÓN multiplo de 100 Hz
                case 0:
                case 1:
                case 2:
                    frec_canal = $"{mensaje_separado[0]}{mensaje_separado[1]}{mensaje_separado[2]}{mensaje_separado[3]}{mensaje_separado[4]}.{mensaje_separado[5]}kHz";
                    break;
                // CANAL MF/HF
                case 3:
                    frec_canal = $"{mensaje_separado[1]}{mensaje_separado[2]}{mensaje_separado[3]}{mensaje_separado[4]}{mensaje_separado[5]}";
                    canal = true;
                    break;
                // FRECUENCIA DE RECEPCIÓN O TRANSMISIÓN multiplo de 10 Hz
                case 4:
                    ocho_caracteres = true;
                    frec_canal = $"{mensaje_separado[1]}{mensaje_separado[2]}{mensaje_separado[3]}{mensaje_separado[4]}{mensaje_separado[5]}.{mensaje_separado[6]}{mensaje_separado[7]} kHz";
                    break;
                // Sólo se utiliza en los equipos de la Rec. UIT-R M.586.
                case 8:
                    frec_canal = $"{mensaje_separado[1]}{mensaje_separado[2]}{mensaje_separado[3]}{mensaje_separado[4]}{mensaje_separado[5]}";
                    canal = true;
                    break;
                // CANAL DE RECEPCIÓN VHF
                case 9: 
                    frec_canal = $"{mensaje_separado[1]}{mensaje_separado[2]}{mensaje_separado[3]}{mensaje_separado[4]}{mensaje_separado[5]}";
                    canal = true;
                    break;
                default:
                    break;
            }

            return (frec_canal, ocho_caracteres, canal);
        }


        public static List<int> Separar(List<int> original)
        {
            List<int> separados = new List<int>();

            foreach (int n in original)
            {
                if (n >= 10)
                {
                    separados.Add(n / 10);  // decena
                    separados.Add(n % 10);  // unidad
                }
                else
                {
                    separados.Add(0);       // preserva el cero izquierdo (05 → 0, 5)
                    separados.Add(n);
                }
            }

            return separados;
        }
    }
}