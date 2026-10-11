using System.Collections.Concurrent;

namespace EmbyTube.Application;

/// <summary>Small bounded in-memory cache for metadata. Never used for stream URLs.</summary>
public sealed class TtlCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, (TValue Value, DateTimeOffset Expires)> _items = new();
    private readonly int _maxEntries;
    private readonly Func<DateTimeOffset> _now;

    public TtlCache(int maxEntries, Func<DateTimeOffset>? now = null)
    {
        _maxEntries = maxEntries;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public int Count => _items.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (_items.TryGetValue(key, out var entry))
        {
            if (entry.Expires > _now())
            {
                value = entry.Value;
                return true;
            }

            _items.TryRemove(key, out _);
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
            return;

        if (_items.Count >= _maxEntries)
            Evict();

        _items[key] = (value, _now() + ttl);
    }

    public void Clear() => _items.Clear();

    private void Evict()
    {
        var now = _now();
        foreach (var kv in _items)
        {
            if (kv.Value.Expires <= now)
                _items.TryRemove(kv.Key, out _);
        }

        // Still full: drop the entries closest to expiry.
        var overflow = _items.Count - (_maxEntries * 3 / 4);
        if (overflow <= 0)
            return;
        foreach (var kv in _items.OrderBy(kv => kv.Value.Expires).Take(overflow).ToList())
            _items.TryRemove(kv.Key, out _);
    }
}
