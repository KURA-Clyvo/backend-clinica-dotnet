namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Interfaces;
using Moq;

/// <summary>
/// S4-DB-06 — o service fino de exportação/relatório por package Oracle. A única decisão dele é
/// de quem é a clínica: tem de sair do <see cref="IClinicaContext"/> e de lugar nenhum mais.
///
/// <para>🔴 O gateway é um mock estrito com <b>duas clínicas</b> no setup: cada clínica tem o
/// seu JSON, e uma chamada com qualquer outro id falha. Com uma clínica só, "passou o id certo"
/// seria indistinguível de "passou um id fixo". ⚠️ Isto prova a fiação, não o Oracle: a chamada
/// ao banco real é NÃO VERIFICADA nesta task (G4 / S4-DB-08).</para>
/// </summary>
public class CobrancaOracleServiceTests
{
    private const long ClinicaA = 11L;
    private const long ClinicaB = 22L;

    private static Mock<IRotinasCobrancaOracle> GatewayDeDuasClinicas()
    {
        var gw = new Mock<IRotinasCobrancaOracle>(MockBehavior.Strict);
        gw.Setup(g => g.ListarJsonAsync(ClinicaA, It.IsAny<CancellationToken>())).ReturnsAsync("[\"lista-A\"]");
        gw.Setup(g => g.ListarJsonAsync(ClinicaB, It.IsAny<CancellationToken>())).ReturnsAsync("[\"lista-B\"]");
        gw.Setup(g => g.RelatorioJsonAsync(ClinicaA, It.IsAny<CancellationToken>())).ReturnsAsync("{\"rel\":\"A\"}");
        gw.Setup(g => g.RelatorioJsonAsync(ClinicaB, It.IsAny<CancellationToken>())).ReturnsAsync("{\"rel\":\"B\"}");
        return gw;
    }

    private static CobrancaOracleService Criar(Mock<IRotinasCobrancaOracle> gw, long idClinicaDoJwt)
    {
        var ctx = new Mock<IClinicaContext>();
        ctx.Setup(c => c.IdClinica).Returns(idClinicaDoJwt);
        return new CobrancaOracleService(gw.Object, ctx.Object);
    }

    [Theory]
    [InlineData(ClinicaA, "[\"lista-A\"]")]
    [InlineData(ClinicaB, "[\"lista-B\"]")]
    public async Task Exportar_usa_a_clinica_do_contexto_e_devolve_o_json_do_package(long clinica, string esperado)
    {
        var gw = GatewayDeDuasClinicas();

        var json = await Criar(gw, clinica).ExportarJsonAsync();

        json.Should().Be(esperado);
        gw.Verify(g => g.ListarJsonAsync(clinica, It.IsAny<CancellationToken>()), Times.Once);
        gw.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ClinicaA, "{\"rel\":\"A\"}")]
    [InlineData(ClinicaB, "{\"rel\":\"B\"}")]
    public async Task Relatorio_usa_a_clinica_do_contexto_e_devolve_o_json_do_package(long clinica, string esperado)
    {
        var gw = GatewayDeDuasClinicas();

        var json = await Criar(gw, clinica).RelatorioJsonAsync();

        json.Should().Be(esperado);
        gw.Verify(g => g.RelatorioJsonAsync(clinica, It.IsAny<CancellationToken>()), Times.Once);
        gw.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task O_cancellation_token_chega_ao_gateway()
    {
        var gw = new Mock<IRotinasCobrancaOracle>();
        gw.Setup(g => g.ListarJsonAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync("[]");
        using var cts = new CancellationTokenSource();

        await Criar(gw, ClinicaA).ExportarJsonAsync(cts.Token);

        gw.Verify(g => g.ListarJsonAsync(ClinicaA, cts.Token), Times.Once);
    }
}
