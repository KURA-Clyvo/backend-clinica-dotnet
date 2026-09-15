namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Luna;
using Kura.Application.Validators;

public class TriageRequestValidatorTests
{
    private readonly TriageRequestValidator _sut = new();

    private static TriageRequestDto ValidDto(
        string dsUrgencia = "ALTA", string? regrasVersao = null, int nrScore = 80) => new()
    {
        IdInteracao = 100,
        IdTutor = 7,
        Sintomas = ["vomito"],
        DsUrgencia = dsUrgencia,
        NrScore = nrScore,
        DsRecomendacao = "Levar ao veterinário",
        DsRegrasVersao = regrasVersao
    };

    [Fact]
    public void Validate_DadosValidos_NaoRetornaErros()
    {
        // Act
        var resultado = _sut.Validate(ValidDto());

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("BAIXA")]
    [InlineData("MEDIA")]
    [InlineData("ALTA")]
    public void Validate_DsUrgenciaValida_NaoRetornaErro(string urgencia)
    {
        // Act
        var resultado = _sut.Validate(ValidDto(dsUrgencia: urgencia));

        // Assert
        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsUrgenciaForaDoEnum_RetornaErro()
    {
        // Act
        var resultado = _sut.Validate(ValidDto(dsUrgencia: "CRITICA"));

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.DsUrgencia));
    }

    [Fact]
    public void Validate_IdInteracaoZero_RetornaErro()
    {
        // Arrange
        var dto = new TriageRequestDto
        {
            IdInteracao = 0,
            IdTutor = 7,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 80,
            DsRecomendacao = "Levar ao veterinário"
        };

        // Act
        var resultado = _sut.Validate(dto);

        // Assert
        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.IdInteracao));
    }

    // ── Fix wave 1 (IMPORTANTE-2, lu-08-revisao.md frentes 3/4) ─────────────────────
    // Tamanhos lidos da V21 real (backend-tutor-java `main` @ 3fb8ea3): DS_REGRAS_VERSAO
    // VARCHAR2(10) [BYTE], NR_SCORE NUMBER(5). Antes deste fix os dois campos podiam
    // estourar o Oracle com 500 (ORA-12899 / ORA-01438), reproduzido pela G2.

    [Fact]
    public void Validate_RegrasVersaoComExatamente10BytesAscii_NaoRetornaErro()
    {
        // 10 caracteres ASCII = 10 bytes UTF-8 — no limite exato da coluna.
        var resultado = _sut.Validate(ValidDto(regrasVersao: new string('1', 10)));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RegrasVersaoComExatamente11BytesAscii_RetornaErro()
    {
        var resultado = _sut.Validate(ValidDto(regrasVersao: new string('1', 11)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.DsRegrasVersao));
    }

    [Fact]
    public void Validate_RegrasVersaoComExatamente10BytesMultibyte_NaoRetornaErro()
    {
        // 5 caracteres 'ã' = 10 bytes UTF-8 (2 bytes cada) — 5 <= 10 caracteres já
        // passaria na regra ANTIGA (MaximumLength(10) por char); prova que a regra nova
        // (por bytes) também aceita quando o total de bytes está no limite exato.
        var resultado = _sut.Validate(ValidDto(regrasVersao: new string('ã', 5)));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RegrasVersaoMultibyteAcimaDoLimiteDeBytes_RetornaErro()
    {
        // 6 caracteres 'ã' = 6 CARACTERES (passaria pela regra ANTIGA MaximumLength(10))
        // mas 12 BYTES UTF-8 — estoura VARCHAR2(10 BYTE) no Oracle (ORA-12899, achado da
        // G2 reproduzido contra Oracle real). Prova que a checagem é por bytes, não char.
        var resultado = _sut.Validate(ValidDto(regrasVersao: new string('ã', 6)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.DsRegrasVersao));
    }

    [Fact]
    public void Validate_NrScoreZero_NaoRetornaErro()
    {
        var resultado = _sut.Validate(ValidDto(nrScore: 0));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NrScoreNoLimiteSuperiorDaColuna_NaoRetornaErro()
    {
        // NUMBER(5) sem escala — 99999 é o maior inteiro que cabe.
        var resultado = _sut.Validate(ValidDto(nrScore: 99999));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NrScoreAcimaDoLimiteDaColuna_RetornaErro()
    {
        // 100000 estoura NUMBER(5) com ORA-01438, reproduzido pela G2 contra Oracle real.
        var resultado = _sut.Validate(ValidDto(nrScore: 100000));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.NrScore));
    }

    [Fact]
    public void Validate_NrScoreNegativo_RetornaErro()
    {
        var resultado = _sut.Validate(ValidDto(nrScore: -1));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TriageRequestDto.NrScore));
    }
}
