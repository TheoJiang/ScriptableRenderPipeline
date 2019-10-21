using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// The different ray count values that can be asked for.
    /// </summary>
    [GenerateHLSL]
    public enum RayCountValues
    {
        AmbientOcclusion = 0,
        ShadowDirectional = 1,
        ShadowPointSpot = 2,
        ShadowAreaLight = 3,
        DiffuseGI_Forward = 4,
        DiffuseGI_Deferred = 5,
        ReflectionForward = 6,
        ReflectionDeferred = 7,
        Recursive = 8,
        Count = 9,
        Total = 10
    }

    class RayCountManager
    {
        
#if ENABLE_RAYTRACING
        // Buffer that holds ray counts (filled directly in the RT shaders with atomic operations)
        ComputeBuffer m_rayCountBuffer = null;

        // CPU Buffer that holds the current values
        uint[] m_ReducedRayCountValues = new uint[(int)RayCountValues.Count];

        // HDRP Resources
        ComputeShader rayCountCS;

        // Flag that defines if ray counting is enabled for the current frame
        bool m_IsActive;

        // Given that the requests are guaranteed to be executed in order we use a queue to store it
        Queue<AsyncGPUReadbackRequest> rayCountReadbacks = new Queue<AsyncGPUReadbackRequest>();

        public void Init(HDRenderPipelineRayTracingResources rayTracingResources)
        {
            // Keep track of the compute shader we are going to use
            rayCountCS = rayTracingResources.countTracedRays;

            // Allocate the ray count buffer
            m_rayCountBuffer = new ComputeBuffer((int)RayCountValues.Count, sizeof(uint));

            // Initialize the CPU  ray count (Optional)
            for(int i = 0; i < (int)RayCountValues.Count; ++i)
            {
                m_ReducedRayCountValues[i] = 0;
            }

            // By default, this is not active
            m_IsActive = false;
        }

        public void Release()
        {
            CoreUtils.SafeRelease(m_rayCountBuffer);
        }

        public void ClearRayCount(CommandBuffer cmd, HDCamera camera, bool isActive)
        {
            m_IsActive = isActive;

            // Make sure to clear before the current frame
            if (m_IsActive)
            {
                // Grab the kernel that we will be using for the clear
                int currentKenel = rayCountCS.FindKernel("ClearAtomicBuffer");
                cmd.SetComputeBufferParam(rayCountCS, currentKenel, HDShaderIDs._RayCountBuffer, m_rayCountBuffer);
                cmd.DispatchCompute(rayCountCS, currentKenel, 1, 1, 1);
            }
        }

        public int RayCountIsEnabled()
        {
            return m_IsActive ? 1 : 0;
        }

        public ComputeBuffer GetRayCountBuffer()
        {
            return m_rayCountBuffer;
        }

        public void EvaluateRayCount(CommandBuffer cmd, HDCamera camera)
        {
            if (m_IsActive)
            {
                using (new ProfilingSample(cmd, "Raytracing Debug Overlay", CustomSamplerId.RaytracingDebug.GetSampler()))
                {
                    // Enqueue an Async read-back for the atomically counted values
                    AsyncGPUReadbackRequest atomicCounterReadBack = AsyncGPUReadback.Request(m_rayCountBuffer, (int)RayCountValues.Count * sizeof(uint), 0);
                    rayCountReadbacks.Enqueue(atomicCounterReadBack);

                }
            }
        }

        public uint GetRaysPerFrame(RayCountValues rayCountValue)
        {
            if (!m_IsActive)
            {
                return 0;
            }
            else
            {
                while(rayCountReadbacks.Peek().done || rayCountReadbacks.Peek().hasError ==  true)
                {
                    // If this has an error, just skip it
                    if (!rayCountReadbacks.Peek().hasError)
                    {
                        // Grab the native array from this readback
                        NativeArray<uint> sampleCount = rayCountReadbacks.Peek().GetData<uint>();
                        for(int i = 0; i < (int)RayCountValues.Count; ++i)
                        {
                            m_ReducedRayCountValues[i] = sampleCount[i];
                        }
                    }
                    rayCountReadbacks.Dequeue();
                }

                if (rayCountValue != RayCountValues.Total)
                {
                    return m_ReducedRayCountValues[(int)rayCountValue];
                }
                else
                {
                    uint raycount = 0;
                    for (int i = 0; i < (int)RayCountValues.Count; ++i)
                    {
                        raycount += m_ReducedRayCountValues[i] ;
                    }
                    return raycount;
                }
            }
        }
#endif
    }
}
