using System.Collections.Concurrent;
using Valid.Configuration;

namespace Valid.Mcp;

/// <summary>
/// Global registry of live IValidObject instances available for MCP inspection.
/// Objects are registered at runtime (e.g., by VavidComponentBase or manually).
/// Uses a bounded capacity with LRU-style eviction to prevent memory leaks.
/// </summary>
public static class ValidObjectRegistry
{
    private static readonly ConcurrentDictionary<string, IValidObject> _instances = new();
    private static readonly ConcurrentQueue<string> _insertionOrder = new();
    private static int _maxCapacity = 10_000;

    /// <summary>
    /// Configure the registry capacity from <see cref="ValidRegistryOptions"/>.
    /// Call once at startup before registering objects.
    /// </summary>
    public static void Configure(ValidRegistryOptions options)
    {
        _maxCapacity = options.MaxCapacity > 0 ? options.MaxCapacity : 10_000;
    }

    /// <summary>
    /// Register a live object instance for MCP access.
    /// If the registry exceeds capacity, the oldest entries are evicted.
    /// </summary>
    public static void Register(string instanceId, IValidObject obj)
    {
        _instances[instanceId] = obj;
        _insertionOrder.Enqueue(instanceId);

        // Evict oldest entries if over capacity
        while (_instances.Count > _maxCapacity && _insertionOrder.TryDequeue(out var oldest))
        {
            _instances.TryRemove(oldest, out _);
        }
    }

    /// <summary>
    /// Unregister an object when it is disposed or no longer tracked.
    /// </summary>
    public static void Unregister(string instanceId)
    {
        _instances.TryRemove(instanceId, out _);
    }

    /// <summary>
    /// Get a specific instance by ID.
    /// </summary>
    public static IValidObject? Get(string instanceId)
    {
        _instances.TryGetValue(instanceId, out var obj);
        return obj;
    }

    /// <summary>
    /// Get all registered instances.
    /// </summary>
    public static IReadOnlyDictionary<string, IValidObject> GetAll() => _instances;

    /// <summary>
    /// Get a snapshot of all instance IDs and their type names.
    /// </summary>
    public static Dictionary<string, string> GetManifest()
    {
        return _instances.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetType().Name
        );
    }

    /// <summary>
    /// Returns the current number of registered instances.
    /// </summary>
    public static int Count => _instances.Count;
}
