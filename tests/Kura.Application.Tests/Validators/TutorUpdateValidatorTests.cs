namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Validators;

/// <summary>
/// REC-01 (KURA_BACKLOG_RECEPCAO.md, G0 item 4): o PUT NÃO exige telefone (recomendação do
/// maestro — não quebrar edição parcial), mas valida o FORMATO quando o campo vem preenchido.
/// </summary>
public class TutorUpdateValidatorTests
{
    private readonly TutorUpdateValidator _sut = new();

    private static TutorUpdateDto ValidDto(string nrTelefone = "11999999999") => new()
    {
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = nrTelefone
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        _sut.Validate(ValidDto()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NrTelefoneAusente_NaoRetornaErro(string nrTelefoneBruto)
    {
        // Diferente do TutorCreateValidator: o PUT continua NÃO exigindo telefone (edição
        // parcial de tutor antigo sem telefone tem que continuar possível).
        var dto = ValidDto(nrTelefone: nrTelefoneBruto);

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorUpdateDto.NrTelefone));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghij")]
    [InlineData("12345678901234")]
    public void Validate_NrTelefonePreenchidoComFormatoInvalido_RetornaErro(string nrTelefoneInvalido)
    {
        var dto = ValidDto(nrTelefone: nrTelefoneInvalido);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorUpdateDto.NrTelefone));
    }

    [Theory]
    [InlineData("(11) 98765-4321")]
    [InlineData("11987654321")]
    [InlineData("+55 11 98765-4321")]
    [InlineData("(11) 3456-7890")]
    [InlineData("+1 415 555 0100")]
    public void Validate_NrTelefonePreenchidoComFormatoValido_NaoRetornaErro(string nrTelefoneValido)
    {
        var dto = ValidDto(nrTelefone: nrTelefoneValido);

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorUpdateDto.NrTelefone));
    }

    // ── I1 (G2 fix wave, achado Important #1): DsWhatsapp opcional no PUT ───

    [Fact]
    public void Validate_DsWhatsappAusente_NaoRetornaErro()
    {
        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "11999999999", DsWhatsapp = null
        };

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorUpdateDto.DsWhatsapp));
    }

    [Fact]
    public void Validate_DsWhatsappFormatoInvalido_RetornaErro()
    {
        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "11999999999", DsWhatsapp = "123"
        };

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorUpdateDto.DsWhatsapp));
    }

    [Fact]
    public void Validate_DsWhatsappFormatoValido_NaoRetornaErro()
    {
        var dto = new TutorUpdateDto
        {
            NmTutor = "Maria Silva", NrCpf = "12345678901", DsEmail = "maria@email.com",
            NrTelefone = "11999999999", DsWhatsapp = "+1 415 555 0100"
        };

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorUpdateDto.DsWhatsapp));
    }
}
