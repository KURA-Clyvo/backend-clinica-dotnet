namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.DTOs.Pet;
using Kura.Application.Validators;

public class PetFotoUploadValidatorTests
{
    private readonly PetFotoUploadValidator _sut = new();

    [Theory]
    [InlineData(nameof(FormFileFixtures.JpegValido))]
    [InlineData(nameof(FormFileFixtures.PngValido))]
    [InlineData(nameof(FormFileFixtures.WebpValido))]
    public async Task Validate_ThumbEMediaValidosDoMesmoFormato_SemErros(string nomeFixture)
    {
        var bytes = nomeFixture switch
        {
            nameof(FormFileFixtures.JpegValido) => FormFileFixtures.JpegValido,
            nameof(FormFileFixtures.PngValido) => FormFileFixtures.PngValido,
            _ => FormFileFixtures.WebpValido,
        };

        var dto = new PetFotoUploadDto
        {
            Thumb = FormFileFixtures.CriarStream(bytes),
            Media = FormFileFixtures.CriarStream(bytes),
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeTrue(
            string.Join(", ", resultado.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task Validate_ThumbAusente_Invalido()
    {
        var dto = new PetFotoUploadDto
        {
            Thumb = null,
            Media = FormFileFixtures.CriarStream(FormFileFixtures.WebpValido),
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(PetFotoUploadDto.Thumb));
    }

    [Fact]
    public async Task Validate_MediaAusente_Invalido()
    {
        var dto = new PetFotoUploadDto
        {
            Thumb = FormFileFixtures.CriarStream(FormFileFixtures.WebpValido),
            Media = null,
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(PetFotoUploadDto.Media));
    }

    [Fact]
    public async Task Validate_ThumbVazio_Invalido()
    {
        var dto = new PetFotoUploadDto
        {
            Thumb = FormFileFixtures.CriarStream([]),
            Media = FormFileFixtures.CriarStream(FormFileFixtures.WebpValido),
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Aceite 2 da FT-03: "bytes que não batem a assinatura com Content-Type: image/jpeg → 400".
    /// Aqui provamos a metade que o validator controla — IsValid == false — e a HTTP real
    /// (400 de fato) é medida em PetFotoHttpTests.
    /// </summary>
    [Fact]
    public async Task Validate_ThumbComBytesInvalidosEContentTypeJpegMentiroso_Invalido()
    {
        var dto = new PetFotoUploadDto
        {
            Thumb = FormFileFixtures.CriarStream(FormFileFixtures.BytesInvalidos),
            Media = FormFileFixtures.CriarStream(FormFileFixtures.WebpValido),
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(PetFotoUploadDto.Thumb));
    }

    [Fact]
    public async Task Validate_ThumbEMediaDeFormatosDiferentes_Invalido()
    {
        // Ruling F7-a do maestro (G2 g2-ft01-ft02.md): thumb JPEG + media PNG → 400.
        var dto = new PetFotoUploadDto
        {
            Thumb = FormFileFixtures.CriarStream(FormFileFixtures.JpegValido),
            Media = FormFileFixtures.CriarStream(FormFileFixtures.PngValido),
        };

        var resultado = await _sut.ValidateAsync(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("mesmo formato"));
    }
}
