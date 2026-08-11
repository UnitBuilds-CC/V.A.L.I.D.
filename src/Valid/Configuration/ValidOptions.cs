namespace Valid.Configuration;

/// <summary>
/// Configuration options for SqliteOutbox.
/// Bind via IOptions&lt;ValidOutboxOptions&gt; in DI containers.
/// </summary>
public class ValidOutboxOptions
{
    /// <summary>
    /// SQLite connection string. Defaults to "Data Source=valid_outbox.db".
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=valid_outbox.db";

    /// <summary>
    /// Optional encryption key for AES-GCM authenticated encryption.
    /// When set, all payloads are encrypted at rest.
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Default batch size for dequeue operations. Defaults to 10.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 10;
}

/// <summary>
/// Configuration options for the ValidObject MCP registry.
/// Bind via IOptions&lt;ValidRegistryOptions&gt; in DI containers.
/// </summary>
public class ValidRegistryOptions
{
    /// <summary>
    /// Maximum number of objects in the registry before LRU eviction.
    /// Defaults to 10,000.
    /// </summary>
    public int MaxCapacity { get; set; } = 10_000;
}
