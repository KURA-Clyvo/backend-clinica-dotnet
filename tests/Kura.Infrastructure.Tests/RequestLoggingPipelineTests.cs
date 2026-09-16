namespace Kura.Infrastructure.Tests;

using System.Linq;
using FluentAssertions;
using Kura.Api.Extensions;
using Kura.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// TASK-84: prova de mordida (bite-proof) de que
/// <see cref="RequestPipelineExtensions.UseRequestLoggingAndExceptionHandling"/> —
/// a mesma extensão chamada por <c>Program.cs</c> — produz uma linha de log do
/// Serilog cujo TEXTO e NÍVEL batem com o status HTTP de verdade devolvido ao
/// cliente, para exceções mapeadas (4xx) e não mapeadas (5xx genuíno).
///
/// Antes da TASK-84, <c>ExceptionHandlerMiddleware</c> era registrado ANTES de
/// <c>UseSerilogRequestLogging()</c> em <c>Program.cs</c> — o Serilog logava
/// "responded 500" em nível Error para TODA exceção, mapeada ou não, porque
/// intercepta a exceção antes dela virar resposta 4xx. Ver CLAUDE.md/
/// KURA_BACKLOG_FIX_7.md para o histórico completo (quase reprovou o G4 do FIX_6).
///
/// Esta suíte não usa <c>WebApplicationFactory&lt;Program&gt;</c> porque
/// <c>Program.cs</c> tenta conectar no Oracle logo depois de <c>builder.Build()</c>
/// (checagem de migrations pendentes, com retry de até 10 tentativas / backoff até
/// 60s) — inviável sem Docker/Oracle rodando (estavam fora do ar no momento desta
/// task). Em vez disso, constrói um <see cref="WebApplication"/> mínimo que chama a
/// MESMA extensão de produção usada por <c>Program.cs</c>, isolando exatamente o
/// trecho sob teste (ordem de middleware + Serilog) do bootstrap de infraestrutura
/// não relacionado. Isso NÃO prova o pipeline completo de <c>Program.cs</c> —
/// prova o comportamento real da extensão que <c>Program.cs</c> invoca.
/// </summary>
public class RequestLoggingPipelineTests : IAsyncLifetime
{
    /// <summary>Captura os LogEvent brutos do Serilog, sem passar por sink de texto.</summary>
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly CapturingSink _sink = new();
    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var serilogLogger = new LoggerConfiguration()
            // LU-16 (A4): Information, não Verbose — é o nível real de produção
            // (Serilog cai no default Information porque appsettings.json só declara a
            // seção "Logging", que .ReadFrom.Configuration() do Serilog NUNCA lê; ver
            // comentário em Program.cs). Um harness em Verbose enxergaria linhas que a
            // produção nunca emite (ex.: "All hosts are allowed." do HostFiltering, em
            // nível Trace, que carrega RequestPath cru) — teste "vermelho" por um
            // vazamento que não existe em produção não prova nada sobre o achado A4.
            .MinimumLevel.Information()
            // LU-16 (A4): espelha o MinimumLevel.Override de Program.cs — sem isto, este
            // harness não reproduz o comportamento real e um teste "verde" aqui não
            // provaria nada sobre produção (a mesma armadilha que already exige espelhar
            // Enrich.FromLogContext() abaixo).
            .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Warning)
            // S3D-01: precisa espelhar Program.cs (Enrich.FromLogContext() em Program.cs:22)
            // para que LogContext.PushProperty("TraceId", ...) — empurrado pelo middleware
            // registrado em UseRequestLoggingAndExceptionHandling() — realmente apareça nas
            // propriedades estruturadas dos LogEvent capturados por este harness. Sem esta
            // linha, o middleware roda mas o sink nunca vê a propriedade.
            .Enrich.FromLogContext()
            // LU-16 (A4): espelha o enricher de Program.cs — é ele que prova o achado A4
            // (ver GetTelefone_QualquerStatus_TelefoneNaoApareceEmNenhumLogEvent abaixo).
            .Enrich.With<RedigirRequestPathEnricher>()
            .WriteTo.Sink(_sink)
            .CreateLogger();

        // Serilog.AspNetCore's RequestLoggingMiddleware resolves its logger from the
        // AMBIENT static Serilog.Log.Logger, not from DI's ILogger<T> — confirmed
        // empirically while writing this test (the middleware emitted nothing at all
        // until this line was added, with zero Serilog.Debugging.SelfLog errors).
        // Program.cs's real UseSerilog((ctx, sp, cfg) => ...) delegate overload sets
        // Log.Logger as a side effect by default (preserveStaticLogger defaults to
        // false), so production gets this wiring "for free" — here it must be explicit.
        Log.Logger = serilogLogger;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Host.UseSerilog(serilogLogger, dispose: true);

        var app = builder.Build();

        // Mesma extensão chamada por Program.cs — não uma reimplementação paralela.
        app.UseRequestLoggingAndExceptionHandling();

        app.MapGet("/regra-de-negocio", () => { throw new RegraDeNegocioException("violação de regra de negócio"); });
        app.MapGet("/nao-encontrado", () => { throw new EntidadeNaoEncontradaException("Pet", 42); });
        app.MapGet("/nao-mapeada", () => { throw new InvalidOperationException("bug genuíno não mapeado"); });
        app.MapGet("/ok", () => Results.Ok("tudo certo"));

        // LU-16 (A4): mesma rota sensível de produção (TutorLunaController — GET
        // /api/v1/tutores/telefone/{numero}), no CAMINHO FELIZ (sem exceção) e sem
        // exceção mapeada — é exatamente o que o achado A4 mediu 0→4 gravações do
        // telefone. Os 3 números escolhem o desfecho (200/404/500) sem que a mensagem
        // da exceção em si carregue o telefone — só o PATH carrega, de propósito, pra
        // isolar o vazamento que o teste precisa provar.
        app.MapGet("/api/v1/tutores/telefone/{numero}", (string numero) => numero switch
        {
            "11999990000" => Results.Ok(new { idTutor = 1 }),
            "22999990000" => throw new EntidadeNaoEncontradaException("Tutor", 1),
            _ => throw new InvalidOperationException("erro não mapeado, sem telefone na mensagem"),
        });

        // S3D-01: rota que emite uma linha de log DE NEGÓCIO no meio do processamento
        // (distinta da linha de conclusão que o Serilog.AspNetCore emite sozinho), para
        // provar correlação real entre linhas — não só a linha final. Palavra
        // deliberadamente diferente de "responded" (ver EncontrarLinhaDeRequestLogging).
        app.MapGet("/com-log-de-negocio", () =>
        {
            app.Logger.LogInformation("Processando etapa intermediária de negócio");
            return Results.Ok("tudo certo, com log no meio do caminho");
        });

        await app.StartAsync();

        _host = app;
        _client = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>Acha a linha de log emitida pelo Serilog.AspNetCore RequestLoggingMiddleware
    /// (mensagem "HTTP {Method} {Path} responded {StatusCode}..."), distinta da linha
    /// separada que o próprio ExceptionHandlerMiddleware escreve ("Exception caught by
    /// middleware...").</summary>
    private LogEvent EncontrarLinhaDeRequestLogging()
    {
        var candidatas = _sink.Events
            .Where(e => e.MessageTemplate.Text.Contains("responded", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidatas.Count != 1)
        {
            Console.WriteLine($"DEBUG total events={_sink.Events.Count}");
            foreach (var e in _sink.Events)
                Console.WriteLine($"DEBUG event: level={e.Level} template=\"{e.MessageTemplate.Text}\"");
        }

        candidatas.Should().ContainSingle(
            "cada requisição deve produzir exatamente uma linha de request-logging do Serilog");
        return candidatas[0];
    }

    [Fact]
    public async Task RegraDeNegocio_Devolve422_LogNaoMenteEDizNivelWarning()
    {
        var response = await _client.GetAsync("/regra-de-negocio");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 422",
            "o texto do log precisa refletir o status real devolvido ao cliente");
        mensagem.Should().NotContain("responded 500",
            "TASK-84: esta é a armadilha de diagnóstico original — 422 real logado como 500");
        linha.Level.Should().Be(LogEventLevel.Warning,
            "4xx é aviso, não erro de servidor — nível Error aqui é falso positivo (quase reprovou o G4 do FIX_6)");
    }

    [Fact]
    public async Task EntidadeNaoEncontrada_Devolve404_LogNaoMenteEDizNivelWarning()
    {
        var response = await _client.GetAsync("/nao-encontrado");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 404");
        mensagem.Should().NotContain("responded 500");
        linha.Level.Should().Be(LogEventLevel.Warning);
    }

    [Fact]
    public async Task ExcecaoNaoMapeada_Devolve500_LogContinuaDizendo500ENivelError()
    {
        // Prova a metade que NÃO pode regredir: um 500 genuíno (bug real, exceção não
        // mapeada por ExceptionHandlerMiddleware) precisa continuar soando como erro de
        // verdade. Trocar o falso positivo (4xx como 500/Error) por um falso negativo
        // (5xx real escondido) seria pior do que o bug original.
        var response = await _client.GetAsync("/nao-mapeada");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.InternalServerError);

        var linha = EncontrarLinhaDeRequestLogging();
        var mensagem = linha.RenderMessage();

        mensagem.Should().Contain("responded 500");
        linha.Level.Should().Be(LogEventLevel.Error,
            "5xx real é exatamente o que o G4 precisa continuar pegando como [ERR]");
    }

    [Fact]
    public async Task RequisicaoComSucesso_Devolve200_LogNivelInformation()
    {
        var response = await _client.GetAsync("/ok");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var linha = EncontrarLinhaDeRequestLogging();
        linha.RenderMessage().Should().Contain("responded 200");
        linha.Level.Should().Be(LogEventLevel.Information);
    }

    /// <summary>
    /// S3D-01: prova de correlação de requisição. Duas linhas de log DISTINTAS emitidas
    /// dentro da MESMA requisição HTTP — a linha de conclusão do
    /// Serilog.AspNetCore (emitida por dentro de <c>UseSerilogRequestLogging</c>) e a
    /// linha de negócio emitida pelo endpoint no meio do processamento — precisam
    /// carregar o MESMO valor de <c>TraceId</c> como propriedade estruturada. Isso só é
    /// possível porque o middleware registrado por
    /// <see cref="RequestPipelineExtensions.UseRequestLoggingAndExceptionHandling"/>
    /// (o primeiro <c>app.Use(...)</c>, ver comentário da classe) empurra
    /// <c>HttpContext.TraceIdentifier</c> para o <c>LogContext</c> ANTES de
    /// <c>UseSerilogRequestLogging</c> e do endpoint rodarem, envolvendo o pipeline
    /// inteiro. Sem esse middleware, nenhuma das duas linhas carregaria a propriedade
    /// (prova de mordida: ver relatório da task com o teste falhando de propósito
    /// contra o código sem o middleware).
    /// </summary>
    [Fact]
    public async Task DuasLinhasDeLogDaMesmaRequisicao_ComMiddlewareDeCorrelacao_CompartilhamMesmoTraceId()
    {
        var response = await _client.GetAsync("/com-log-de-negocio");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var linhaDeConclusao = EncontrarLinhaDeRequestLogging();
        var linhaDeNegocio = _sink.Events.SingleOrDefault(e =>
            e.MessageTemplate.Text.Contains("etapa intermediária de negócio", StringComparison.OrdinalIgnoreCase));

        linhaDeNegocio.Should().NotBeNull(
            "o endpoint de teste precisa ter emitido uma linha de log de negócio no meio do processamento");

        linhaDeConclusao.Properties.Should().ContainKey("TraceId",
            "a linha de conclusão do Serilog.AspNetCore precisa carregar TraceId estruturado");
        linhaDeNegocio!.Properties.Should().ContainKey("TraceId",
            "a linha de negócio, emitida dentro do mesmo escopo de LogContext, também precisa carregar TraceId");

        var traceIdDaConclusao = ((ScalarValue)linhaDeConclusao.Properties["TraceId"]).Value as string;
        var traceIdDoNegocio = ((ScalarValue)linhaDeNegocio.Properties["TraceId"]).Value as string;

        traceIdDaConclusao.Should().NotBeNullOrWhiteSpace();
        traceIdDoNegocio.Should().Be(traceIdDaConclusao,
            "S3D-01: correlação de requisição — duas linhas distintas da mesma requisição HTTP " +
            "devem compartilhar o mesmo TraceId, não só a linha de conclusão");
    }

    /// <summary>
    /// LU-16 G4 (achado A4, BLOQUEANTE): antes deste fix, uma chamada a GET
    /// /api/v1/tutores/telefone/{numero} — o CAMINHO FELIZ, sem exceção — fazia o
    /// telefone do tutor aparecer CRU em toda linha de log da requisição (medido pelo
    /// revisor: 0→4 gravações com uma única chamada 404 real). Este teste procura o
    /// telefone em QUALQUER LogEvent capturado — mensagem renderizada E valores de
    /// propriedade — não só na linha "responded", porque o vazamento original vinha de
    /// TRÊS fontes (a linha de conclusão do Serilog.AspNetCore, "Request starting" e
    /// "Request finished" do Microsoft.AspNetCore.Hosting.Diagnostics).
    /// </summary>
    [Theory]
    [InlineData("11999990000", System.Net.HttpStatusCode.OK)]
    [InlineData("22999990000", System.Net.HttpStatusCode.NotFound)]
    [InlineData("33999990000", System.Net.HttpStatusCode.InternalServerError)]
    public async Task GetTelefone_QualquerStatus_TelefoneNaoApareceEmNenhumLogEvent(
        string numero, System.Net.HttpStatusCode statusEsperado)
    {
        var response = await _client.GetAsync($"/api/v1/tutores/telefone/{numero}");
        response.StatusCode.Should().Be(statusEsperado);

        _sink.Events.Should().NotBeEmpty();

        foreach (var evento in _sink.Events)
        {
            evento.RenderMessage().Should().NotContain(numero,
                $"telefone não pode aparecer na mensagem renderizada (nível {evento.Level}, " +
                $"template \"{evento.MessageTemplate.Text}\")");

            foreach (var (nome, valor) in evento.Properties)
            {
                valor.ToString().Should().NotContain(numero,
                    $"telefone não pode aparecer na propriedade estruturada '{nome}' " +
                    $"(nível {evento.Level}, template \"{evento.MessageTemplate.Text}\")");
            }
        }

        // Regressão: a linha de conclusão continua existindo e com o marcador de
        // redação — não é "logar nada", é "logar sem o número".
        var linha = EncontrarLinhaDeRequestLogging();
        linha.RenderMessage().Should().Contain("/api/v1/tutores/telefone/{redacted}");
    }

    /// <summary>
    /// Regressão da S3D-01: a mutação de <c>context.Request.Path</c> feita pelo fix da
    /// A4 não pode quebrar o TraceId (LU-16) nem apagar path útil de rota SEM PII.
    /// </summary>
    [Fact]
    public async Task OutraRota_SemPii_ContinuaCompletaMesmoComOFixDoA4Ativo()
    {
        var response = await _client.GetAsync("/ok");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var linha = EncontrarLinhaDeRequestLogging();
        linha.RenderMessage().Should().Contain("/ok");
    }
}
