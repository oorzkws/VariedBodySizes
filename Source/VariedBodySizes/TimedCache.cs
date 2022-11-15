using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;

namespace VariedBodySizes;
readonly struct CacheEntry<T>
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

public class TimedCache<T>
{
    private readonly int expiry;
    private readonly Dictionary<Pawn, LinkedListNode<CacheEntry<T>>> internalCache = new();
    private readonly LinkedList<CacheEntry<T>> expiryList = new();
    public TimedCache(int expiryTime) => this.expiry = expiryTime;

    public T SetAndReturn(Pawn key, T value)
    {
        Set(key, value);
        return internalCache[key].Value.CachedValue;
    }

    public void Set(Pawn key, T value)
    {
        CheckFirstExpiry();
        
        var node = new LinkedListNode<CacheEntry<T>>(new CacheEntry<T>(value, key));
        internalCache.Add(key, node);
        expiryList.AddLast(node);
    }

    public bool TryGet(Pawn key, out T value)
    {
        if (!ContainsKey(key))
        {
            value = default;
            return false;
        }

        value = internalCache[key].Value.CachedValue;
        return true;
    }
    public T Get(Pawn key)
    {
        return ContainsKey(key) ? internalCache[key].Value.CachedValue : default;
    }

    public bool ContainsKey(Pawn key)
    {
        CheckKeyExpiry(key);
        return internalCache.ContainsKey(key);
    }

    private void CheckKeyExpiry(Pawn key)
    {
        if (internalCache.ContainsKey(key) && internalCache[key].Value.Expired(Find.TickManager.TicksGame, expiry))
            internalCache.Remove(key);
    }
    private void CheckFirstExpiry()
    {
        var first = expiryList.First;
        if (first == null || !first.Value.Expired(Find.TickManager.TicksGame, expiry)) return;
        expiryList.RemoveFirst();
        internalCache.Remove(first.Value.Owner);
    }
}