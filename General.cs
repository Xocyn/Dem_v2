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
        static public int Categoria(int i, int form, string input, List<int> ECC, out bool socorro)
        {
            int j = 0;
            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            socorro = false;

            if (form == valor) // es el primer format recibido
            {
                // decodifico el categoria en base a esta posicion
                i = i + 40;
                ventana = input.Substring(i, 10);
                mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
                ECC.Add(valor);
                if (valor == 108)
                    Console.WriteLine("Seguridad");

                else if (valor == 110)
                    Console.WriteLine("Urgencia");

                else if (valor == 112)
                {
                    Console.WriteLine("Socorro");
                    socorro = true;
                }

                else
                    Console.WriteLine("Categoria corrupta");
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
                    Console.WriteLine("Seguridad");

                else if (valor == 110)
                    Console.WriteLine("Urgencia");

                else if (valor == 112)
                {
                    Console.WriteLine("Socorro");
                    socorro = true;
                }

                else
                    Console.WriteLine("Categoria corrupta");

            }
            j = i + 20;
            return j;
        }

        static public (int, bool, bool) Categoria2(int i, string input, List<int> ECC)
        {
            bool socorro = false;
            bool rutina = false;
            int j = 0;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            ECC.Add(valor);
            Console.Write($"Categoria de llamada {valor}: ");
            if (valor == 108)
                Console.WriteLine("Seguridad");
            else if (valor == 110)
                Console.WriteLine("Urgencia");
            else if (valor == 100)
            {
                Console.WriteLine("Rutina");
                rutina = true;
            }
            else if (valor == 112)
            {
                Console.WriteLine("Retransmisión de Socorro");
                socorro = true;
            }
            else
                Console.WriteLine("Categoria corrupta");
            j = i + 20;
            return (j, rutina, socorro);
        }

        static public (int, string) MMSI_2(int i, string input, List<int> ECC)
        {
            string si;
            int j = 0;
            List<int> MMSI = new List<int>();
            List<int> same = new List<int>();
            //List<string> fail = new List<string> { "X", "X", "X", "X", "X", "X", "X", "X", "X" };
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

            string mm = string.Join("", MMSI.Select(x => x.ToString("D2")));

            if (mismoContenido)
            {
                //Console.WriteLine("MMSI DX/RX coinciden");
                //Console.WriteLine($"MMSI: {mm}");
                si = mm;
            }
            else
            {
                //Console.WriteLine("MMSI DX/RX NO coinciden");
                //Console.WriteLine($"MMSI desconocido: {string.Join(" | ", fail)}");
                si = "XXXXXXXXXX";

            }
            j = i + 100;
            return (j, si);
        }


        static public (int, bool) Mensaje_1(int i, string input, List<int> ECC)
        {
            bool pos = false;
            int j = 0;
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            Console.Write("Primer Telecomando: ");
            switch (valor)
            {
                case 100:
                    Console.WriteLine("ALL MODES F3E/G3E TP");
                    break;
                case 101:
                    Console.WriteLine("DUPLEX F3E/G3E TP");
                    break;
                case 103:
                    Console.WriteLine("Interrogación secuencial");
                    break;
                case 104:
                    Console.WriteLine("Incapaz de cumplimentar");
                    break;
                case 105:
                    Console.WriteLine("Fin de llamada");
                    break;
                case 106:
                    Console.WriteLine("Datos");
                    break;
                case 109:
                    Console.WriteLine("J3E TP");
                    break;
                case 110:
                    Console.WriteLine("ACK de socorro");
                    break;
                case 112:
                    Console.WriteLine("Retransmisión de socorro");
                    break;
                case 113:
                    Console.WriteLine("F1B/J2B TTY-FEC");
                    break;
                case 115:
                    Console.WriteLine("F1B/J2B TTY-ARQ");
                    break;
                case 118:
                    Console.WriteLine("Prueba");
                    break;
                case 121:
                    Console.WriteLine("Actualización de resgistro de posición/ubicación del barco");
                    pos = true;
                    break;
                case 126:
                    Console.WriteLine("Ninguna información");
                    break;
                default:
                    Console.WriteLine("¿¿¿???");
                    break;
            }
            ECC.Add(valor);
            i = i + 20;

            ventana = input.Substring(i, 10);
            mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor_2);
            Console.Write("Segundo Telecomando: ");
            switch (valor_2)
            {
                case 100:
                    Console.WriteLine("Sin motivo");
                    break;
                case 101:
                    Console.WriteLine("Congestión en el centro de conmutación marítima");
                    break;
                case 102:
                    Console.WriteLine("Ocupado");
                    break;
                case 103:
                    Console.WriteLine("Indicación de cola de espera");
                    break;
                case 104:
                    Console.WriteLine("Estación prohibida");
                    break;
                case 105:
                    Console.WriteLine("No hay operador disponible");
                    break;
                case 106:
                    Console.WriteLine("Operador temporalmente no disponible");
                    break;
                case 107:
                    Console.WriteLine("Equipo desconectado");
                    break;
                case 108:
                    Console.WriteLine("Incapaz de utilizar el canal propuesto");
                    break;
                case 109:
                    Console.WriteLine("Incapaz de utilizar el modo propuesto");
                    break;
                case 110:
                    Console.WriteLine("Barcos y aeronaves, de Estados que nos son parte de un conflicto armado");
                    break;
                case 111:
                    Console.WriteLine("Transportes médicos");
                    break;
                case 112:
                    Console.WriteLine("Oficina pública de llamada de previo pago");
                    break;
                case 113:
                    Console.WriteLine("Facsímil/datos");
                    break;
                case 120:
                    Console.WriteLine("No queda transmisión secuencial de SCA");
                    break;
                case 121:
                    Console.WriteLine("1  vez la transmisión secuencial de SCA restante");
                    break;
                case 122:
                    Console.WriteLine("2  veces la transmisión secuencial de SCA restante");
                    break;
                case 123:
                    Console.WriteLine("3  veces la transmisión secuencial de SCA restante");
                    break;
                case 124:
                    Console.WriteLine("4  veces la transmisión secuencial de SCA restante");
                    break;
                case 125:
                    Console.WriteLine("5  veces la transmisión secuencial de SCA restante");
                    break;
                case 126:
                    Console.WriteLine("Ninguna información");
                    break;
                default:
                    Console.WriteLine("¿¿¿???");
                    break;
            }
            ECC.Add(valor_2);
            i = i + 20;
            j = i;
            return (j, pos);
        }
        static public (int, byte) Mensaje_2(int i, string input, List<int> ECC, byte h)
        {
            int j = 0;

            if (h == 44)
            {
                j = i; h = 1;
                return (j, h);
            }

            List<int> freq_canal = new List<int>();
            for (int k = 0; k < 80; k += 10) // 80 porque existe la posiblilidad de que sean 4 caracteres
            {
                string ventana = input.Substring(i + k, 10);
                int mensajeInt = Convert.ToInt32(ventana, 2);
                Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
                freq_canal.Add(valor);
            }
            Geografica.EliminarPosicionesImpares(freq_canal);


            // Caso en el que exista Pos2 en Mensaje 2 //
            if (freq_canal[0] == 55)
            {
                i += 20; ECC.Add(55);
                i = Geografica.PuntoGeografico(i, input, ECC, out bool valid2);
                j = i;
                h = 44; // asigno un valor a h para que no se ejecute el bloque de código de frecuencia/canal
                return (j, h);
            }

            foreach (int fc in freq_canal)
            {
                ECC.Add(fc);
            }

            List<string> FC = freq_canal
            .Select(x => x.ToString("D2"))
            .ToList();

            // quiero saber con que voy a laburar
            //List<int> freq_canal_digitos = SplitDigits(freq_canal);
            List<int> freq_canal_digitos = SplitDigits2(FC);

            //var resultado = Desagrupar(freq_canal);

            // Caso especial: 126
            if (freq_canal[2] == 126) // creo que me aseguro ya que si el caracter HM es 126, entonces no hay información de frecuencia o canal
            {
                Console.WriteLine($"NO DATA: {string.Join(" | ", freq_canal)}");
                j = i + 60;
                ECC.RemoveAt(ECC.Count - 1);
            }
            else if (h == 1)
            {
                switch (freq_canal_digitos[0])
                {
                    case 0:
                    case 1:
                    case 2:
                        h = 2;
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Informacion de Frecuencia de Recepcion: ");
                        //Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        Console.WriteLine($"{freq_canal_digitos[0]}{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}.{freq_canal_digitos[5]}kHz");
                        break;

                    case 3:
                        h = 2;
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Informacion de canal MF/HF: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}");
                        break;

                    case 4:
                        h = 2;
                        j = i + 80;
                        Console.Write("Informacion de Frecuencia de Recepcion: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}.{freq_canal_digitos[6]}{freq_canal_digitos[7]}kHz");
                        //Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        break;

                    case 8:
                    case 9:
                        h = 2;
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Canal de recepción VHF: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}");
                        break;

                    default:
                        h = 2;
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Caracter HM no identificado: ");
                        Console.WriteLine(string.Join(" ", freq_canal_digitos));
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
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Informacion de Frecuencia de Transmisión: ");
                        //Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        Console.WriteLine($"{freq_canal_digitos[0]}{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}.{freq_canal_digitos[5]}kHz");
                        h = 1;
                        break;

                    case 3:
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Informacion de canal MF/HF: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}");
                        h = 1;
                        break;

                    case 4:
                        j = i + 80;
                        Console.Write("Informacion de Frecuencia de Transmisión: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}.{freq_canal_digitos[6]}{freq_canal_digitos[7]}kHz");
                        //Console.WriteLine(string.Join(", ", freq_canal_digitos));
                        h = 1;
                        break;

                    case 8:
                    case 9:
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Canal de recepción VHF: ");
                        Console.WriteLine($"{freq_canal_digitos[1]}{freq_canal_digitos[2]}{freq_canal_digitos[3]}{freq_canal_digitos[4]}{freq_canal_digitos[5]}");
                        h = 1;
                        break;

                    default:
                        j = i + 60; ECC.RemoveAt(ECC.Count - 1);
                        Console.Write("Caracter HM no identificado: ");
                        Console.WriteLine(string.Join(" ", freq_canal_digitos));
                        h = 1;
                        break;

                }

            }

            return (j, h);
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

        static public int Retransmision(int i, string input, List<int> ECC)
        {
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            Console.Write($"Categoria de llamada {valor}: ");
            switch (valor)
            {
                case 110:
                    Console.WriteLine("ACK de socorro");
                    break;
                case 112:
                    Console.WriteLine("Retransmisión de socorro");
                    break;
                default:
                    Console.WriteLine("¿¿¿???");
                    break;
            }
            ECC.Add(valor);
            i = i + 20;
            return i;
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

        public static string PrimerTelemando(int valor)
        {
            return valor switch
            {
                100 => "Todos los modos F3E/G3E TP",
                101 => "Dúplex F3E/G3E TP",
                103 => "Interrogación secuencial",
                104 => "Incapaz de cumplimentar",
                105 => "Fin de llamada",
                106 => "Datos",
                109 => "J3E TP",
                110 => "Acuese de recibo de socorro",
                112 => "Retransmisión de alerta de socorro",
                113 => "F1B/J2B TTY-FEC",
                115 => "F1B/J2B TTY-ARQ",
                118 => "Prueba",
                121 => "Actualización del registro de posición o ubicación del barco",
                126 => "Ninguna información",
                _ => "¿?" // Caso por defecto
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

        public static (string, bool, bool) FrecuenciaCanal(List<int> mensaje, int i)
        {
            string frec_canal = string.Empty;
            bool ocho_caracteres = false;
            bool canal = false;
            List<int> mensaje_canal = new List<int>();


            for (int k = i; k < i + 8; k += 1)
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