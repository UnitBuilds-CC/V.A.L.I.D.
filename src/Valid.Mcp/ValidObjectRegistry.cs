using System.Collections.Concurrent;

namespace Valid.Mcp;

/// <summary>
/// Global registry of live IValidObject instances available for MCP inspection.
/// Objects are registered at runtime (e.g., by VavidComponentBase or manually).
/// </summary>
public static class ValidObjectRegistry
{
    private static readonly ConcurrentDictionary<string, IValidObject> _instances = new();

    /// <summary>
    /// Register a live object instance for MCP access.
    /// </summary>
    public static void Register(string instanceId, IValidObject obj)
    {
        _instances[instanceId] = obj;
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
}
