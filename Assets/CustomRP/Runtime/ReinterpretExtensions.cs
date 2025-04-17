
using System.Runtime.InteropServices;

namespace NoesisRender
{
    public static class ReinterpretExtensions
    {

        /// <summary>
        /// Force float and int use the same bits in memory. Its possible because they have same size - 4 bits. Also, this code is safe.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        struct IntFloat
        {
            [FieldOffset(0)] public int intValue;

            [FieldOffset(0)] public float floatValue;
        }


        /// <summary>
        /// Light mask stored as uint and use Bitwise-And operation. In C# there is no asuint like in HLSL. To not mess up with bits, we need these exstension
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static float ReinterpretAsFloat(this int value)
        {
            IntFloat converter = default;
            converter.intValue = value;
            return converter.floatValue;
        }
    }
}
