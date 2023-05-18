using System.Runtime.CompilerServices;

namespace VariedBodySizes;

internal readonly struct CacheEntry<T>
{
    public readonly T CachedValue;
    private readonly int birthday;

    public CacheEntry(T cachedValue)
    {
        CachedValue = cachedValue;
        birthday = Current.gameInt.tickManager.ticksGameInt;
    }

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