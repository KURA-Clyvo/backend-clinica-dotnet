namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Infrastructure.Storage;

/// <summary>
/// FT-02 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>): prova de <see cref="AssinadorUrlFotoHmac"/>
/// contra um VETOR FIXO — o mesmo segredo/chave/exp/assinatura que a FT-05 (Java) vai
/// replicar byte a byte, para provar que os dois lados calculam a MESMA string.
///
/// <para>Vetor: segredo <c>"kura-teste-segredo-com-32-bytes-ok!!"</c> (36 bytes UTF-8), chave
/// <c>"clinica/1/pet/2/abc_256.webp"</c>, exp <c>1893456000</c> (unix seconds). Mensagem
/// assinada: <c>chave + "\n" + exp</c>. Assinatura esperada calculada de forma independente
/// com Python (<c>hmac</c>/<c>hashlib</c>/<c>base64</c>) e reconferida com Node.js
/// (<c>crypto.createHmac</c>) — os dois deram o mesmo literal, registrado no artefato
/// <c>.superpowers/sdd/NEXT_EVENTO/ft-02-report.md</c> junto do comando exato.</para>
///
/// <para>Mordida real (comparação trocada por <c>return true</c>, suíte rodada, falha
/// registrada, restaurada) está documentada no artefato — este arquivo prova o
/// COMPORTAMENTO correto, não a mordida em si.</para>
/// </summary>
public sealed class AssinadorUrlFotoHmacTests
{
    private const string SegredoVetorFixo = "kura-teste-segredo-com-32-bytes-ok!!";
    private const string ChaveVetorFixo = "clinica/1/pet/2/abc_256.webp";
    private const long ExpVetorFixo = 1893456000L;

    // Calculada de forma independente (Python hmac/hashlib/base64 e Node.js crypto,
    // resultado idêntico nos dois) — ver artefato ft-02-report.md para o comando exato.
    private const string SigEsperadaVetorFixo = "frkL_Z__8wHPKtfmeVu8A4L59bcuZNzdSx31BgHBjL0";

    private static readonly DateTimeOffset AgoraAntesDoVencimento = DateTimeOffset.FromUnixTimeSeconds(1758000000L);

    private readonly AssinadorUrlFotoHmac _sut = new(SegredoVetorFixo);

    [Fact]
    public void Assinar_VetorFixo_DevolveAssinaturaEsperada()
    {
        var sig = _sut.Assinar(ChaveVetorFixo, DateTimeOffset.FromUnixTimeSeconds(ExpVetorFixo));

        sig.Should().Be(SigEsperadaVetorFixo);
    }

    [Fact]
    public void Validar_VetorFixo_AssinaturaCorreta_DevolveTrue()
    {
        var valido = _sut.Validar(ChaveVetorFixo, ExpVetorFixo, SigEsperadaVetorFixo, AgoraAntesDoVencimento);

        valido.Should().BeTrue();
    }

    [Fact]
    public void Validar_AssinaturaAdulterada_DevolveFalse()
    {
        // Um caractere trocado no fim da assinatura válida.
        var sigAdulterada = SigEsperadaVetorFixo[..^1] + (SigEsperadaVetorFixo[^1] == 'A' ? 'B' : 'A');

        var valido = _sut.Validar(ChaveVetorFixo, ExpVetorFixo, sigAdulterada, AgoraAntesDoVencimento);

        valido.Should().BeFalse();
    }

    [Fact]
    public void Validar_ChaveDiferente_DevolveFalse()
    {
        var valido = _sut.Validar("clinica/1/pet/2/outra-chave.webp", ExpVetorFixo, SigEsperadaVetorFixo, AgoraAntesDoVencimento);

        valido.Should().BeFalse();
    }

    [Fact]
    public void Validar_ExpAlterado_DevolveFalse()
    {
        var valido = _sut.Validar(ChaveVetorFixo, ExpVetorFixo + 1, SigEsperadaVetorFixo, AgoraAntesDoVencimento);

        valido.Should().BeFalse();
    }

    [Fact]
    public void Validar_ExpVencido_DevolveFalse()
    {
        // Assina com validade num instante passado (relativo a "agora") e confirma que a
        // própria assinatura, correta, é rejeitada por estar vencida.
        var expPassado = AgoraAntesDoVencimento.AddMinutes(-10).ToUnixTimeSeconds();
        var sigParaExpPassado = _sut.Assinar(ChaveVetorFixo, DateTimeOffset.FromUnixTimeSeconds(expPassado));

        var valido = _sut.Validar(ChaveVetorFixo, expPassado, sigParaExpPassado, AgoraAntesDoVencimento);

        valido.Should().BeFalse();
    }

    [Fact]
    public void Validar_ExpIgualAgora_DevolveTrue()
    {
        // Borda: exp == agora não é "vencido" (só exp < agora é).
        var sig = _sut.Assinar(ChaveVetorFixo, AgoraAntesDoVencimento);

        var valido = _sut.Validar(ChaveVetorFixo, AgoraAntesDoVencimento.ToUnixTimeSeconds(), sig, AgoraAntesDoVencimento);

        valido.Should().BeTrue();
    }

    [Fact]
    public void Validar_SigVazia_DevolveFalse()
    {
        var valido = _sut.Validar(ChaveVetorFixo, ExpVetorFixo, "", AgoraAntesDoVencimento);

        valido.Should().BeFalse();
    }

    [Fact]
    public void Validar_SigMalformadaNaoBase64Url_DevolveFalseSemLancar()
    {
        var act = () => _sut.Validar(ChaveVetorFixo, ExpVetorFixo, "!!!nao-e-base64url!!!", AgoraAntesDoVencimento);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Theory]
    [InlineData("segredo-com-menos-de-32-bytes")]
    [InlineData("")]
    public void Construtor_SegredoCurtoOuVazio_Lanca(string segredoInvalido)
    {
        var act = () => new AssinadorUrlFotoHmac(segredoInvalido);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Construtor_SegredoComExatamente32Bytes_NaoLanca()
    {
        var segredo32Bytes = new string('a', 32);

        var act = () => new AssinadorUrlFotoHmac(segredo32Bytes);

        act.Should().NotThrow();
    }
}
