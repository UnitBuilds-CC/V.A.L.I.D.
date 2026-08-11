using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Valid.Configuration;

namespace Valid.Queue;

/// <summary>
/// A hash-chained outbox for persistent, offline-first data synchronization.
/// Ensures causal ordering and data integrity via SHA-256 version chaining
/// and AES-GCM authenticated encryption.
/// </summary>
public sealed class SqliteOutbox : IDisposable
{
    /// <summary>
    /// Current schema version. Increment when making breaking changes to the table layout.
    /// </summary>
    private const int SchemaVersion = 1;

    private readonly SqliteConnection _connection;
    private string _lastHash = "INITIAL_BLOCK";
    private readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);
    private readonly byte[]? _encryptionKey;

    public SqliteOutbox(string connectionString = "Data Source=valid_outbox.db", string? encryptionKey = null)
    {
        if (encryptionKey != null) _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeDatabase();
        MigrateSchema();
        LoadLastHash();
    }

    /// <summary>
    /// Creates an outbox from <see cref="ValidOutboxOptions"/> (for use with IOptions pattern).
    /// </summary>
    public SqliteOutbox(ValidOutboxOptions options)
        : this(options.ConnectionString, options.EncryptionKey)
    {
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Outbox (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Payload TEXT NOT NULL,
                Hash TEXT NOT NULL,
                PrevHash TEXT NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                Processed INTEGER NOT NULL DEFAULT 0
            );";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Schema migration system. Checks the current schema version and applies
    /// migrations sequentially. Uses a SchemaVersions table to track applied migrations.
    /// </summary>
    private void MigrateSchema()
    {
        using var createVersionTable = _connection.CreateCommand();
        createVersionTable.CommandText = @"
            CREATE TABLE IF NOT EXISTS SchemaVersions (
                Version INTEGER PRIMARY KEY,
                AppliedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );";
        createVersionTable.ExecuteNonQuery();

        var currentVersion = GetCurrentSchemaVersion();
        if (currentVersion >= SchemaVersion) return;

        // Future migrations go here, each gated by version check:
        // if (currentVersion < 2) { MigrateV2(); }

        // Mark current version as applied
        if (currentVersion == 0)
        {
            using var insertCmd = _connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO SchemaVersions (Version) VALUES (@v)";
            insertCmd.Parameters.AddWithValue("@v", SchemaVersion);
            insertCmd.ExecuteNonQuery();
        }
    }

    private int GetCurrentSchemaVersion()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT MAX(Version) FROM SchemaVersions";
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private void LoadLastHash()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Hash FROM Outbox ORDER BY Id DESC LIMIT 1";
        var result = command.ExecuteScalar();
        if (result != null) _lastHash = (string)result;
    }

    public async System.Threading.Tasks.Task EnqueueAsync<T>(T payload)
    {
        // Compute JSON and Hash outside the lock to prevent blocking threads with CPU math
        var json = JsonSerializer.Serialize(payload);
        
        // Encrypted State Payloads (AES-GCM authenticated encryption)
        if (_encryptionKey != null)
        {
            json = Encrypt(json, _encryptionKey);
        }

        await _semaphore.WaitAsync();
        try
        {
            var hash = ComputeHash(json + _lastHash);

            using var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO Outbox (Payload, Hash, PrevHash) VALUES (@p, @h, @ph)";
            command.Parameters.AddWithValue("@p", json);
            command.Parameters.AddWithValue("@h", hash);
            command.Parameters.AddWithValue("@ph", _lastHash);
            await command.ExecuteNonQueryAsync();

            _lastHash = hash;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Encrypts plaintext using AES-GCM authenticated encryption (no separate HMAC needed).
    /// Output format: [12-byte nonce][16-byte tag][ciphertext]
    /// </summary>
    private static string Encrypt(string text, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); // 96-bit nonce for AES-GCM
        var plainBytes = Encoding.UTF8.GetBytes(text);
        var tag = new byte[16]; // 128-bit authentication tag
        var cipherBytes = new byte[plainBytes.Length];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Combine: nonce + tag + ciphertext
        var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts AES-GCM encrypted text.
    /// Input format: [12-byte nonce][16-byte tag][ciphertext]
    /// </summary>
    private static string Decrypt(string encryptedBase64, byte[] key)
    {
        var fullCipher = Convert.FromBase64String(encryptedBase64);

        var nonce = new byte[12];
        var tag = new byte[16];
        var cipherBytes = new byte[fullCipher.Length - 12 - 16];

        Buffer.BlockCopy(fullCipher, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(fullCipher, nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(fullCipher, nonce.Length + tag.Length, cipherBytes, 0, cipherBytes.Length);

        var plainBytes = new byte[cipherBytes.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Dequeues up to <paramref name="batchSize"/> unprocessed entries from the outbox.
    /// Returns the deserialized payloads. Marks entries as processed after successful dequeue.
    /// </summary>
    public async System.Threading.Tasks.Task<IReadOnlyList<T>> DequeueAsync<T>(int batchSize = 10)
    {
        await _semaphore.WaitAsync();
        try
        {
            var results = new List<T>();

            using var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id, Payload FROM Outbox WHERE Processed = 0 ORDER BY Id ASC LIMIT @limit";
            selectCmd.Parameters.AddWithValue("@limit", batchSize);

            using var reader = await selectCmd.ExecuteReaderAsync();
            var ids = new List<long>();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var payloadJson = reader.GetString(1);

                if (_encryptionKey != null)
                {
                    payloadJson = Decrypt(payloadJson, _encryptionKey);
                }

                var payload = JsonSerializer.Deserialize<T>(payloadJson);
                if (payload != null)
                {
                    results.Add(payload);
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                using var markCmd = _connection.CreateCommand();
                var idList = string.Join(",", ids);
                markCmd.CommandText = $"UPDATE Outbox SET Processed = 1 WHERE Id IN ({idList})";
                await markCmd.ExecuteNonQueryAsync();
            }

            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Synchronous dequeue for convenience. Prefer <see cref="DequeueAsync{T}"/> in async contexts.
    /// </summary>
    public IReadOnlyList<T> Dequeue<T>(int batchSize = 10) => DequeueAsync<T>(batchSize).GetAwaiter().GetResult();

    /// <summary>
    /// Synchronous enqueue for convenience. Prefer <see cref="EnqueueAsync{T}"/> in async contexts.
    /// </summary>
    public void Enqueue<T>(T payload) => EnqueueAsync(payload).GetAwaiter().GetResult();

    /// <summary>
    /// Computes a SHA-256 hash for chain integrity verification.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hash);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _semaphore.Dispose();
    }
}
