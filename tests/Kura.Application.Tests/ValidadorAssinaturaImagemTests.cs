namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;

public class ValidadorAssinaturaImagemTests
{
    [Fact]
    public void DetectarAsync_JpegValido_DevolveJpeg()
    {
        var arquivo = FormFileFixtures.CriarArquivo(FormFileFixtures.JpegValido, "thumb", "image/jpeg");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.Jpeg);
    }

    [Fact]
    public void DetectarAsync_PngValido_DevolvePng()
    {
        var arquivo = FormFileFixtures.CriarArquivo(FormFileFixtures.PngValido, "thumb", "image/png");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.Png);
    }

    [Fact]
    public void DetectarAsync_WebPValido_DevolveWebP()
    {
        var arquivo = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
        formato.Should().Be(ValidadorAssinaturaImagem.Formato.WebP);
    }

    [Fact]
    public void DetectarAsync_BytesInvalidos_ComContentTypeJpegMentiroso_DevolveNull()
    {
        // A prova central da regra "magic bytes, não Content-Type do cliente": o
        // Content-Type diz image/jpeg, os bytes não são JPEG nenhum.
        var arquivo = FormFileFixtures.CriarArquivo(FormFileFixtures.BytesInvalidos, "thumb", "image/jpeg");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
        formato.Should().BeNull();
    }

    [Fact]
    public void DetectarAsync_ArquivoVazio_DevolveNull()
    {
        var arquivo = FormFileFixtures.CriarArquivo([], "thumb", "image/jpeg");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
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
        var arquivo = FormFileFixtures.CriarArquivo([0xFF, 0xD8], "thumb", "image/jpeg");
        var formato = ValidadorAssinaturaImagem.Detectar(arquivo);
        formato.Should().BeNull();
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
