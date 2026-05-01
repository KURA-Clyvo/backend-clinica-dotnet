namespace Kura.Infrastructure.Logging;

using System.Globalization;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

public class OracleLogErroSink : ILogEventSink
{
    private const int MaxMensagemLength = 500;
    private const int MaxEndpointLength = 500;
    private const int MaxMetodoLength = 20;
    private const int DefaultStatusCode = 500;

    private readonly IServiceProvider _serviceProvider;

    public OracleLogErroSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Error)
        {
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<KuraDbContext>();

            var endpoint = ReadStringProperty(logEvent, "RequestPath", string.Empty);
            var metodo = ReadStringProperty(logEvent, "RequestMethod", string.Empty);
            var statusCode = ReadIntProperty(logEvent, "StatusCode", DefaultStatusCode);

            var logErro = new LogErro
            {
                DsMensagem = Truncate(logEvent.RenderMessage(), MaxMensagemLength),
                DsStackTrace = logEvent.Exception?.ToString(),
                DtOcorrencia = logEvent.Timestamp.UtcDateTime,
                NrStatusCode = statusCode,
                DsEndpoint = Truncate(endpoint, MaxEndpointLength),
                DsMetodo = Truncate(metodo, MaxMetodoLength),
            };

            dbContext.Set<LogErro>().Add(logErro);
            dbContext.SaveChanges();
        }
        catch
        {
            // A logging sink must never throw or recurse — swallow all exceptions.
        }
    }

    private static string ReadStringProperty(LogEvent e, string key, string fallback)
    {
        if (!e.Properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        var raw = value.ToString();

        // Serilog wraps string scalar values in quotes when calling ToString().
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            raw = raw.Substring(1, raw.Length - 2);
        }

        return raw;
    }

    private static int ReadIntProperty(LogEvent e, string key, int fallback)
    {
        if (!e.Properties.TryGetValue(key, out var value))
        {
            return fallback;
        }

        if (value is ScalarValue scalar && scalar.Value is int intValue)
        {
            return intValue;
        }

        var raw = value.ToString();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            raw = raw.Substring(1, raw.Length - 2);
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}

public static class OracleLogErroSinkExtensions
{
    public static Serilog.LoggerConfiguration OracleLogErro(
        this LoggerSinkConfiguration config,
        IServiceProvider sp)
    {
        return config.Sink(new OracleLogErroSink(sp));
    }
}
