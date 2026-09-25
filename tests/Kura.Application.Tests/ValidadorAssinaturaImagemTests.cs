namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;

public class ValidadorAssinaturaImagemTests
{
    [Fact]
    public void DetectarAsync_JpegValido_DevolveJpeg()
    {
        var stream = FormFileFixtures.CriarStream(FormFileFixtures.JpegValido);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.Jpeg);
    }

    [Fact]
    public void DetectarAsync_PngValido_DevolvePng()
    {
        var stream = FormFileFixtures.CriarStream(FormFileFixtures.PngValido);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.Png);
    }

    [Fact]
    public void DetectarAsync_WebPValido_DevolveWebP()
    {
        var stream = FormFileFixtures.CriarStream(FormFileFixtures.WebpValido);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.WebP);
    }

    [Fact]
    public void DetectarAsync_BytesInvalidos_ComContentTypeJpegMentiroso_DevolveNull()
    {
        // A prova central da regra "magic bytes, não Content-Type do cliente": o
        // Content-Type diz image/jpeg, os bytes não são JPEG nenhum.
        var stream = FormFileFixtures.CriarStream(FormFileFixtures.BytesInvalidos);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().BeNull();
    }

    [Fact]
    public void DetectarAsync_ArquivoVazio_DevolveNull()
    {
        var stream = FormFileFixtures.CriarStream([]);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().BeNull();
    }

    [Fact]
    public void DetectarAsync_ArquivoNulo_DevolveNull()
    {
        var formato = ValidadorAssinaturaImagem.Detectar(null);
        formato.Should().BeNull();
    }

    [Fact]
    public void DetectarAsync_ArquivoMenorQueCabecalhoCompleto_NaoLanca()
    {
        // Controle de robustez: 2 bytes só (menor que qualquer assinatura) não deve lançar,
        // só devolver null.
        var stream = FormFileFixtures.CriarStream([0xFF, 0xD8]);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().BeNull();
    }

    /// <summary>
    /// 🔴 Fix wave G2 (g2-ft03.md, achado G2-d): buraco de teste — nenhum caso cobria "começa
    /// com RIFF mas NÃO é WEBP nos bytes 8-11" (ex.: um WAV real: "RIFF" + tamanho + "WAVE").
    /// A mutação que remove a checagem de <c>AssinaturaWebp</c> em
    /// <c>DetectarPorCabecalho</c> (bytes 8-11) fazia qualquer container RIFF virar WebP em
    /// silêncio, e a suíte inteira continuava 100% verde — só uma sonda HTTP fora da suíte
    /// (RIFF....WAVE → esperado 400) pegava a mutação. Este teste fecha o buraco: mordida
    /// aplicada e revertida nesta fix wave, ver o relatório da task.
    /// </summary>
    [Fact]
    public void DetectarAsync_RiffQueNaoEhWebp_DevolveNull()
    {
        byte[] wav = [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WAVE"u8.ToArray()];
        var stream = FormFileFixtures.CriarStream(wav);
        var formato = ValidadorAssinaturaImagem.Detectar(stream);
        formato.Should().BeNull(
            "o container é RIFF, mas os bytes 8-11 são 'WAVE', não 'WEBP' — não deve ser " +
            "aceito como imagem WebP só por começar com RIFF");
    }

    [Theory]
    [InlineData(ValidadorAssinaturaImagem.Formato.Jpeg, "jpg", "image/jpeg")]
    [InlineData(ValidadorAssinaturaImagem.Formato.Png, "png", "image/png")]
    [InlineData(ValidadorAssinaturaImagem.Formato.WebP, "webp", "image/webp")]
    public void ExtensaoEContentType_PorFormato_Correspondem(
        ValidadorAssinaturaImagem.Formato formato, string extensaoEsperada, string contentTypeEsperado)
    {
        ValidadorAssinaturaImagem.ExtensaoPara(formato).Should().Be(extensaoEsperada);
        ValidadorAssinaturaImagem.ContentTypePara(formato).Should().Be(contentTypeEsperado);
    }
}
