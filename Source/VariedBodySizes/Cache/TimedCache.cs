using System.Runtime.CompilerServices;

namespace VariedBodySizes;

public class TimedCache<T>(int expiryTime)
{
    private readonly Dictionary<int, CacheEntry<T>> internalCache = new Dictionary<int, CacheEntry<T>>();

    public void Set(Pawn pawn, T value)
    {
        internalCache[pawn.thingIDNumber] = new CacheEntry<T>(value);
    }

    public void Clear()
    {
        internalCache.Clear();
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
            if (reference.Expired(expiryTime))
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