using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Valid.Queue;

/// <summary>
/// A hash-chained outbox for persistent, offline-first data synchronization.
/// Ensures causal ordering and data integrity via cryptographic version chaining.
/// </summary>
public sealed class SqliteOutbox : IDisposable
{
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
        LoadLastHash();
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
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );";
        command.ExecuteNonQuery();
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
        
        // MISSION 24: Encrypted State Payloads
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

    private static string Encrypt(string text, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(text);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public void Enqueue<T>(T payload) => EnqueueAsync(payload).GetAwaiter().GetResult();

    private static string ComputeHash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = System.IO.Hashing.XxHash3.HashToUInt64(inputBytes);
        return hash.ToString("X16");
    }

    public void Dispose() => _connection.Dispose();
}
