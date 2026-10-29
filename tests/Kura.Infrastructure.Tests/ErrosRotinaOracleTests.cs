namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Exceptions;
using Kura.Infrastructure.RotinasOracle;

/// <summary>
/// S4-DB-06 — o mapeador puro <c>OracleException.Number</c> para exceção de domínio. Função de
/// <c>int</c> porque o driver não deixa construir <c>OracleException</c> em teste.
///
/// <para>🔴 Cada título/mensagem é conferido contra <c>ORA-</c> e <c>PKG_</c>/<c>KURA.</c>: o
/// middleware põe <c>Message</c> no <c>title</c> da resposta, então a mensagem do Oracle
/// (<c>ORA-06512: at "KURA.PKG_COBRANCA"</c>) não pode passar nem como <c>Message</c> nem pela
/// exceção devolvida.</para>
/// </summary>
public class ErrosRotinaOracleTests
{
    // O que o Oracle de verdade devolve, e que NÃO pode vazar.
    private static readonly Exception Origem =
        new InvalidOperationException("ORA-20001: p_id_clinica nulo\nORA-06512: at \"KURA.PKG_COBRANCA\", line 12");

    [Theory]
    [InlineData(4063)]
    [InlineData(4067)]
    [InlineData(4068)]
    [InlineData(6508)]
    [InlineData(6550)]
    public void Package_ausente_ou_invalido_vira_RotinaOracleIndisponivel(int number)
    {
        var ex = ErrosRotinaOracle.Traduzir(number, 7, Origem);

        ex.Should().BeOfType<RotinaOracleIndisponivelException>();
        ex.Message.Should().Be("Rotina Oracle indisponível.");
        SemVazamento(ex);
    }

    [Fact]
    public void Clinica_inexistente_20002_vira_EntidadeNaoEncontrada_404()
    {
        var ex = ErrosRotinaOracle.Traduzir(20002, 7, Origem);

        ex.Should().BeOfType<EntidadeNaoEncontradaException>();
        SemVazamento(ex);
    }

    [Fact]
    public void P_id_clinica_nulo_20001_e_bug_do_servidor_e_vira_500_de_mensagem_fixa()
    {
        var ex = ErrosRotinaOracle.Traduzir(20001, 7, Origem);

        ex.Should().BeOfType<RotinaOracleFalhouException>();
        ex.Message.Should().Be(RotinaOracleFalhouException.MensagemFixa);
        SemVazamento(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(942)]
    [InlineData(12541)]
    [InlineData(4098)]   // trigger inválida: só tem sentido no INSERT, não numa chamada de package
    public void Qualquer_outro_codigo_vira_500_de_mensagem_fixa(int number)
    {
        var ex = ErrosRotinaOracle.Traduzir(number, 7, Origem);

        ex.Should().BeOfType<RotinaOracleFalhouException>();
        SemVazamento(ex);
    }

    [Theory]
    [InlineData(4063, true)]
    [InlineData(4098, true)]
    [InlineData(4067, true)]
    [InlineData(4068, true)]
    [InlineData(6508, true)]
    [InlineData(6550, true)]
    [InlineData(1, false)]      // ORA-00001: violação de unicidade segue como estava
    [InlineData(2291, false)]   // FK
    [InlineData(20001, false)]
    public void No_INSERT_de_cobranca_so_os_codigos_de_indisponibilidade_viram_503(int number, bool esperado) =>
        ErrosRotinaOracle.IndisponivelNoInsert(number).Should().Be(esperado);

    private static void SemVazamento(Exception ex)
    {
        ex.Message.Should().NotContain("ORA-");
        ex.Message.Should().NotContain("PKG_");
        ex.Message.Should().NotContain("KURA.");
    }
}
