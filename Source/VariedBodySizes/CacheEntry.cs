using Verse;

namespace VariedBodySizes;

internal readonly struct CacheEntry<T>
{
    public readonly T CachedValue;
    private readonly int birthday;

    public CacheEntry(T cachedValue)
    {
        CachedValue = cachedValue;
        birthday = Find.TickManager.TicksGame;
    }

    public bool Expired(int currentTick, int expiryPeriod)
    {
        return birthday + expiryPeriod < currentTick;
    }

    public bool Expired(int expiryPeriod)
    {
        return birthday + expiryPeriod < Find.TickManager.TicksGame;
    }
}