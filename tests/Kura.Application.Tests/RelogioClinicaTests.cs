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
}
