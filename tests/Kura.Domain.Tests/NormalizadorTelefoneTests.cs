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

    // ── G2 fix wave (achados Important #2 / Minor #6): piso/teto de dígitos ─

    [Theory]
    [InlineData("+1")] // 1 dígito — achado Minor #6, "nunca grava lixo" não cumpria
    [InlineData("+55 11")] // 4 dígitos
    [InlineData("+123456789")] // 9 dígitos — abaixo do piso (10)
    public void ComDdiExplicito_MenosQueOPiso_DevolveFalse(string entrada)
    {
        var resultado = NormalizadorTelefone.TentarNormalizar(entrada, out var armazenado);

        resultado.Should().BeFalse();
        armazenado.Should().BeEmpty();
    }

    [Fact]
    public void ComDdiExplicito_MaisQueOTeto_DevolveFalse()
    {
        // Achado Important #2 (G2): sonda mediu "+" + 30 dígitos aceito, E.164 de 31 chars —
        // NÃO cabe em DS_WHATSAPP VARCHAR2(20) (backend-tutor-java V1__initial_schema.sql:92).
        var entrada = "+" + new string('9', 30);

        var resultado = NormalizadorTelefone.TentarNormalizar(entrada, out var armazenado);

        resultado.Should().BeFalse();
        armazenado.Should().BeEmpty();
    }

    [Fact]
    public void ComDdiExplicito_ExatamenteNoTeto_NormalizaEExatosDezesseisCharsEmE164()
    {
        // Teto = 15 dígitos (limite do próprio E.164) ⇒ ParaE164 dá 16 chars, cabe em
        // VARCHAR2(20) com folga.
        var entrada = "+" + new string('9', 15);

        NormalizadorTelefone.TentarNormalizar(entrada, out var armazenado).Should().BeTrue();
        armazenado.Should().HaveLength(15);
        NormalizadorTelefone.ParaE164(armazenado).Should().HaveLength(16);
    }

    [Fact]
    public void ComDdiExplicito_ExatamenteNoPiso_Normaliza()
    {
        var entrada = "+" + new string('9', 10);

        NormalizadorTelefone.TentarNormalizar(entrada, out var armazenado).Should().BeTrue();
        armazenado.Should().HaveLength(10);
    }

    // ── ExtrairApenasDigitos (usado pela busca da Luna, I3) ──────────────────

    [Theory]
    [InlineData("14155550100", "14155550100")]
    [InlineData("(11) 91234-5678", "11912345678")]
    [InlineData("+55 11 91234-5678", "5511912345678")]
    [InlineData("", "")]
    public void ExtrairApenasDigitos_NaoInterpretaNadaSoExtrai(string entrada, string esperado)
    {
        NormalizadorTelefone.ExtrairApenasDigitos(entrada).Should().Be(esperado);
    }

    [Fact]
    public void ExtrairApenasDigitos_EntradaNula_DevolveVazio()
    {
        NormalizadorTelefone.ExtrairApenasDigitos(null).Should().BeEmpty();
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
