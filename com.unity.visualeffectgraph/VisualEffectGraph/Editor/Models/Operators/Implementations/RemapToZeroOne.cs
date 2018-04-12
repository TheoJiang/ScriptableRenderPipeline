using System;
using UnityEngine;

namespace UnityEditor.VFX.Operator
{
    [VFXInfo(category = "Math/Remap")]
    class RemapToZeroOne : VFXOperatorFloatUnifiedWithVariadicOutput
    {
        [VFXSetting, Tooltip("Whether the values are clamped to the input/output range")]
        public bool Clamp = false;

        public class InputProperties
        {
            [Tooltip("The value to be remapped into the new range.")]
            public FloatN input = new FloatN(0.0f);
        }

        override public string name { get { return "Remap [-1..1] => [0..1]"; } }

        protected override VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            var type = inputExpression[0].valueType;

            var zerofive = VFXOperatorUtility.HalfExpression[type];
            var expression = VFXOperatorUtility.Mad(inputExpression[0], zerofive, zerofive);

            if (Clamp)
                return new[] { VFXOperatorUtility.Saturate(expression) };
            else
                return new[] { expression };
        }
    }
}
