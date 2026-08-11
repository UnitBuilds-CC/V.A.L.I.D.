using Xunit;
using Valid;
using Valid.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Valid.Testing;

/// <summary>
/// Tests for ValidServiceExtensions DI registration and ValidServiceOptions.
/// </summary>
public class ValidServiceExtensionsTests
{
    [Fact]
    public void AddValid_NoConfig_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddValid();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddValid_WithAction_ConfiguresOutboxOptions()
    {
        var services = new ServiceCollection();
        services.AddValid(options =>
        {
            options.Outbox.ConnectionString = "Data Source=test.db";
            options.Outbox.EncryptionKey = "test-key-12345678901234567890";
            options.Outbox.DefaultBatchSize = 25;
        });

        var provider = services.BuildServiceProvider();
        var outboxOptions = provider.GetRequiredService<IOptions<ValidOutboxOptions>>().Value;

        Assert.Equal("Data Source=test.db", outboxOptions.ConnectionString);
        Assert.Equal("test-key-12345678901234567890", outboxOptions.EncryptionKey);
        Assert.Equal(25, outboxOptions.DefaultBatchSize);
    }

    [Fact]
    public void AddValid_WithAction_ConfiguresRegistryOptions()
    {
        var services = new ServiceCollection();
        services.AddValid(options =>
        {
            options.Registry.MaxCapacity = 5000;
        });

        var provider = services.BuildServiceProvider();
        var registryOptions = provider.GetRequiredService<IOptions<ValidRegistryOptions>>().Value;

        Assert.Equal(5000, registryOptions.MaxCapacity);
    }

    [Fact]
    public void AddValid_DefaultOptions_MatchExpectedDefaults()
    {
        var services = new ServiceCollection();
        services.AddValid();

        var provider = services.BuildServiceProvider();
        var outboxOptions = provider.GetRequiredService<IOptions<ValidOutboxOptions>>().Value;
        var registryOptions = provider.GetRequiredService<IOptions<ValidRegistryOptions>>().Value;

        Assert.Equal("Data Source=valid_outbox.db", outboxOptions.ConnectionString);
        Assert.Null(outboxOptions.EncryptionKey);
        Assert.Equal(10, outboxOptions.DefaultBatchSize);
        Assert.Equal(10_000, registryOptions.MaxCapacity);
    }

    [Fact]
    public void ValidServiceOptions_HasDefaultSubOptions()
    {
        var options = new ValidServiceOptions();
        Assert.NotNull(options.Outbox);
        Assert.NotNull(options.Registry);
        Assert.Equal("Data Source=valid_outbox.db", options.Outbox.ConnectionString);
        Assert.Equal(10_000, options.Registry.MaxCapacity);
    }
}
