namespace Kura.Api.Extensions;

using System.Text.Json;
using Kura.Api.HealthChecks;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// S3D-03: health checks reais, substituindo o antigo <c>HealthController</c>
/// (<c>200</c> incondicional, não consultava nada). Cobre os 3 alvos que a rubrica
/// nomeia: saúde da própria API, conectividade com o Oracle e disponibilidade do
/// serviço externo (Luna).
/// </summary>
public static class HealthCheckExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static IServiceCollection AddKuraHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Item 3 (serviço externo): HttpClient dedicado, timeout curto (~3s) — não
        // reaproveita o client de ILunaTranscricaoService porque aquele não define
        // timeout próprio (herdaria o default de 100s do HttpClient, inaceitável
        // para um healthcheck) nem precisa do header X-API-Key (GET /health da Luna
        // não exige auth inbound — CLAUDE.md confirma que só o webhook Twilio tem
        // verificação de assinatura).
        services.AddHttpClient<LunaHealthCheck>((sp, http) =>
        {
            var baseUrl = configuration["Luna:BaseUrl"]
                ?? throw new InvalidOperationException("Luna:BaseUrl not configured.");
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHealthChecks()
            // Item 1 (saúde da API): confirma que o processo está de pé e servindo
            // requisições — se este delegate roda, o pipeline HTTP funciona.
            .AddCheck("self", () => HealthCheckResult.Healthy("API respondendo."), tags: ["self"])
            // Item 2 (conectividade com o banco): pacote oficial Microsoft
            // (Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore),
            // decisão travada no backlog — não usar pacote community de Oracle.
            // Falha aqui É Unhealthy de propósito (comportamento default do pacote,
            // sem failureStatus customizado): Oracle fora do ar É uma falha real da
            // API, diferente da Luna.
            .AddDbContextCheck<KuraDbContext>("oracle", tags: ["oracle"])
            // Item 3 (serviço externo): ver LunaHealthCheck — failureStatus
            // Degraded aqui é defesa em profundidade (o check já nunca lança e já
            // nunca devolve Unhealthy por conta própria; isto cobre qualquer
            // exceção que escape do try/catch interno).
            .AddCheck<LunaHealthCheck>("luna", failureStatus: HealthStatus.Degraded, tags: ["luna"])
            // S4-NET-02: MongoDB (laudos) e dependência opcional da API: fora do ar ou não
            // configurado = Degraded (200), nunca Unhealthy.
            .AddCheck<MongoHealthCheck>("mongo", failureStatus: HealthStatus.Degraded, tags: ["mongo"]);

        return services;
    }

    /// <summary>
    /// Item 4: substitui o <c>HealthController</c>. Não sobrescreve
    /// <see cref="HealthCheckOptions.ResultStatusCodes"/> — o default do ASP.NET Core
    /// já mapeia <see cref="HealthStatus.Healthy"/>/<see cref="HealthStatus.Degraded"/>
    /// para HTTP 200 e só <see cref="HealthStatus.Unhealthy"/> para 503. Isso é
    /// exigido pela decisão do item 3 (Luna Degraded não pode virar 503) — ver
    /// comentário em <see cref="LunaHealthCheck"/> sobre a armadilha de acoplamento
    /// com o healthcheck do container Docker.
    /// </summary>
    public static WebApplication MapKuraHealthChecks(this WebApplication app, string pattern = "/health")
    {
        app.MapHealthChecks(pattern, new HealthCheckOptions
        {
            ResponseWriter = WriteResponse,
        });

        return app;
    }

    private static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
            }),
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
