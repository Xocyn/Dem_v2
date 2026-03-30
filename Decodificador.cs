using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Dem_v2
{
    internal class Decodificador
    {
        public static int DecodificarMensaje(int mensaje10Bits)
        {
            // 1. Extraer campos
            int datos = mensaje10Bits >> 3;        // 7 bits de datos
            int control = mensaje10Bits & 0b111;     // 3 bits de control
            int valor = 0;
            int ceros = 0;

            // 2. Contar ceros en los 7 bits de datos y reconstruir carácter
            for (int i = 0; i < 7; i++)
            {
                int bit = (datos >> i) & 1;   // lee LSB → MSB
                valor |= bit << (6 - i);      // asigna peso invertido
                if (((datos >> i) & 1) == 0)
                    ceros++;
            }

            // 3. Verificar control de errores
            if (ceros != control)
                Console.WriteLine("Error de control: cantidad de ceros incorrecta");

            // 4. Devuelve el valor del carácter
            return valor;
        }

        // Nuevo: versión "try" que indica si el mensaje de 10 bits es válido.
        // Devuelve true si la verificación de control coincide y sale el valor reconstruido.
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

    }
}