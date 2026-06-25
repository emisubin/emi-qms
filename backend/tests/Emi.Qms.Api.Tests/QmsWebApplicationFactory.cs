using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Tests;

public sealed class QmsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string environment;
    private readonly IReadOnlyDictionary<string, string?>? configuration;
    private readonly bool includeDefaultDevelopmentAuthentication;
    private readonly IIdentityStore? identityStore;

    public TestLogSink Logs { get; } = new();

    public QmsWebApplicationFactory()
        : this(
            "Testing",
            new Dictionary<string, string?>
            {
                ["DevAuthentication:Enabled"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["DevelopmentData:SeedEnabled"] = "false"
            },
            includeDefaultDevelopmentAuthentication: true)
    {
    }

    private QmsWebApplicationFactory(
        string environment,
        IReadOnlyDictionary<string, string?>? configuration = null,
        bool includeDefaultDevelopmentAuthentication = false,
        IIdentityStore? identityStore = null)
    {
        this.environment = environment;
        this.configuration = configuration;
        this.includeDefaultDevelopmentAuthentication = includeDefaultDevelopmentAuthentication;
        this.identityStore = identityStore;
    }

    public static QmsWebApplicationFactory Create(
        string environment,
        IReadOnlyDictionary<string, string?>? configuration = null,
        bool includeDefaultDevelopmentAuthentication = false,
        IIdentityStore? identityStore = null)
    {
        return new QmsWebApplicationFactory(environment, configuration, includeDefaultDevelopmentAuthentication, identityStore);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.Sources.Clear();

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Database:ApplyMigrationsOnStartup"] = "false"
            };

            if (includeDefaultDevelopmentAuthentication)
            {
                values["DevAuthentication:Enabled"] = "true";
            }

            if (configuration is not null)
            {
                foreach (var item in configuration)
                {
                    values[item.Key] = item.Value;
                }
            }

            configBuilder.AddInMemoryCollection(values);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new TestLoggerProvider(Logs));
        });

        if (identityStore is not null)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(service => service.ServiceType == typeof(IIdentityStore));
                services.Remove(descriptor);
                services.AddSingleton(identityStore);
            });
        }
    }
}

public sealed record TestLogEntry(
    LogLevel LogLevel,
    EventId EventId,
    string Category,
    string Message,
    Exception? Exception);

public sealed class TestLogSink
{
    private readonly List<TestLogEntry> entries = [];

    public IReadOnlyList<TestLogEntry> Entries
    {
        get
        {
            lock (entries)
            {
                return entries.ToList();
            }
        }
    }

    public void Add(TestLogEntry entry)
    {
        lock (entries)
        {
            entries.Add(entry);
        }
    }
}

public sealed class TestLoggerProvider(TestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName, sink);
    }

    public void Dispose()
    {
    }
}

public sealed class TestLogger(string categoryName, TestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        sink.Add(new TestLogEntry(logLevel, eventId, categoryName, formatter(state, exception), exception));
    }
}
