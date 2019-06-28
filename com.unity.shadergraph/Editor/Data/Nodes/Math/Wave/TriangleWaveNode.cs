using UnityEngine.ShaderGraph.Hlsl;
using static UnityEngine.ShaderGraph.Hlsl.Intrinsics;

namespace UnityEditor.ShaderGraph
{
    [Title("Math", "Wave", "Triangle Wave")]
    class TriangleWaveNode : CodeFunctionNode
    {
        public TriangleWaveNode()
        {
            name = "Triangle Wave";
        }

        [HlslCodeGen]
        static void TriangleWave(
            [Slot(0, Binding.None)] [AnyDimension] Float4 In,
            [Slot(1, Binding.None)] [AnyDimension] out Float4 Out)
        {
            Out = 2.0 * abs(2 * (In - floor(0.5 + In))) - 1.0;
        }
    }
}
