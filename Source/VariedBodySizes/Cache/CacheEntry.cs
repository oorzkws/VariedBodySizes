using System.Runtime.CompilerServices;

namespace VariedBodySizes;

internal readonly struct CacheEntry<T>(T cachedValue)
{
    public readonly T CachedValue = cachedValue;
    private readonly int birthday = Current.gameInt.tickManager.ticksGameInt;

    public bool Expired(int currentTick, int expiryPeriod)
    {
        return birthday + expiryPeriod < currentTick;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Expired(int expiryPeriod)
    {
        return birthday + expiryPeriod < Current.gameInt.tickManager.ticksGameInt;
    }
}