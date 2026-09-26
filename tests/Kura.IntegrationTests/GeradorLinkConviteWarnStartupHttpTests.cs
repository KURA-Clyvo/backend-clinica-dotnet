namespace Kura.IntegrationTests;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// N4 (G2b fix wave 2, achado Minor — REC-01): prova, contra o STARTUP REAL (via
/// <see cref="KuraApiFactory"/>, o mesmo <c>Program.cs</c> de produção), que o WARN de
/// <c>Convite:UrlBaseAppTutor</c> ausente sai NA PARTIDA — sem NENHUMA requisição HTTP —
/// porque <c>Program.cs</c> resolve <see cref="Kura.Domain.Interfaces.IGeradorLinkConvite"/>
/// explicitamente logo depois do <c>Build()</c>. A re-G2 mutou essa linha (mutação N4 do
/// <c>g2-rec01.md</c>) e a suíte sobreviveu (nenhum teste exercitava o comportamento de
/// verdade) — este teste fecha esse buraco.
///
/// <para><b>Por que via <see cref="ILoggerProvider"/> e NÃO via captura de
/// <see cref="Console.Out"/></b> (a sonda do G2b usava um <c>TextWriter</c> trocando
/// <c>Console.Out</c> globalmente): o xUnit paraleliza collections diferentes por padrão
/// (ver <c>ColecaoDeIntegracao.cs</c>) — capturar <c>Console.Out</c> globalmente correria o
/// risco de contar o WARN de OUTRA classe de teste que também construa uma
/// <see cref="KuraApiFactory"/> sem config (ex.: <c>TutorRecepcaoHttpTests</c>) rodando em
/// PARALELO numa collection diferente, dando um número inflado e um teste instável. Um
/// <see cref="ILoggerProvider"/> registrado só nesta instância de fábrica via
/// <c>ConfigureLogging</c> é privado a ela — imune a ruído de outro processo/teste, mesmo em
/// paralelo. Confirmado empiricamente: o Serilog (<c>UseSerilog</c> em <c>Program.cs</c>) NÃO
/// substitui providers adicionados aqui — os dois convivem.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class GeradorLinkConviteWarnStartupHttpTests
{
    private sealed class ProviderCapturador : ILoggerProvider
    {
        public List<string> MensagensFormatadas { get; } = [];

        public ILogger CreateLogger(string categoryName) => new LoggerCapturador(MensagensFormatadas);

        public void Dispose() { }

        private sealed class LoggerCapturador(List<string> destino) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EscopoNulo.Instancia;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (destino) destino.Add(formatter(state, exception));
            }

            private sealed class EscopoNulo : IDisposable
            {
                public static readonly EscopoNulo Instancia = new();
                public void Dispose() { }
            }
        }
    }

    private static int Ocorrencias(IEnumerable<string> mensagens, string trecho) =>
        mensagens.Count(m => m.Contains(trecho, StringComparison.Ordinal));

    private sealed class FabricaSemConviteConfigurado : KuraApiFactory
    {
        public ProviderCapturador Provider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Não sobrescreve Convite:UrlBaseAppTutor — herda o "" do appsettings.json, o
            // mesmo estado "ausente" que produz o WARN.
            builder.ConfigureLogging(lb => lb.AddProvider(Provider));
        }
    }

    [Fact]
    public void HostSobeSemNenhumaRequisicao_WarnDeConviteAusenteJaApareceUmaVez()
    {
        using var factory = new FabricaSemConviteConfigurado();

        // Acessar `.Services` força o WebApplicationFactory a construir o IHost (que roda
        // Program.cs até `app.Run()`, sem chamar `Run()` de verdade) — NENHUMA requisição
        // HTTP acontece aqui.
        _ = factory.Services;

        var warnsSemRequisicao = Ocorrencias(factory.Provider.MensagensFormatadas, "Convite:UrlBaseAppTutor ausente");

        warnsSemRequisicao.Should().Be(
            1,
            "Program.cs resolve IGeradorLinkConvite logo após Build() — o WARN tem que sair " +
            "na partida, antes de qualquer requisição, não só no primeiro POST /tutores");
    }

    private sealed class FabricaComConviteConfigurado : KuraApiFactory
    {
        public ProviderCapturador Provider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Convite:UrlBaseAppTutor", "https://tutor.exemplo.com/");
            builder.ConfigureLogging(lb => lb.AddProvider(Provider));
        }
    }

    [Fact]
    public async Task ComConviteConfigurado_NenhumWarnApareceNaPartida()
    {
        // Controle negativo: com a config presente, o WARN não deveria aparecer nunca —
        // prova que o teste acima não está capturando ruído genérico de startup.
        using var factory = new FabricaComConviteConfigurado();
        var health = await factory.CreateClient().GetAsync("/health");

        health.IsSuccessStatusCode.Should().BeTrue("controle: o host tem que ter subido de verdade");
        Ocorrencias(factory.Provider.MensagensFormatadas, "Convite:UrlBaseAppTutor ausente").Should().Be(0);
    }

    [Fact]
    public void ControlePositivo_OProviderEnxergaOutroLoggerQualquer()
    {
        // Prova que o instrumento (ProviderCapturador) não está cego: um logger comum,
        // resolvido do MESMO container, tem sua mensagem capturada.
        using var factory = new FabricaSemConviteConfigurado();
        using var escopo = factory.Services.CreateScope();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ControlePositivo");

        logger.LogWarning("valor de controle N4");

        Ocorrencias(factory.Provider.MensagensFormatadas, "valor de controle N4").Should().Be(1);
    }
}
