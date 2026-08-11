using Xunit;
using Valid.Queue;
using Valid.Configuration;
using Valid.Mcp;

namespace Valid.Testing;

/// <summary>
/// Tests for IOptions configuration, schema migration, and registry configuration.
/// </summary>
public class ConfigurationTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"valid_cfg_{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        return path;
    }

    // ── ValidOutboxOptions ──────────────────────────────────────────

    [Fact]
    public void OutboxOptions_DefaultValues()
    {
        var opts = new ValidOutboxOptions();
        Assert.Equal("Data Source=valid_outbox.db", opts.ConnectionString);
        Assert.Null(opts.EncryptionKey);
        Assert.Equal(10, opts.DefaultBatchSize);
    }

    [Fact]
    public async Task Outbox_ConstructorFromOptions_Works()
    {
        var dbPath = NewDbPath();
        var options = new ValidOutboxOptions
        {
            ConnectionString = $"Data Source={dbPath}",
            EncryptionKey = "test-key"
        };

        using var outbox = new SqliteOutbox(options);
        await outbox.EnqueueAsync("hello-options");
        var items = await outbox.DequeueAsync<string>();
        Assert.Single(items);
        Assert.Equal("hello-options", items[0]);
    }

    // ── Schema Migration ────────────────────────────────────────────

    [Fact]
    public void SchemaMigration_CreatesSchemaVersionsTable()
    {
        var dbPath = NewDbPath();
        using var outbox = new SqliteOutbox($"Data Source={dbPath}");
        
        // The SchemaVersions table should exist after initialization
        // We verify by successfully creating a second outbox on the same DB
        using var outbox2 = new SqliteOutbox($"Data Source={dbPath}");
        // If schema migration didn't work, this would throw
    }

    [Fact]
    public async Task SchemaMigration_PreservesDataAcrossReopen()
    {
        var dbPath = NewDbPath();
        
        // First session: enqueue data
        using (var outbox1 = new SqliteOutbox($"Data Source={dbPath}"))
        {
            await outbox1.EnqueueAsync("persistent-data");
        }

        // Second session: data should still be there
        using (var outbox2 = new SqliteOutbox($"Data Source={dbPath}"))
        {
            var items = await outbox2.DequeueAsync<string>();
            Assert.Single(items);
            Assert.Equal("persistent-data", items[0]);
        }
    }

    // ── ValidRegistryOptions ────────────────────────────────────────

    [Fact]
    public void RegistryOptions_DefaultMaxCapacity()
    {
        var opts = new ValidRegistryOptions();
        Assert.Equal(10_000, opts.MaxCapacity);
    }

    [Fact]
    public void Registry_Configure_SetsCapacity()
    {
        var opts = new ValidRegistryOptions { MaxCapacity = 5 };
        ValidObjectRegistry.Configure(opts);

        // Register more than 5 items — oldest should be evicted
        var ids = new List<string>();
        try
        {
            for (int i = 0; i < 8; i++)
            {
                var m = new Models.BasicTestModel();
                var id = $"cfg-test-{i}";
                ids.Add(id);
                ValidObjectRegistry.Register(id, m);
            }

            // Registry should have at most 5 items
            Assert.True(ValidObjectRegistry.Count <= 5);
        }
        finally
        {
            // Clean up: remove any remaining test entries
            foreach (var id in ids)
                ValidObjectRegistry.Unregister(id);

            // Reset to default capacity
            ValidObjectRegistry.Configure(new ValidRegistryOptions());
        }
    }

    // ── ValidServiceOptions ──────────────────────────────────────────

    [Fact]
    public void ValidServiceOptions_HasDefaults()
    {
        var opts = new ValidServiceOptions();
        Assert.NotNull(opts.Outbox);
        Assert.NotNull(opts.Registry);
        Assert.Equal(10, opts.Outbox.DefaultBatchSize);
        Assert.Equal(10_000, opts.Registry.MaxCapacity);
    }
}
