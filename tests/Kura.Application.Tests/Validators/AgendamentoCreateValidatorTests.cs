namespace Kura.Application.Tests.Validators;

using FluentAssertions;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Validators;

/// <summary>
/// REC-10 — a metade "forma do payload" da validação. A metade relacional/temporal (tutor,
/// pet, veterinário, triagem de origem, encaixe) mora em <c>AgendaService.CriarAsync</c> e é
/// provada em <c>AgendaServiceTests</c>/<c>CrossTenantRegressionTests</c>.
/// </summary>
public class AgendamentoCreateValidatorTests
{
    private readonly AgendamentoCreateValidator _sut = new();

    private static AgendamentoCreateDto Dto(
        long idTutor = 1,
        long idPet = 1,
        long idVeterinario = 1,
        string dsTipo = "CONSULTA",
        int? duracao = null,
        string? dsObservacoes = null,
        DateTime? dtAgendamento = null) => new()
    {
        IdTutor = idTutor,
        IdPet = idPet,
        IdVeterinario = idVeterinario,
        DtAgendamento = dtAgendamento ?? new DateTime(2026, 10, 7, 9, 0, 0),
        DsTipo = dsTipo,
        Duracao = duracao,
        DsObservacoes = dsObservacoes
    };

    [Fact]
    public void Validate_DtoValido_NaoRetornaErro()
    {
        _sut.Validate(Dto()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdTutorNaoPositivo_RetornaErro(long idTutor)
    {
        _sut.Validate(Dto(idTutor: idTutor)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdPetNaoPositivo_RetornaErro(long idPet)
    {
        _sut.Validate(Dto(idPet: idPet)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IdVeterinarioNaoPositivo_RetornaErro(long idVeterinario)
    {
        _sut.Validate(Dto(idVeterinario: idVeterinario)).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// G0 item 9 — no Java, esta lista existe SÓ em <c>@Schema(allowableValues=...)</c>
    /// (Swagger, não validação). O REC-10 é mais estrito de propósito.
    /// </summary>
    [Theory]
    [InlineData("CONSULTA")]
    [InlineData("RETORNO")]
    [InlineData("VACINA")]
    [InlineData("EXAME")]
    [InlineData("PROCEDIMENTO")]
    [InlineData("TELEORIENTACAO")]
    public void Validate_TipoDaListaPermitida_NaoRetornaErro(string tipo)
    {
        _sut.Validate(Dto(dsTipo: tipo)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("consulta")] // caixa
    [InlineData("URGENCIA")] // fora da lista (não é um dos 6 valores)
    public void Validate_TipoForaDaLista_RetornaErro(string tipo)
    {
        _sut.Validate(Dto(dsTipo: tipo)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(480)]
    public void Validate_DuracaoDentroDaFaixa_NaoRetornaErro(int duracao)
    {
        _sut.Validate(Dto(duracao: duracao)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    public void Validate_DuracaoForaDaFaixa_RetornaErro(int duracao)
    {
        _sut.Validate(Dto(duracao: duracao)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuracaoAusente_NaoRetornaErro()
    {
        // O default (30) é aplicado no service, não aqui -- "ausente" é um estado válido do DTO.
        _sut.Validate(Dto(duracao: null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsObservacoesAcimaDe1000Caracteres_RetornaErro()
    {
        _sut.Validate(Dto(dsObservacoes: new string('a', 1001))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DsObservacoesAusente_NaoRetornaErro()
    {
        _sut.Validate(Dto(dsObservacoes: null)).IsValid.Should().BeTrue();
    }

    // ---------- G2 I-2: DsObservacoes validado por BYTES UTF-8, não caracteres ----------

    [Fact]
    public void Validate_DsObservacoes1000BytesAscii_NaoRetornaErro()
    {
        // 1000 caracteres ASCII = 1000 bytes UTF-8 -- exatamente o limite.
        _sut.Validate(Dto(dsObservacoes: new string('a', 1000))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DsObservacoes999CaracteresAcentuados_AcimaDe1000Bytes_RetornaErro()
    {
        // G2 I-2 -- 999 caracteres acentuados (2 bytes cada em UTF-8) = 1998 bytes, acima
        // do limite de 1000 -- mas ABAIXO de 1000 CARACTERES, então o validator antigo
        // (MaximumLength(1000), que conta caracteres) deixava passar e o Oracle real
        // estourava ORA-12899 (500). Prova de que a contagem é por BYTE agora.
        var texto = new string('á', 999);
        System.Text.Encoding.UTF8.GetByteCount(texto).Should().BeGreaterThan(1000, "setup do teste: precisa exceder 1000 bytes para ser o cenário do achado");
        texto.Length.Should().BeLessThan(1000, "setup do teste: precisa ficar abaixo de 1000 CARACTERES, senão MaximumLength(1000) já pegaria por acidente");

        _sut.Validate(Dto(dsObservacoes: texto)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DsObservacoes500CaracteresAcentuados_DentroDe1000Bytes_NaoRetornaErro()
    {
        // 500 caracteres acentuados = 1000 bytes -- ainda dentro do limite.
        var texto = new string('á', 500);
        System.Text.Encoding.UTF8.GetByteCount(texto).Should().Be(1000);

        _sut.Validate(Dto(dsObservacoes: texto)).IsValid.Should().BeTrue();
    }

    // ---------- G2 I-1: DtAgendamento sem fuso (Kind == Unspecified) ----------

    [Fact]
    public void Validate_DtAgendamentoComKindUnspecified_NaoRetornaErro()
    {
        var dt = new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Unspecified);
        _sut.Validate(Dto(dtAgendamento: dt)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DtAgendamentoComKindUtc_RetornaErro()
    {
        // G2 I-1 -- equivalente ao que "...T10:00:00Z" produz na deserialização.
        var dt = DateTime.SpecifyKind(new DateTime(2026, 10, 7, 9, 0, 0), DateTimeKind.Utc);
        _sut.Validate(Dto(dtAgendamento: dt)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DtAgendamentoComKindLocal_RetornaErro()
    {
        // G2 I-1 -- equivalente ao que "...T10:00:00-03:00" produz na deserialização
        // (System.Text.Json converte offset explícito para Kind=Local).
        var dt = DateTime.SpecifyKind(new DateTime(2026, 10, 7, 9, 0, 0), DateTimeKind.Local);
        _sut.Validate(Dto(dtAgendamento: dt)).IsValid.Should().BeFalse();
    }
}
