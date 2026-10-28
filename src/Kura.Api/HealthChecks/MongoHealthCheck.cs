namespace Kura.Api.HealthChecks;

using Kura.Infrastructure.Mongo;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// S4-NET-02: saúde do MongoDB (comando <c>ping</c>, timeout de 3 s). Espelha o
/// <see cref="LunaHealthCheck"/>: o Mongo é dependência de uma feature (laudos), não da API
/// inteira; fora do ar ou não configurado devolve <see cref="HealthStatus.Degraded"/> (HTTP
/// 200), nunca <see cref="HealthStatus.Unhealthy"/>, e o check nunca lança. A descrição não
/// carrega URI nem credencial.
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly MongoContext _mongo;
    private readonly ILogger<MongoHealthCheck> _logger;

    public MongoHealthCheck(MongoContext mongo, ILogger<MongoHealthCheck> logger)
    {
        _mongo = mongo;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_mongo.Configurado)
            return HealthCheckResult.Degraded("MongoDB não configurado.");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            await _mongo.PingAsync(cts.Token);
            return HealthCheckResult.Healthy("MongoDB respondeu ao ping.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("MongoDB indisponível no health check ({TipoExcecao}).", ex.GetType().Name);
            return HealthCheckResult.Degraded("MongoDB indisponível ou não respondeu a tempo.");
        }
    }
}
