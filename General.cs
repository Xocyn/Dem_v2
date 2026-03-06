using MathNet.Numerics.Providers.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Dem_v2
{
    internal class General
    {
        static public int Categoria(int i, int form, string input, List<int> ECC)
        {
            int j = 0;
            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            
            if (form == valor) // es el primer format recibido
            {
                // decodifico el categoria en base a esta posicion
                i = i + 40;
                ventana = input.Substring(i, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                ECC.Add(valor);
                if (valor == 108)
                {
                    Console.WriteLine("Seguridad");
                }
                else if (valor == 110)
                {
                    Console.WriteLine("Urgencia");
                }
                else
                {
                    Console.WriteLine("Categoria corrupta");
                }
            }
            else // es el segundo format recibido
            {
                // decodifico el categoria en base a esta posicion
                i = i + 20;
                ventana = input.Substring(i, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                ECC.Add(valor);
                if (valor == 108)
                {
                    Console.WriteLine("Seguridad");
                }
                else if (valor == 110)
                {
                    Console.WriteLine("Urgencia");
                }
                else
                {
                    Console.WriteLine("Categoria corrupta");
                }
            }
            j = i + 20;
            return j;
        }

        static public int MMSI_2(int i, string input, List<int> ECC)
        {
            int j = 0;
            List<int> MMSI = new List<int>();
            List<int> same = new List<int>();
            List<string> fail = new List<string> { "X", "X", "X", "X", "X", "X", "X", "X", "X" };
            string ventana;
            int mensajeInt;
            int valor;

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
            j = i + 100;
            return j;
        }


        static public int Mensaje_1(int i, string input, List<int> ECC)
        {
            int j = 0;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            Console.WriteLine("Primer Telecomando:");
            switch (valor)
            {
                case 100:
                    Console.WriteLine("ALL MODES F3E/G3E TP"); ECC.Add(valor);
                    break;
                case 101:
                    Console.WriteLine("DUPLEX F3E/G3E TP"); ECC.Add(valor);
                    break;
                case 103:
                    Console.WriteLine("Interrogación secuencial"); ECC.Add(valor);
                    break;
                case 104:
                    Console.WriteLine("Incapaz de cumplimentar"); ECC.Add(valor);
                    break;
                case 105:
                    Console.WriteLine("Fin de llamada"); ECC.Add(valor);
                    break;
                case 106:
                    Console.WriteLine("Datos"); ECC.Add(valor);
                    break;
                case 109:
                    Console.WriteLine("J3E TP"); ECC.Add(valor);
                    break;
                case 110:
                    Console.WriteLine("ACK de socorro"); ECC.Add(valor);
                    break;
                case 112:
                    Console.WriteLine("Retransmisión de socorro"); ECC.Add(valor);
                    break;
                case 113:
                    Console.WriteLine("F1B/J2B TTY-FEC"); ECC.Add(valor);
                    break;
                case 115:
                    Console.WriteLine("F1B/J2B TTY-ARQ"); ECC.Add(valor);
                    break;
                case 118:
                    Console.WriteLine("Prueba"); ECC.Add(valor);
                    break;
                case 121:
                    Console.WriteLine("Actualización de resgistro de posición/ubicación del barco"); ECC.Add(valor);
                    break;
                case 126:
                    Console.WriteLine("Ninguna información"); ECC.Add(valor);
                    break;
                default:
                    Console.WriteLine("¿¿¿???"); ECC.Add(valor);
                    break;
            }

            i = i + 20;

            ventana = input.Substring(i, 10);
            mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor_2);
            Console.WriteLine("Segundo Telecomando:");
            switch (valor_2)
            {
                case 100:
                    Console.WriteLine("Sin motivo"); ECC.Add(valor_2);
                    break;
                case 101:
                    Console.WriteLine("Congestión en el centro de conmutación marítima"); ECC.Add(valor_2);
                    break;
                case 102:
                    Console.WriteLine("Ocupado"); ECC.Add(valor_2);
                    break;
                case 103:
                    Console.WriteLine("Indicación de cola de espera"); ECC.Add(valor_2);
                    break;
                case 104:
                    Console.WriteLine("Estación prohibida"); ECC.Add(valor_2);
                    break;
                case 105:
                    Console.WriteLine("No hay operador disponible"); ECC.Add(valor_2);
                    break;
                case 106:
                    Console.WriteLine("Operador temporalmente no disponible"); ECC.Add(valor_2);
                    break;
                case 107:
                    Console.WriteLine("Equipo desconectado"); ECC.Add(valor_2);
                    break;
                case 108:
                    Console.WriteLine("Incapaz de utilizar el canal propuesto"); ECC.Add(valor_2);
                    break;
                case 109:
                    Console.WriteLine("Incapaz de utilizar el modo propuesto"); ECC.Add(valor_2);
                    break;
                case 110:
                    Console.WriteLine("Barcos y aeronaves, de Estados que nos son parte de un conflicto armado"); ECC.Add(valor_2);
                    break;
                case 111:
                    Console.WriteLine("Transportes médicos"); ECC.Add(valor_2);
                    break;
                case 112:
                    Console.WriteLine("Oficina pública de llamada de previo pago"); ECC.Add(valor_2);
                    break;
                case 113:
                    Console.WriteLine("Facsímil/datos"); ECC.Add(valor_2);
                    break;
                case 120:
                    Console.WriteLine("No queda transmisión secuencial de SCA"); ECC.Add(valor_2);
                    break;
                case 121:
                    Console.WriteLine("1  vez la transmisión secuencial de SCA restante"); ECC.Add(valor_2);
                    break;
                case 122:
                    Console.WriteLine("2  veces la transmisión secuencial de SCA restante"); ECC.Add(valor_2);
                    break;
                case 123:
                    Console.WriteLine("3  veces la transmisión secuencial de SCA restante"); ECC.Add(valor_2);
                    break;
                case 124:
                    Console.WriteLine("4  veces la transmisión secuencial de SCA restante"); ECC.Add(valor_2);
                    break;
                case 125:
                    Console.WriteLine("5  veces la transmisión secuencial de SCA restante"); ECC.Add(valor_2);
                    break;
                case 126:
                    Console.WriteLine("Ninguna información"); ECC.Add(valor_2);
                    break;
                default:
                    Console.WriteLine("¿¿¿???"); ECC.Add(valor_2);
                    break;
            }
            i = i + 20;
            j = i;
            return j;
        }
        static public int Mensaje_2(int i, string input, List<int> ECC, byte h)
        {
            int j = 0;

            List<int> freq_canal = new List<int>();
            for (int k = 0; k < 60; k += 10)
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                freq_canal.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(freq_canal);

            foreach (int fc in freq_canal)
            {
                ECC.Add(fc);
            }

            // quiero saber con que voy a laburar
            List<int> freq_canal_digitos = SplitDigits(freq_canal);

            //var resultado = Desagrupar(freq_canal);

            // Caso especial: 126
            if (freq_canal[2] == 126)
            {
                Console.WriteLine($"NO DATA: {string.Join(" | ", freq_canal)}");
            }
            else if (h == 1)
            {
                switch (freq_canal_digitos[0])
                {
                    case 0:
                    case 1:
                    case 2:
                        Console.WriteLine("Informacion de Frecuencia de Recepcion");
                        Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        //Console.WriteLine($"{freq_canal_digitos[0]}{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}.{freq_canal_digitos[4]}kHz");
                        break;

                    case 3:
                        Console.WriteLine("Informacion de canal MF/HF");
                        Console.WriteLine("EN DESARROLLO");
                        break;

                    case 9:
                        Console.WriteLine("Canal de recepción VHF");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}");
                        break;

                    default:
                        Console.WriteLine("Caracter HM no identificado");
                        Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        break;
                }
            }
            else if (h == 2) 
            {
                switch (freq_canal_digitos[0])
                {
                    case 0:
                    case 1:
                    case 2:
                        Console.WriteLine("Informacion de Frecuencia de Transmisión");
                        Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        //Console.WriteLine($"{freq_canal_digitos[0]}{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}.{freq_canal_digitos[4]}kHz");
                        break;

                    case 3:
                        Console.WriteLine("Informacion de canal MF/HF");
                        Console.WriteLine("EN DESARROLLO");
                        break;

                    case 9:
                        Console.WriteLine("Canal de transmisión VHF");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}");
                        break;

                    default:
                        Console.WriteLine("Caracter HM no identificado");
                        break;

                }

            }

            j = i + 60;
            return j;
        }

        public static List<int> SplitDigits(List<int> input)
        {
            var result = new List<int>();

            foreach (int number in input)
            {
                // Convertimos a string para separar cada dígito
                foreach (char c in number.ToString())
                {
                    result.Add(c - '0'); // convierte char a int
                }
            }

            return result;
        }

    }
}
