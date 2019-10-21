void IncrementRayCounter(int rayType, int numRays)
{
    if (_RayCountEnabled > 0)
    {
        // To avoid congestion, we do a local wave reduction and only one active thread/lane updates the atomic counter
        int activeLanes = WaveActiveCountBits(true);
        if (WavePrefixCountBits(true) == 0)  // This is equivalent to WaveIsFirstLane()
        {
            InterlockedAdd(_RayCountBuffer[rayType], activeLanes * numRays);
        }
    }
}
