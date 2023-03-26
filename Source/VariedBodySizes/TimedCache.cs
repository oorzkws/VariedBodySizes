using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FisheryLib;
using Verse;

namespace VariedBodySizes;

public class TimedCache<T>
{
    private readonly int expiry;
    private readonly Dictionary<int, CacheEntry<T>> internalCache = new Dictionary<int, CacheEntry<T>>();

    public TimedCache(int expiryTime)
    {
        expiry = expiryTime;
    }

    public void Set(Pawn pawn, T value)
    {
        internalCache[pawn.thingIDNumber] = new CacheEntry<T>(value);
    }

    public void Remove(Pawn pawn)
    {
        internalCache.Remove(pawn.thingIDNumber);
    }

    public bool Contains(Pawn pawn)
    {
        return internalCache.ContainsKey(pawn.thingIDNumber);
    }

    public bool TryGet(Pawn pawn, out T value)
    {
        ref var reference = ref internalCache.TryGetReferenceUnsafe(pawn.thingIDNumber);
        if (!Unsafe.IsNullRef(ref reference))
        {
            if (reference.Expired(expiry))
            {
                internalCache.Remove(pawn.thingIDNumber);
                value = default;
                return false;
            }

            value = reference.CachedValue;
            return true;
        }

        value = default;
        return false;
    }

    public T Get(Pawn pawn)
    {
        return internalCache.GetReference(pawn.thingIDNumber).CachedValue;
    }
}