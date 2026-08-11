using Microsoft.Extensions.DependencyInjection;
using Valid.Configuration;

namespace Valid;

/// <summary>
/// Extension methods for registering VALID services in a DI container.
/// </summary>
public static class ValidServiceExtensions
{
    /// <summary>
    /// Adds VALID core services and binds configuration sections.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddValid(options =>
    /// {
    ///     options.Outbox.ConnectionString = "Data Source=my.db";
    ///     options.Outbox.EncryptionKey = "my-secret";
    ///     options.Registry.MaxCapacity = 5000;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddValid(
        this IServiceCollection services,
        Action<ValidServiceOptions>? configure = null)
    {
        var options = new ValidServiceOptions();
        configure?.Invoke(options);

        services.Configure<ValidOutboxOptions>(o =>
        {
            o.ConnectionString = options.Outbox.ConnectionString;
            o.EncryptionKey = options.Outbox.EncryptionKey;
            o.DefaultBatchSize = options.Outbox.DefaultBatchSize;
        });

        services.Configure<ValidRegistryOptions>(o =>
        {
            o.MaxCapacity = options.Registry.MaxCapacity;
        });

        return services;
    }

    /// <summary>
    /// Adds VALID services bound to configuration sections (e.g., from appsettings.json).
    /// </summary>
    public static IServiceCollection AddValid(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<ValidOutboxOptions>(configuration.GetSection("Valid:Outbox"));
        services.Configure<ValidRegistryOptions>(configuration.GetSection("Valid:Registry"));
        return services;
    }
}

/// <summary>
/// Composite options for VALID service registration.
/// </summary>
public class ValidServiceOptions
{
    /// <summary>
    /// Outbox configuration.
    /// </summary>
    public ValidOutboxOptions Outbox { get; set; } = new();

    /// <summary>
    /// Registry configuration.
    /// </summary>
    public ValidRegistryOptions Registry { get; set; } = new();
}
