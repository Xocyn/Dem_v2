using System;
using System.Collections.Generic;

namespace Dem_v2
{
    // ESTO TIENE UN PROBLEMA SI NO TENGO EN FASE LOS MENSAJES QUE PRETENDO QUE ME LLEGUEN NO VA A FUNCIONAR
    // PERO BUENO, ASÍ ES LA VIDA.
    internal enum PhasingPattern
    {
        None,
        DxRxDx,
        RxDxRx,
        RxRxRx
    }

    internal static class PhasingSequence
    {
        // Predicados
        private static bool IsDx(int v) => v == 125;
        private static bool IsRx(int v) => v >= 104 && v <= 111;

        // Intenta detectar un patrón válido en cualquier ventana de 3 valores dentro de 'sequence'.
        // Devuelve true y el patrón encontrado en 'pattern' si existe; false y PhasingPattern.None en caso contrario.

        public static bool TryCaracter(int msj)
        {
            if(IsDx(msj) || IsRx(msj))
            {
                return true; 
            }
            else
            {
                return false;
            }
        }

        public static bool TryDetect(List<(int Index, int Value)> sequence, out PhasingPattern pattern)
        {
            pattern = PhasingPattern.None;
            if (sequence == null || sequence.Count < 3) return false;

            for (int i = 0; i + 3 <= sequence.Count; i++)
            {
                // Extraer sólo el 'Value' de cada tupla (Index, Value)
                var (_, a) = sequence[i];
                var (_, b) = sequence[i + 1];
                var (_, c) = sequence[i + 2];

                if (IsDx(a) && IsRx(b) && IsDx(c) || IsRx(a) && IsDx(b) && IsDx(c)|| IsDx(a) && IsDx(b) && IsRx(c))
                {
                    pattern = PhasingPattern.DxRxDx;
                    return true;
                }

                if (IsRx(a) && IsDx(b) && IsRx(c) || IsDx(a) && IsRx(b) && IsRx(c) || IsRx(a) && IsRx(b) && IsDx(c))
                {
                    pattern = PhasingPattern.RxDxRx;
                    return true;
                }

                // Todavia no contempla si Rx esta en orden
                if (IsRx(a) && IsRx(b) && IsRx(c) && (a > b) && (b > c))
                {
                    pattern = PhasingPattern.RxRxRx;
                    return true;
                }
            }

            return false;
        }

    }
}
