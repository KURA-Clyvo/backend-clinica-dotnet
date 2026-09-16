namespace Kura.Api.Extensions;

using Kura.CrossCutting.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// S3D-04/S3D-04b: tracing entre camadas + métricas de desempenho via OpenTelemetry, com
/// exporter Console — decisão travada no backlog (nunca Application Insights, custo
/// e viola free-first do <c>CLAUDE.md</c>).
///
/// Cobertura real, medida via NuGet antes de escrever este arquivo (ver
/// task-S3D-04-report.md/task-S3D-04b-report.md para os comandos exatos):
/// - <c>OpenTelemetry.Extensions.Hosting</c> 1.18.0 — estável.
/// - <c>OpenTelemetry.Instrumentation.AspNetCore</c> 1.18.0 — estável. Produz o span de
///   BORDA por requisição HTTP (entrada) e as métricas de duração/contagem por status
///   que a rubrica pede literalmente.
/// - 🆕 S3D-04b — <c>KuraActivitySource</c> (<c>Kura.CrossCutting.Observability</c>,
///   <c>System.Diagnostics.ActivitySource</c>, BCL pura, zero dependência nova): cobre
///   o que o G2 da S3D-04 mediu como faltante — hierarquia PAI/FILHO real entre as
///   camadas do próprio projeto (Application → Infrastructure), não só a borda HTTP.
///   Registrado abaixo via <c>.AddSource(KuraActivitySource.NomeFonte)</c>. Instrumenta
///   deliberadamente só um fluxo representativo (<c>AgendaService.GetAgendaAsync</c> →
///   <c>AgendaReadRepository.GetByIntervaloAsync</c>), não o projeto inteiro — ver
///   task-S3D-04b-report.md para o porquê da escolha.
/// - <c>OpenTelemetry.Instrumentation.EntityFrameworkCore</c> — continua NÃO incluído.
///   Toda a série de versões publicada no NuGet (até 1.18.0-beta.1, a mesma major da
///   linha estável acima) é prerelease; não existe nenhuma tag estável desse pacote.
///   Incluir um pacote prerelease para "cobrir a camada de dados" seria fingir uma
///   garantia que o próprio mantenedor não dá. Mesmo se estivesse estável, produziria
///   um span de COMANDO SQL (camada de dados), não de fronteira arquitetural — é
///   exatamente o motivo pelo qual o <c>KuraActivitySource</c> acima é a escolha certa
///   para "entre camadas" no sentido que a rubrica pede.
/// - 🆕 S3D-04b — <c>OpenTelemetry.Instrumentation.Http</c> 1.18.0 — estável (medido via
///   NuGet flatcontainer antes de incluir, ver relatório). Cobre a BORDA DE SAÍDA: a
///   chamada HTTP do healthcheck da Luna (S3D-03) e qualquer outra chamada via
///   <c>HttpClient</c> agora vira span, em vez de invisível como o G2 da S3D-04
///   registrou (achado Minor #3).
/// - 🆕 S3D-04c — <c>ConfigureResource(r =&gt; r.AddService(...))</c>: antes desta task todo
///   span/métrica saía com <c>service.name: unknown_service:Kura.Api</c>, o fallback que a
///   OTel SDK inventa a partir do nome do processo quando ninguém declara o recurso. O
///   recurso é declarado UMA vez, no nível do <c>AddOpenTelemetry()</c>, e vale para as duas
///   pilhas — é a forma idiomática da OTel para recurso compartilhado entre providers. A
///   alternativa <c>SetResourceBuilder(...)</c> por provider foi ACUSADA, na 1ª volta desta
///   task, de quebrar o parentesco dos spans entre camadas; essa causalidade foi DERRUBADA
///   por medição posterior. Ver o comentário no corpo de <c>AddKuraObservability</c> e
///   task-S3D-04c-investigacao-F2.md.
/// - Exporter Prometheus (<c>OpenTelemetry.Exporter.Prometheus.AspNetCore</c>) — NÃO
///   incluído pelo mesmo motivo: toda a série é prerelease (até 1.18.0-beta.1). O
///   Console exporter sozinho já atende ao critério de aceite (prova de emissão real
///   no log).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// S3D-04c: valor de <c>service.name</c> nos spans e métricas exportados. Sem isto a
    /// OTel SDK cai no fallback <c>unknown_service:&lt;nome do processo&gt;</c>.
    /// </summary>
    private const string NomeServico = "Kura.Api";

    /// <summary>Valor de <c>service.version</c> — mesma versão declarada no
    /// <see cref="KuraActivitySource.Instancia"/>.</summary>
    private const string VersaoServico = "1.0.0";

    public static IServiceCollection AddKuraObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            // S3D-04c — ConfigureResource NO NÍVEL DO BUILDER: declara o Resource uma única
            // vez e ele vale para as duas pilhas (tracing e métricas). É a forma idiomática da
            // OTel para recurso compartilhado, e evita duplicar a declaração em cada provider.
            //
            // Histórico, porque este arquivo já afirmou o CONTRÁRIO e alguém pode ter lido:
            // a 1ª volta desta task usava .SetResourceBuilder(...) dentro de WithTracing/
            // WithMetrics e foi reprovada no G2 sob a acusação de que a instância da pilha de
            // MÉTRICAS desparentava os spans entre camadas. ESSA CAUSALIDADE NÃO SE SUSTENTOU.
            // A investigação posterior mediu a configuração acusada em runtime — 117
            // requisições reais com ela ativa (92 locais + 25 em container; mais 10 com a
            // configuração atual, totalizando as 127 que a investigação reporta), Production e
            // Development, cold start, sequencial e concorrente — e a hierarquia veio correta
            // em 100% delas, com controle positivo em cada aparelho provando que o defeito
            // seria visto se existisse. O
            // desparentamento observado na 1ª volta é real no log dela, mas foi reproduzido de
            // forma controlada por OUTRA causa: binário obsoleto de Kura.Infrastructure
            // carregado por `dotnet run --no-build`. Detalhe e limites em
            // task-S3D-04c-investigacao-F2.md §6 e §7.
            //
            // Portanto: trocar para SetResourceBuilder não é proibido por medição nenhuma — é
            // apenas menos idiomático e sem cobertura. ConfigureResource fica, e está travado
            // por teste: remover esta chamada faz ObservabilityExtensionsTests falhar
            // (a asserção de service.name).
            //
            // ⚠️ Armadilha de medição deste repo, para quem for reproduzir qualquer coisa aqui:
            // tests/Kura.Infrastructure.Tests referencia Kura.Api, então um `dotnet test` com
            // mutação aplicada REESCREVE src/Kura.Api/bin/.../Kura.Infrastructure.dll — e
            // `git checkout` do fonte NÃO desfaz o binário. Medir com --no-build depois disso
            // carrega código mutado enquanto service.name/version dizem que está tudo certo.
            .ConfigureResource(r => r.AddService(
                serviceName: NomeServico,
                serviceVersion: VersaoServico))
            .WithTracing(t => t
                .AddSource(KuraActivitySource.NomeFonte)
                // LU-16 G4 (achado A4, BLOQUEANTE): AddAspNetCoreInstrumentation() marca a
                // tag `url.path` com context.Request.Path CRU no span de servidor — o
                // AddConsoleExporter() abaixo IMPRIME essa tag, então GET
                // /api/v1/tutores/telefone/{numero} vazava o telefone do tutor por esta
                // via também (achado, não deduzido: medido em docker logs, linha
                // "url.path: /api/v1/tutores/telefone/<numero>"). EnrichWithHttpRequest
                // reaproveita a MESMA RedigirPathSensivel do ExceptionHandlerMiddleware —
                // sem duplicar a regra — e roda em OnStartActivity, DEPOIS que a
                // instrumentação já setou a tag default, então activity.SetTag aqui
                // SOBRESCREVE o valor cru pelo redigido antes de qualquer exportação.
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.EnrichWithHttpRequest = (activity, request) =>
                        activity.SetTag(
                            "url.path",
                            Kura.Api.Middlewares.ExceptionHandlerMiddleware.RedigirPathSensivel(request.Path));
                })
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
