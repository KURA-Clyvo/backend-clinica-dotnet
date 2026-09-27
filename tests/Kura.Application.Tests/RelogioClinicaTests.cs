namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// REC-08 (backlog <c>KURA_BACKLOG_RECEPCAO.md</c>, A-5/F-4) — <see cref="RelogioClinica"/> em
/// isolamento: conversão de fuso correta (default e configurado), fallback seguro para fuso
/// inválido, e a virada de dia às 23:59/00:00 LOCAIS (mordida "c" do aceite da REC-08) — que é
/// exatamente o ponto em que UTC e hora de São Paulo discordam sobre qual é "hoje".
/// </summary>
public sealed class RelogioClinicaTests
{
    private sealed class TimeProviderFixo(DateTimeOffset utcAgora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcAgora;
    }

    private static IConfiguration ConfigSemFuso()
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);
        return mock.Object;
    }

    private static IConfiguration ConfigComFuso(string fuso)
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["RelogioClinica:FusoHorario"]).Returns(fuso);
        return mock.Object;
    }

    private static ILogger<RelogioClinica> LoggerMudo() => new Mock<ILogger<RelogioClinica>>().Object;

    [Fact]
    public void Agora_SemConfig_UsaSaoPauloPorDefault()
    {
        // 12:37 UTC == 09:37 em São Paulo (UTC-3, sem horário de verão desde 2019) -- o
        // exemplo medido ao vivo no G0 item 3.
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigSemFuso(), LoggerMudo());

        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 9, 37, 0));
    }

    [Fact]
    public void Hoje_EDataDeAgoraLocal()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigSemFuso(), LoggerMudo());

        relogio.Hoje().Should().Be(new DateTime(2026, 9, 26));
    }

    [Fact]
    public void Agora_ComFusoConfigurado_UsaOFusoDaConfig()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigComFuso("UTC"), LoggerMudo());

        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 12, 0, 0));
    }

    [Fact]
    public void Agora_ComFusoInvalidoNaConfig_CaiParaOFusoDefaultSemDerrubarOProcesso()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(
            new TimeProviderFixo(utcAgora), ConfigComFuso("XPTO/Nao_Existe"), LoggerMudo());

        // Não lança -- cai no default (São Paulo), mesmo resultado do teste acima.
        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 9, 37, 0));
    }

    /// <summary>
    /// Mordida "c" do aceite da REC-08: virada de dia às 23:59/00:00 LOCAIS. 02:59:59 UTC de
    /// 27/09 é 23:59:59 de 26/09 em SP -- ainda "ontem" local, mesmo já sendo "hoje" em UTC.
    /// </summary>
    [Fact]
    public void Hoje_NaViradaDeDia_UmMinutoAntesDaMeiaNoiteLocal_AindaEDiaAnteriorEmUtcJaEDiaSeguinte()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 27, 2, 59, 59, TimeSpan.Zero);
        var relogio = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigSemFuso(), LoggerMudo());

        relogio.Hoje().Should().Be(new DateTime(2026, 9, 26)); // local ainda é 26, UTC já é 27
        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 23, 59, 59));
    }

    /// <summary>Mesma virada, 1 segundo depois: agora É meia-noite local -- "hoje" avança.</summary>
    [Fact]
    public void Hoje_NaViradaDeDia_NoInstanteDaMeiaNoiteLocal_AvancaParaODiaSeguinte()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigSemFuso(), LoggerMudo());

        relogio.Hoje().Should().Be(new DateTime(2026, 9, 27));
        relogio.Agora().Should().Be(new DateTime(2026, 9, 27, 0, 0, 0));
    }

    // ---------- G2 fix wave (m-4): o WARN de fuso inválido tem que ser PROVADO, não só o valor ----------

    /// <summary>
    /// Padrão Moq para verificar chamada aos métodos de extensão <c>ILogger.LogX</c> (que por
    /// baixo chamam <c>ILogger.Log(LogLevel, EventId, TState, Exception?, Func...)</c>) --
    /// <c>It.IsAnyType</c> casa o <c>TState</c> genérico que o compilador gera para a mensagem
    /// interpolada.
    /// </summary>
    private static void VerificaWarnLogado(Mock<ILogger<RelogioClinica>> loggerMock, Times vezes) =>
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            vezes);

    /// <summary>
    /// G2 achado m-4 (Minor): o teste antigo (<c>Agora_ComFusoInvalidoNaConfig_...</c>) só
    /// conferia o VALOR devolvido, nunca que o <c>WARN</c> foi de fato logado -- "loga um único
    /// WARN" (comentário da classe) não estava provado. Mordida: remover o
    /// <c>logger.LogWarning</c> do primeiro <c>catch</c> em <c>RelogioClinica.cs</c> -- este
    /// teste tem que ficar vermelho.
    /// </summary>
    [Fact]
    public void Agora_ComFusoInvalidoNaConfig_LogaUmWarnComAExcecaoOriginal()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero);
        var loggerMock = new Mock<ILogger<RelogioClinica>>();

        _ = new RelogioClinica(new TimeProviderFixo(utcAgora), ConfigComFuso("XPTO/Nao_Existe"), loggerMock.Object);

        VerificaWarnLogado(loggerMock, Times.Once());
    }

    // ---------- G2 fix wave (m-5): fallback do fallback -- nem o configurado nem o default existem no host ----------

    /// <summary>
    /// Resolvedor que SEMPRE lança <see cref="TimeZoneNotFoundException"/>, simulando um host
    /// sem <c>tzdata</c> nenhum (nem o fuso configurado, nem <c>America/Sao_Paulo</c>) -- sem
    /// depender de o host que roda a suíte realmente estar nesse estado (o G0/G2 mediram que a
    /// imagem de produção TEM <c>tzdata</c>; este é um teste de estrutura, não de ambiente).
    /// </summary>
    private static TimeZoneInfo ResolvedorSempreFalha(string id) =>
        throw new TimeZoneNotFoundException($"fuso '{id}' não existe neste host (simulado)");

    /// <summary>
    /// G2 achado m-5 (Minor) -- antes desta fix wave, se o SEGUNDO
    /// <c>FindSystemTimeZoneById(FusoDefault)</c> também lançasse, a exceção escapava sem
    /// tratamento do construtor: como a resolução do singleton é preguiçosa, isso viraria
    /// <c>500</c> no primeiro request que tocasse dashboard/teleconsulta/agenda, não uma falha
    /// clara na partida. Mordida: reverter o `catch` interno (m-5) -- este teste tem que ficar
    /// vermelho (a exceção deve voltar a escapar).
    /// </summary>
    [Fact]
    public void Agora_QuandoNemOConfiguradoNemODefaultExistemNoHost_CaiParaOffsetFixoDeMenos3SemLancar()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero); // 09:37 em UTC-3
        var loggerMock = new Mock<ILogger<RelogioClinica>>();

        var relogio = new RelogioClinica(
            new TimeProviderFixo(utcAgora), ConfigSemFuso(), loggerMock.Object, ResolvedorSempreFalha);

        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 9, 37, 0));
        // 2 WARN: um pelo fuso configurado/default que falhou, outro pelo default que também falhou.
        VerificaWarnLogado(loggerMock, Times.Exactly(2));
    }

    /// <summary>Controle: com resolvedor real (BCL), o comportamento é o mesmo dos testes acima.</summary>
    [Fact]
    public void Agora_ComResolvedorRealInjetadoExplicitamente_ComportaIgualAoConstrutorPadrao()
    {
        var utcAgora = new DateTimeOffset(2026, 9, 26, 12, 37, 0, TimeSpan.Zero);
        var relogio = new RelogioClinica(
            new TimeProviderFixo(utcAgora), ConfigSemFuso(), LoggerMudo(), TimeZoneInfo.FindSystemTimeZoneById);

        relogio.Agora().Should().Be(new DateTime(2026, 9, 26, 9, 37, 0));
    }
}
