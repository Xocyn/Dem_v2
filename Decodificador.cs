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
            string ventana = input.Substring(i, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            TryDecodificarMensaje(mensajeInt, out int valor);

            string ventana2 = input.Substring(i+50, 10);
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

        public static void checkecc(int i, string input, List<int> ECC)
        {
            // 1️ Sumar todos los elementos
            int sum = ECC.Sum();

            // 2️ Aplicar máscara de 7 bits (0x7F = 127 = 01111111)
            int ecc = sum & 0x7F;

            int valor;
            string ventana = input.Substring(i + 20, 10);
            int mensajeInt = Convert.ToInt32(ventana, 2);
            Decodificador.TryDecodificarMensaje(mensajeInt, out valor);
            if (ecc == valor)
            {
                Console.WriteLine("ECC correcto");
            }
            else
            {
                Console.WriteLine("Error en ECC: valor calculado = " + ecc + ", valor recibido = " + valor);
            }
        }

    }
}
