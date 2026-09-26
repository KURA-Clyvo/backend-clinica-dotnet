namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Validators;

/// <summary>
/// REC-01 (KURA_BACKLOG_RECEPCAO.md): cobertura do contrato de 400 que
/// <c>AddFluentValidationAutoValidation()</c> converte automaticamente (ModelState inválido) —
/// diferente da defesa em profundidade no <c>TutorService</c> (que lança
/// <c>RegraDeNegocioException</c>, 422, coberta em <c>TutorServiceTests</c>). Estes testes
/// exercitam o validator em si, que é onde o 400 real do aceite da REC-01 nasce.
/// </summary>
public class TutorCreateValidatorTests
{
    private readonly TutorCreateValidator _sut = new();

    private static TutorCreateDto ValidDto(
        string nrTelefone = "11999999999",
        string? dsWhatsapp = null,
        bool stAvisoPrivacidadeInformado = true,
        string dsCanalConvite = "WHATSAPP") => new()
    {
        NmTutor = "Maria Silva",
        NrCpf = "12345678901",
        DsEmail = "maria@email.com",
        NrTelefone = nrTelefone,
        DsWhatsapp = dsWhatsapp,
        StAvisoPrivacidadeInformado = stAvisoPrivacidadeInformado,
        DsCanalConvite = dsCanalConvite
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        _sut.Validate(ValidDto()).IsValid.Should().BeTrue();
    }

    // ── A-9: aviso de privacidade — mordida (a) do aceite da REC-01 ─────────

    [Fact]
    public void Validate_StAvisoPrivacidadeInformadoFalse_RetornaErro()
    {
        // Mordida (a): sem esta checagem, um dto com o aviso NÃO confirmado passaria — este
        // teste é o que fica vermelho se a regra Equal(true) for removida.
        var dto = ValidDto(stAvisoPrivacidadeInformado: false);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorCreateDto.StAvisoPrivacidadeInformado));
    }

    [Fact]
    public void Validate_StAvisoPrivacidadeInformadoTrue_NaoRetornaErroNesseCampo()
    {
        var dto = ValidDto(stAvisoPrivacidadeInformado: true);

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorCreateDto.StAvisoPrivacidadeInformado));
    }

    // ── A-12: telefone obrigatório e validado por formato ───────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NrTelefoneAusente_RetornaErro(string nrTelefoneBruto)
    {
        // REC-01 inverte de propósito o contrato da TASK-60 nesta rota: antes, um telefone
        // ausente passava (200) e o service coalescia para o sentinela "Não informado".
        var dto = ValidDto(nrTelefone: nrTelefoneBruto);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorCreateDto.NrTelefone));
    }

    [Theory]
    [InlineData("123")] // menos de 10 dígitos, sem "+"
    [InlineData("abcdefghij")] // sem nenhum dígito
    [InlineData("12345678901234")] // 14 dígitos, sem DDI reconhecido
    public void Validate_NrTelefoneFormatoInvalido_RetornaErro(string nrTelefoneInvalido)
    {
        var dto = ValidDto(nrTelefone: nrTelefoneInvalido);

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorCreateDto.NrTelefone));
    }

    [Theory]
    [InlineData("(11) 98765-4321")]
    [InlineData("11987654321")]
    [InlineData("+55 11 98765-4321")]
    [InlineData("(11) 3456-7890")]
    [InlineData("+1 415 555 0100")]
    public void Validate_NrTelefoneFormatoValido_NaoRetornaErroNesseCampo(string nrTelefoneValido)
    {
        var dto = ValidDto(nrTelefone: nrTelefoneValido);

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorCreateDto.NrTelefone));
    }

    // ── A-12: DsWhatsapp opcional, mas valida formato quando vem ────────────

    [Fact]
    public void Validate_DsWhatsappAusente_NaoRetornaErro()
    {
        var dto = ValidDto(dsWhatsapp: null);

        var resultado = _sut.Validate(dto);

        resultado.Errors.Should().NotContain(e => e.PropertyName == nameof(TutorCreateDto.DsWhatsapp));
    }

    [Fact]
    public void Validate_DsWhatsappFormatoInvalido_RetornaErro()
    {
        var dto = ValidDto(dsWhatsapp: "123");

        var resultado = _sut.Validate(dto);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(TutorCreateDto.DsWhatsapp));
    }
}
