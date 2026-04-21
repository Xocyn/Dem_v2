using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Dem_v2
{
    internal class Decodificador
    {
        public static bool TryDecodificarMensaje(int mensaje10Bits, out int valor)
        {
            int datos = mensaje10Bits >> 3;        // 7 bits de datos
            int control = mensaje10Bits & 0b111;   // 3 bits de control
            int val = 0;
            int ceros = 0;

            for (int i = 0; i < 7; i++)
            {
                int bit = (datos >> i) & 1;   // lee LSB → MSB
                val |= bit << (6 - i);       // asigna peso invertido
                if (bit == 0)
                    ceros++;
            }

            if (ceros != control)
            {
                valor = 0;
                return false;
            }

            valor = val;
            return true;
        }

        public static bool TryDeco(string mensaje10Bits, out int valor)
        {
            valor = 0;

            // validar longitud
            if (mensaje10Bits.Length != 10)
                return false;

            // validar caracteres
            foreach (char c in mensaje10Bits)
                if (c != '0' && c != '1')
                    return false;

            // separar partes
            string datosStr = mensaje10Bits.Substring(0, 7);
            string controlStr = mensaje10Bits.Substring(7, 3);

            int datos = Convert.ToInt32(datosStr, 2);
            int control = Convert.ToInt32(controlStr, 2);

            int val = 0;
            int ceros = 0;

            for (int i = 0; i < 7; i++)
            {
                int bit = (datos >> i) & 1;   // lee LSB → MSB
                val |= bit << (6 - i);        // asigna peso invertido

                if (bit == 0)
                    ceros++;
            }

            if (ceros != control)
                return false;

            valor = val;
            return true;
        }

        public static bool DxRx(string input, int i) // verificia si Dx y Rx son iguales
        {
            if (i + 10 > input.Length || i + 60 > input.Length)
            {
                Console.WriteLine("DxRx: stream demasiado corto para verificar.");
                return false;
            }

            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            TryDecodificarMensaje(mensajeInt, out int valor);

            string ventana2 = input.Substring(i + 50, 10);
            int mensajeInt2 = Convert.ToInt32(ventana2, 2);
            TryDecodificarMensaje(mensajeInt2, out int valor2);

            if (valor == valor2)
            {
                //Console.WriteLine("Dx y Rx son iguales");
                return true;
            }
            else
            {
                //Console.WriteLine("Dx y Rx NO son iguales");
                return false;
            }
        }

        public static bool checkecc(int i, string input, List<int> ECC)
        {
            int sum = ECC.Sum();
            int ecc = sum & 0x7F;

            if (i + 30 > input.Length)
            {
                Console.WriteLine("Stream demasiado corto para leer ECC.");
                return false;
            }

            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            if (ecc == valor)
            {
                Console.WriteLine("ECC correcto");
                return true;
            }
            else
            {
                Console.WriteLine("Error en ECC: calculado=" + ecc + " recibido=" + valor);
                return false;
            }
        }

        public static bool Mod2Sum7Bits(int i, string input, List<int> ECC)
        {                
            int result = 0;

            foreach (int v in ECC)
            {
                result ^= v; // XOR acumulativo (suma módulo 2)
            }

            // Nos quedamos con los 7 bits menos significativos
            result &= 0x7F;

            if (i + 30 > input.Length)
            {
                Console.WriteLine("Stream demasiado corto para leer ECC.");
                return false;
            }

            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out int valor);
            if (result == valor)
            {
                Console.WriteLine("ECC correcto");
                return true;
            }
            else
            {
                Console.WriteLine("Error en ECC: calculado=" + result + " recibido=" + valor);
                return false;
            }
        }

        public static void Mensaje(string input, int i, out List<int> Message)
        {
            Message = new List<int>();

            for (int k = i; k + 10 <= input.Length; k += 10)
            {
                string ventana = input.Substring(k, 10);
                int mensaje10Bits = Convert.ToInt32(ventana, 2);

                int datos = mensaje10Bits >> 3;
                int control = mensaje10Bits & 0b111;
                int val = 0, ceros = 0;

                for (int h = 0; h < 7; h++)
                {
                    int bit = (datos >> h) & 1;
                    val |= bit << (6 - h);
                    if (bit == 0) ceros++;
                }

                // DX válido → agregar y continuar
                if (ceros == control)
                {
                    Message.Add(val);
                    continue;
                }

                // DX inválido → intentar con RX (k + 50)
                int kRx = k + 50;
                if (kRx + 10 > input.Length)
                {
                    // RX fuera de rango: no hay forma de recuperar este valor
                    Message.Add(0);
                    continue;
                }

                string ventana2 = input.Substring(kRx, 10);
                int mensajeInt2 = Convert.ToInt32(ventana2, 2);
                int datos2 = mensajeInt2 >> 3;
                int control2 = mensajeInt2 & 0b111;
                int val2 = 0, ceros2 = 0;

                for (int h = 0; h < 7; h++)
                {
                    int bit = (datos2 >> h) & 1;
                    val2 |= bit << (6 - h);
                    if (bit == 0) ceros2++;
                }

                // RX válido → usar su valor; RX inválido → marcar como 0
                Message.Add(ceros2 == control2 ? val2 : 0);
            }
        }

    }
}