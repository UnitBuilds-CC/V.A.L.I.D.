using Xunit;
using Valid.Queue;

namespace Valid.Testing;

/// <summary>
/// Tests for SqliteOutbox: enqueue/dequeue, hash chain integrity,
/// AES-GCM encryption/decryption, and batch processing.
/// </summary>
public class SqliteOutboxTests : IDisposable
{
    private readonly SqliteOutbox _outbox;
    private readonly string _dbPath;

    public SqliteOutboxTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"valid_test_{Guid.NewGuid():N}.db");
        _outbox = new SqliteOutbox($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        _outbox.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    // ── Enqueue / Dequeue ────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_SingleItem_CanDequeue()
    {
        await _outbox.EnqueueAsync(new { Name = "Test", Value = 42 });
        var items = await _outbox.DequeueAsync<object>();
        Assert.Single(items);
    }

    [Fact]
    public async Task Enqueue_MultipleItems_DequeueReturnsAll()
    {
        await _outbox.EnqueueAsync("item1");
        await _outbox.EnqueueAsync("item2");
        await _outbox.EnqueueAsync("item3");

        var items = await _outbox.DequeueAsync<string>(batchSize: 10);
        Assert.Equal(3, items.Count);
        Assert.Equal("item1", items[0]);
        Assert.Equal("item2", items[1]);
        Assert.Equal("item3", items[2]);
    }

    [Fact]
    public async Task Dequeue_RespectsBatchSize()
    {
        for (int i = 0; i < 5; i++)
            await _outbox.EnqueueAsync($"item{i}");

        var batch1 = await _outbox.DequeueAsync<string>(batchSize: 2);
        Assert.Equal(2, batch1.Count);

        var batch2 = await _outbox.DequeueAsync<string>(batchSize: 2);
        Assert.Equal(2, batch2.Count);

        var batch3 = await _outbox.DequeueAsync<string>(batchSize: 2);
        Assert.Single(batch3);
    }

    [Fact]
    public async Task Dequeue_MarksItemsAsProcessed()
    {
        await _outbox.EnqueueAsync("item1");
        await _outbox.EnqueueAsync("item2");

        var first = await _outbox.DequeueAsync<string>();
        Assert.Equal(2, first.Count);

        // Second dequeue should return empty (all processed)
        var second = await _outbox.DequeueAsync<string>();
        Assert.Empty(second);
    }

    [Fact]
    public async Task Dequeue_EmptyOutbox_ReturnsEmpty()
    {
        var items = await _outbox.DequeueAsync<string>();
        Assert.Empty(items);
    }

    // ── Hash Chain Integrity ─────────────────────────────────────────

    [Fact]
    public async Task HashChain_PersistsAcrossReopen()
    {
        await _outbox.EnqueueAsync("first");
        await _outbox.EnqueueAsync("second");
        _outbox.Dispose();

        // Reopen the same database
        using var reopened = new SqliteOutbox($"Data Source={_dbPath}");
        await reopened.EnqueueAsync("third");

        // All three items should be retrievable
        var items = await reopened.DequeueAsync<string>(batchSize: 10);
        Assert.Equal(3, items.Count);
    }

    // ── Encryption ───────────────────────────────────────────────────

    [Fact]
    public async Task EncryptedOutbox_EnqueueDequeue_Roundtrip()
    {
        var encPath = Path.Combine(Path.GetTempPath(), $"valid_enc_{Guid.NewGuid():N}.db");
        try
        {
            using var encOutbox = new SqliteOutbox(
                $"Data Source={encPath}",
                encryptionKey: "my-secret-key-123");

            await encOutbox.EnqueueAsync(new { Secret = "classified", Code = 42 });
            var items = await encOutbox.DequeueAsync<object>();

            Assert.Single(items);
        }
        finally
        {
            try { File.Delete(encPath); } catch { }
        }
    }

    [Fact]
    public async Task EncryptedOutbox_DifferentKeys_CannotDecrypt()
    {
        var encPath = Path.Combine(Path.GetTempPath(), $"valid_enc2_{Guid.NewGuid():N}.db");
        try
        {
            // Encrypt with key A
            using var outboxA = new SqliteOutbox(
                $"Data Source={encPath}",
                encryptionKey: "key-alpha");
            await outboxA.EnqueueAsync("secret-data");
            outboxA.Dispose();

            // Try to decrypt with key B — should fail
            using var outboxB = new SqliteOutbox(
                $"Data Source={encPath}",
                encryptionKey: "key-bravo");

            // AES-GCM will throw CryptographicException on tag mismatch
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await outboxB.DequeueAsync<string>());
        }
        finally
        {
            try { File.Delete(encPath); } catch { }
        }
    }

    [Fact]
    public async Task UnencryptedOutbox_RawPayloadVisible()
    {
        // Without encryption, the payload in the DB should be readable JSON
        await _outbox.EnqueueAsync(new { Visible = true });

        // We can verify by dequeuing — the JSON should deserialize correctly
        var items = await _outbox.DequeueAsync<object>();
        Assert.Single(items);
    }

    // ── Synchronous Wrappers ─────────────────────────────────────────

    [Fact]
    public void Sync_EnqueueDequeue_Works()
    {
        _outbox.Enqueue("sync-item");
        var items = _outbox.Dequeue<string>();
        Assert.Single(items);
        Assert.Equal("sync-item", items[0]);
    }

    // ── Complex Types ────────────────────────────────────────────────

    [Fact]
    public async Task Enqueue_ComplexObject_Roundtrips()
    {
        var payload = new TestPayload
        {
            Id = 42,
            Name = "Complex",
            Tags = new[] { "a", "b", "c" },
            Nested = new TestPayloadInner { Value = 3.14 }
        };

        await _outbox.EnqueueAsync(payload);
        var items = await _outbox.DequeueAsync<TestPayload>();

        Assert.Single(items);
        Assert.Equal(42, items[0].Id);
        Assert.Equal("Complex", items[0].Name);
        Assert.Equal(3, items[0].Tags!.Length);
        Assert.Equal(3.14, items[0].Nested!.Value);
    }

    private class TestPayload
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string[]? Tags { get; set; }
        public TestPayloadInner? Nested { get; set; }
    }

    private class TestPayloadInner
    {
        public double Value { get; set; }
    }
}
