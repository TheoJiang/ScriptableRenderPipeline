using UnityEngine.ShaderGraph.Hlsl;
using static UnityEngine.ShaderGraph.Hlsl.Intrinsics;

namespace UnityEditor.ShaderGraph
{
    [Title("Math", "Range", "Maximum")]
    class MaximumNode : CodeFunctionNode
    {
        public MaximumNode()
        {
            name = "Maximum";
        }

        [HlslCodeGen]
        static void Unity_Maximum(
            [Slot(0, Binding.None, 0, 0, 0, 0)] [AnyDimension] Float4 A,
            [Slot(1, Binding.None, 1, 1, 1, 1)] [AnyDimension] Float4 B,
            [Slot(2, Binding.None)] [AnyDimension] out Float4 Out)
        {
            Out = max(A, B);
        }
    }
}
