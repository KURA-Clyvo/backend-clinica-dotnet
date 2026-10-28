namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.HealthChecks;
using Kura.Infrastructure.Mongo;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// S4-NET-02: o check <c>mongo</c> nunca lança e nunca devolve Unhealthy (o Mongo é dependência
/// de uma feature, não da API inteira), igual ao check da Luna.
/// </summary>
public class MongoHealthCheckTests
{
    private static MongoHealthCheck Criar(MongoOptions opcoes) =>
        new(new MongoContext(Options.Create(opcoes)), NullLogger<MongoHealthCheck>.Instance);

    [Fact]
    public async Task Sem_connection_string_devolve_Degraded_nao_configurado()
    {
        var check = Criar(new MongoOptions());

        var r = await check.CheckHealthAsync(new HealthCheckContext());

        r.Status.Should().Be(HealthStatus.Degraded);
        r.Description.Should().Contain("não configurado");
    }

    [Fact]
    public async Task Com_mongo_inalcancavel_devolve_Degraded_dentro_do_teto_de_tempo()
    {
        var check = Criar(new MongoOptions
        {
            ConnectionString = "mongodb://usuario:segredo@127.0.0.1:1/kura_docs",
            ServerSelectionTimeoutSeconds = 1,
        });
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        var r = await check.CheckHealthAsync(new HealthCheckContext());

        r.Status.Should().Be(HealthStatus.Degraded);
        r.Description.Should().NotContain("segredo").And.NotContain("mongodb://");
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task Com_mongo_no_ar_devolve_Healthy()
    {
        using var runner = Mongo2Go.MongoDbRunner.Start();
        var check = Criar(new MongoOptions { ConnectionString = runner.ConnectionString });

        var r = await check.CheckHealthAsync(new HealthCheckContext());

        r.Status.Should().Be(HealthStatus.Healthy);
    }
}
