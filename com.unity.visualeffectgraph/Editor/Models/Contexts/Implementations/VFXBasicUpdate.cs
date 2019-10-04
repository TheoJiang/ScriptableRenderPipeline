using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor.VFX.Block;

namespace UnityEditor.VFX
{
    [VFXInfo]
    class VFXBasicUpdate : VFXContext
    {
        public enum VFXIntegrationMode
        {
            Euler,
            None
        }

        [Header("Particle Update Options")]
        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Tooltip("When enabled, particle positions are automatically updated using their velocity.")]
        public bool updatePosition = true;

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Tooltip("When enabled, particle rotations are automatically updated using their angular velocity.")]
        public bool updateRotation = true;

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Tooltip("When enabled, the particle age attribute will increase every frame based on deltaTime.")]
        public bool ageParticles = true;

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), Tooltip("When enabled, particles whose age exceeds their lifetime will be destroyed.")]
        public bool reapParticles = true;

        public VFXBasicUpdate() : base(VFXContextType.Update, VFXDataType.None, VFXDataType.None) {}
        public override string name { get { return "Update " + ObjectNames.NicifyVariableName(ownedType.ToString()); } }
        public override string codeGeneratorTemplate { get { return VisualEffectGraphPackageInfo.assetPackagePath + "/Shaders/VFXUpdate"; } }
        public override bool codeGeneratorCompute { get { return true; } }
        public override VFXTaskType taskType { get { return VFXTaskType.Update; } }
        public override VFXDataType inputType { get { return GetData() == null ? VFXDataType.Particle : GetData().type; } }
        public override VFXDataType outputType { get { return GetData() == null ? VFXDataType.Particle : GetData().type; } }

        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                if (GetData().IsCurrentAttributeRead(VFXAttribute.OldPosition))
                {
                    yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Read);
                    yield return new VFXAttributeInfo(VFXAttribute.OldPosition, VFXAttributeMode.Write);
                }

                if (GetData().IsCurrentAttributeWritten(VFXAttribute.Alive) && GetData().dependenciesOut.Any(d => ((VFXDataParticle)d).hasStrip))
                    yield return new VFXAttributeInfo(VFXAttribute.StripAlive, VFXAttributeMode.ReadWrite);
            }
        }

        protected override IEnumerable<string> filteredOutSettings
        {
            get
            {
                foreach (var setting in base.filteredOutSettings)
                    yield return setting;

                var lifeTime = GetData().IsCurrentAttributeWritten(VFXAttribute.Lifetime);
                var age = GetData().IsCurrentAttributeRead(VFXAttribute.Age);

                if (!(age || lifeTime))
                    yield return "ageParticles";

                if (!lifeTime)
                    yield return "reapParticles";
            }
        }

        protected override IEnumerable<VFXBlock> implicitPostBlock
        {
            get
            {
                var data = GetData();

                if (updatePosition && data.IsCurrentAttributeWritten(VFXAttribute.Velocity))
                    yield return VFXBlock.CreateImplicitBlock<EulerIntegration>(data);

                if (updateRotation &&
                    (
                        data.IsCurrentAttributeWritten(VFXAttribute.AngularVelocityX) ||
                        data.IsCurrentAttributeWritten(VFXAttribute.AngularVelocityY) ||
                        data.IsCurrentAttributeWritten(VFXAttribute.AngularVelocityZ))
                    )
                    yield return VFXBlock.CreateImplicitBlock<AngularEulerIntegration>(data);

                var lifeTime = GetData().IsCurrentAttributeWritten(VFXAttribute.Lifetime);
                var age = GetData().IsCurrentAttributeRead(VFXAttribute.Age);

                if (age || lifeTime)
                {
                    if (ageParticles)
                        yield return VFXBlock.CreateImplicitBlock<Age>(data);

                    if (lifeTime && reapParticles)
                        yield return VFXBlock.CreateImplicitBlock<Reap>(data);
                }
            }
        }

        public override IEnumerable<string> additionalDefines
        {
            get
            {
                if ((GetData() as VFXDataParticle).NeedsIndirectBuffer())
                    yield return "VFX_HAS_INDIRECT_DRAW";

                if (ownedType == VFXDataType.ParticleStrip)
                    yield return "HAS_STRIPS";
            }
        }
    }
}
