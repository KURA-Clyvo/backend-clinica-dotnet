namespace Kura.Domain.Tests;

using FluentAssertions;
using Kura.Domain.Tutores;

/// <summary>
/// Cobertura direta de <see cref="NormalizadorTelefone"/> — REC-01/backlog
/// <c>KURA_BACKLOG_RECEPCAO.md</c>, G0 item 4 (a tabela dos 6 casos vem literalmente de lá).
/// Mordida (b) do aceite da REC-01: devolver a entrada crua (sem normalizar) faz estes testes
/// falharem.
/// </summary>
public class NormalizadorTelefoneTests
{
    // ── Os 6 casos da tabela do G0 item 4 ───────────────────────────────────

    [Fact]
    public void Caso1_CelularComMascara_NormalizaParaDdiBrasil()
    {
        NormalizadorTelefone.TentarNormalizar("(11) 98765-4321", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("5511987654321");
    }

    [Fact]
    public void Caso2_CelularSemDdiSemMascara_NormalizaParaDdiBrasil()
    {
        NormalizadorTelefone.TentarNormalizar("11987654321", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("5511987654321");
    }

    [Fact]
    public void Caso3_ComDdiExplicitoMaisSinalDeMais_ArmazenaSoOsDigitos()
    {
        NormalizadorTelefone.TentarNormalizar("+55 11 98765-4321", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("5511987654321");
    }

    [Fact]
    public void Caso4_FixoComMascara_NormalizaParaDdiBrasilSemNonoDigito()
    {
        NormalizadorTelefone.TentarNormalizar("(11) 3456-7890", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("551134567890");
    }

    [Fact]
    public void Caso5_CelularSemO9_NormalizaParaDdiBrasil()
    {
        // G0 item 4: "só se a Twilio entregar 12 dígitos — não medido", mas a REGRA aceita
        // 10 dígitos nacionais igual (DDD + 8 dígitos) — comportamento declarado, não
        // confirmado contra tráfego real desse formato específico.
        NormalizadorTelefone.TentarNormalizar("(11) 3456-7891", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("551134567891");
    }

    [Fact]
    public void Caso6_EstrangeiroComDdiExplicito_ArmazenaSoOsDigitosSemPlus()
    {
        NormalizadorTelefone.TentarNormalizar("+1 415 555 0100", out var armazenado).Should().BeTrue();
        armazenado.Should().Be("14155550100");
    }

    // ── Formatos inválidos: "nunca grava lixo" ──────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")] // menos de 10 dígitos, sem "+"
    [InlineData("+")] // "+" sem nenhum dígito
    [InlineData("12345678901234")] // 14 dígitos, sem "+", não é o padrão BR conhecido
    public void FormatoNaoReconhecido_DevolveFalseENaoArmazenaNada(string entrada)
    {
        var resultado = NormalizadorTelefone.TentarNormalizar(entrada, out var armazenado);

        resultado.Should().BeFalse();
        armazenado.Should().BeEmpty();
    }

    [Fact]
    public void EntradaNula_DevolveFalse()
    {
        NormalizadorTelefone.TentarNormalizar(null, out _).Should().BeFalse();
    }

    // ── Idempotência — obrigatória para o caso doméstico (ver XML doc da classe) ───────────

    [Theory]
    [InlineData("(11) 98765-4321")] // caso 1
    [InlineData("11987654321")] // caso 2
    [InlineData("+55 11 98765-4321")] // caso 3
    [InlineData("(11) 3456-7890")] // caso 4
    public void Idempotente_NormalizarOValorJaArmazenado_DevolveOMesmoValor(string entradaOriginal)
    {
        NormalizadorTelefone.TentarNormalizar(entradaOriginal, out var primeiraPassada).Should().BeTrue();

        // Realimenta o valor JÁ ARMAZENADO (o que o seed-demo-luna.sh faz ao reenviar o
        // telefone pelo PUT, e o que aconteceria se a recepção reenviasse o mesmo tutor sem
        // mudar o telefone) — precisa devolver o MESMO valor, nunca prefixar "55" de novo.
        NormalizadorTelefone.TentarNormalizar(primeiraPassada, out var segundaPassada).Should().BeTrue();

        segundaPassada.Should().Be(primeiraPassada);
    }

    [Fact]
    public void ParaE164_PrefixaComSinalDeMais()
    {
        NormalizadorTelefone.ParaE164("5511987654321").Should().Be("+5511987654321");
    }
}
