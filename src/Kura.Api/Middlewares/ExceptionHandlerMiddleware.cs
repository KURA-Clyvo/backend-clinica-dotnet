using Kura.Domain.Exceptions;
using System.Text.Json;

namespace Kura.Api.Middlewares;

/// <summary>
/// Captura exceções não tratadas, registra via ILogger (observabilidade)
/// e devolve resposta RFC 7807 ao cliente.
///
/// NOTA: este middleware NÃO escreve em LOG_ERRO — essa tabela é
/// exclusiva do domínio PL/SQL (rubrica FIAP de Banco). Logs operacionais
/// HTTP vivem em stdout/Serilog/observabilidade externa.
///
/// TASK-67 fix round 1 (Important-1 da revisão): este middleware loga
/// <c>context.Request.Path</c> integralmente. GET /api/v1/tutores/telefone/{numero}
/// (TASK-67) carrega o telefone do tutor **no próprio path**, não no body — então
/// qualquer exceção nesse endpoint (timeout Oracle, NRE, o que for) gravava o telefone
/// cru no log de aplicação, violação direta da restrição de LGPD deste projeto. Fix:
/// <see cref="RedigirPathSensivel(PathString)"/> redige o segmento variável antes de logar. O
/// corpo da resposta HTTP (`problem.title = ex.Message`) nunca incluiu o path, então
/// não precisou de mudança.
///
/// LU-16 G4 (achado A4, BLOQUEANTE): o fix acima só cobria o caminho de EXCEÇÃO — o
/// caminho FELIZ (200/404/... sem exceção, que é o que roda em TODA chamada da Luna)
/// nunca passava por <see cref="RedigirPathSensivel(PathString)"/>. Medido: uma chamada 404 gravava
/// o telefone 4× — na linha de conclusão do <c>UseSerilogRequestLogging</c>
/// (propriedade <c>RequestPath</c>), em "Request starting"/"Request finished" do
/// <c>Microsoft.AspNetCore.Hosting.Diagnostics</c> (propriedade <c>Path</c>) e na tag
/// <c>url.path</c> exportada pelo OpenTelemetry. Nenhuma dessas 3 fontes chama este
/// middleware — a correção não é aqui. Fix real, nos 3 lugares certos:
/// <list type="number">
/// <item><description><see cref="Kura.Api.Extensions.RedigirRequestPathEnricher"/> —
/// enricher global do Serilog (registrado em <c>Program.cs</c>) que reescreve QUALQUER
/// propriedade <c>RequestPath</c>/<c>Path</c> de QUALQUER <c>LogEvent</c>, reaproveitando
/// esta mesma <see cref="RedigirPathSensivel(string?)"/>. Cobre a linha de conclusão do Serilog E
/// a scope ambiente que o ASP.NET Core hosting empurra (<c>HostingLogScope</c>) — que
/// carrega <c>RequestPath</c> em TODO log emitido durante a requisição, inclusive
/// "Executing endpoint", achado ao escrever o teste desta task, não previsto no brief.</description></item>
/// <item><description><c>Program.cs</c>: <c>MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Warning)</c>
/// — suprime "Request starting"/"Request finished" (nível Information sempre, então some
/// para todo status), tornando efetivo o <c>appsettings.json</c> que já declarava essa
/// intenção sem nunca valer para o Serilog.</description></item>
/// <item><description><c>ObservabilityExtensions.AddKuraObservability</c>:
/// <c>EnrichWithHttpRequest</c> sobrescreve a tag <c>url.path</c> do
/// <c>OpenTelemetry.Instrumentation.AspNetCore</c> antes do <c>AddConsoleExporter()</c>
/// imprimir.</description></item>
/// </list>
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    // Prefixos de rota conhecidos por carregarem PII diretamente no segmento de path
    // (não no body). Lista pequena e explícita de propósito — quem adicionar uma rota
    // nova com PII no path (ex.: .../cpf/{numero}) precisa lembrar de somar aqui.
    private static readonly string[] SegmentosSensiveis = ["/tutores/telefone/"];

    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Redige o segmento variável de rotas conhecidas por carregar PII no path.
    /// "/api/v1/tutores/telefone/5511999990000" vira
    /// "/api/v1/tutores/telefone/{redacted}"; qualquer outro path passa intocado.
    /// </summary>
    public static string RedigirPathSensivel(PathString path) => RedigirPathSensivel(path.Value ?? string.Empty);

    /// <summary>
    /// Overload em <see cref="string"/> puro — usado pelo
    /// <see cref="Kura.Api.Extensions.RedigirRequestPathEnricher"/> (LU-16/A4), que lê o
    /// valor já como <c>string</c> de uma propriedade de <c>LogEvent</c> e não deveria
    /// reconstruir um <see cref="PathString"/> só para chamar a sobrecarga acima (um
    /// valor de propriedade sem "/" no início, ex. um <c>RawTarget</c> com query string
    /// mal formada, faria o construtor de <see cref="PathString"/> lançar).
    /// </summary>
    public static string RedigirPathSensivel(string? path)
    {
        var valor = path ?? string.Empty;
        foreach (var marcador in SegmentosSensiveis)
        {
            var indice = valor.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
            if (indice >= 0)
                return valor[..(indice + marcador.Length)] + "{redacted}";
        }
        return valor;
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var statusCode = ex switch
        {
            EntidadeNaoEncontradaException => StatusCodes.Status404NotFound,
            RegraDeNegocioException => StatusCodes.Status422UnprocessableEntity,
            ConflitoConcorrenciaException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            // FT-03 (backlog KURA_BACKLOG_FOTO_PET.md): o Kestrel lança esta exceção quando
            // o corpo da requisição excede o limite configurado por [RequestSizeLimit]
            // (ex.: POST .../pets/{id}/foto), com `StatusCode` já preenchido pelo próprio
            // framework (413 nesse caso). SEM este case, o switch caía no default (500) —
            // ou seja, um 413 genuíno vindo do Kestrel virava 500 ao passar por este
            // middleware, o oposto do que [RequestSizeLimit] existe para sinalizar.
            Microsoft.AspNetCore.Http.BadHttpRequestException badRequestEx => badRequestEx.StatusCode,
            _ => StatusCodes.Status500InternalServerError
        };

        _logger.Log(
            statusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            ex,
            "Exception caught by middleware. Endpoint={Endpoint} Method={Method} Status={Status} ClinicaId={ClinicaId}",
            RedigirPathSensivel(context.Request.Path),
            context.Request.Method,
            statusCode,
            context.User?.FindFirst("clinicaId")?.Value ?? "ANONYMOUS");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = ex.GetType().Name,
            title = ex.Message,
            status = statusCode,
            traceId = context.TraceIdentifier
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(problem);
        await context.Response.Body.WriteAsync(bytes);
    }
}
