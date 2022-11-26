using Verse;

namespace VariedBodySizes;

internal readonly struct CacheEntry<T>
{
    public readonly T CachedValue;
    public readonly Pawn Owner;
    private readonly int birthday;

    public CacheEntry(T cachedValue, Pawn owner)
    {
        CachedValue = cachedValue;
        Owner = owner;
        birthday = Find.TickManager.TicksGame;
    }

    public bool Expired(int currentTick, int expiryPeriod)
    {
        return birthday + expiryPeriod < currentTick;
    }
}