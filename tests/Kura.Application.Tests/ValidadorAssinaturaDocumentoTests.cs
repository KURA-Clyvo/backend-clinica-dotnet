namespace Kura.Application.Tests;

using System.Text;
using FluentAssertions;
using FluentValidation;
using Kura.Application.DTOs.Laudo;
using Kura.Application.Services;
using Kura.Application.Validators;

/// <summary>S4-NET-03 — assinatura de documento (PDF + imagens) e validador do multipart de laudo.</summary>
public class ValidadorAssinaturaDocumentoTests
{
    private static readonly byte[] PdfMinimo = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");

    [Fact]
    public void Detectar_PdfReal_DevolvePdf() =>
        ValidadorAssinaturaDocumento.Detectar(new MemoryStream(PdfMinimo)).Should().Be(ValidadorAssinaturaDocumento.Formato.Pdf);

    [Fact]
    public void Detectar_ArquivoDePdfComBytesDeTexto_DevolveNull() =>
        ValidadorAssinaturaDocumento.Detectar(new MemoryStream(Encoding.ASCII.GetBytes("Isto parece um laudo, mas e texto"))).Should().BeNull();

    [Fact]
    public void Detectar_PdfCurtoDemais_DevolveNull() =>
        ValidadorAssinaturaDocumento.Detectar(new MemoryStream(Encoding.ASCII.GetBytes("%PDF"))).Should().BeNull();

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    public void Detectar_Imagens_ReusaARegraDeImagem(string qual)
    {
        var (bytes, esperado) = qual switch
        {
            "jpeg" => (FormFileFixtures.JpegValido, ValidadorAssinaturaDocumento.Formato.Jpeg),
            "png" => (FormFileFixtures.PngValido, ValidadorAssinaturaDocumento.Formato.Png),
            _ => (FormFileFixtures.WebpValido, ValidadorAssinaturaDocumento.Formato.WebP),
        };
        ValidadorAssinaturaDocumento.Detectar(new MemoryStream(bytes)).Should().Be(esperado);
    }

    [Fact]
    public void Detectar_DeixaOStreamEmZero()
    {
        var s = new MemoryStream(PdfMinimo);
        ValidadorAssinaturaDocumento.Detectar(s);
        s.Position.Should().Be(0);
    }

    [Fact]
    public void ContentTypePara_TodosOsFormatosEstaoNoMapaFechado()
    {
        foreach (var f in Enum.GetValues<ValidadorAssinaturaDocumento.Formato>())
            ValidadorAssinaturaDocumento.ContentTypesPermitidos.Should().Contain(ValidadorAssinaturaDocumento.ContentTypePara(f));
    }

    // ---- validador do multipart ----

    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private static LaudoCriarDto Valido(Action<Dictionary<string, string?>>? mudar = null, byte[]? arquivo = null)
    {
        var c = new Dictionary<string, string?>
        {
            ["tipo"] = "HEMOGRAMA", ["titulo"] = "Hemograma", ["dataExame"] = "2026-10-01",
            ["laboratorio"] = "Lab", ["observacao"] = "obs", ["idEventoClinico"] = null, ["dados"] = """{"a":1}""",
        };
        mudar?.Invoke(c);
        return new LaudoCriarDto
        {
            Tipo = c["tipo"], Titulo = c["titulo"], DataExame = c["dataExame"], Laboratorio = c["laboratorio"],
            Observacao = c["observacao"], IdEventoClinico = c["idEventoClinico"], Dados = c["dados"],
            Arquivo = new MemoryStream(arquivo ?? PdfMinimo),
        };
    }

    private static FluentValidation.Results.ValidationResult Validar(LaudoCriarDto dto) =>
        new LaudoCriarValidator(new RelogioFixo(Agora)).Validate(dto);

    [Fact]
    public void Validador_EntradaCompleta_EhValida() => Validar(Valido()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("tipo", "RAIOX")]
    [InlineData("tipo", "1")]
    [InlineData("tipo", null)]
    [InlineData("titulo", "")]
    [InlineData("dataExame", "ontem")]
    [InlineData("dataExame", "2026-10-30")]   // futuro além de 1 dia
    [InlineData("idEventoClinico", "abc")]
    [InlineData("idEventoClinico", "-3")]
    [InlineData("dados", "[1,2]")]            // array nao e objeto
    [InlineData("dados", "42")]
    [InlineData("dados", "{quebrado")]
    public void Validador_CampoInvalido_Recusa(string campo, string? valor) =>
        Validar(Valido(c => c[campo] = valor)).IsValid.Should().BeFalse();

    [Fact]
    public void Validador_TextosAcimaDoLimite_Recusa()
    {
        Validar(Valido(c => c["titulo"] = new string('t', 121))).IsValid.Should().BeFalse();
        Validar(Valido(c => c["laboratorio"] = new string('l', 121))).IsValid.Should().BeFalse();
        Validar(Valido(c => c["observacao"] = new string('o', 1001))).IsValid.Should().BeFalse();
        Validar(Valido(c => c["titulo"] = new string('t', 120))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validador_DataExameAmanha_Aceita() =>
        Validar(Valido(c => c["dataExame"] = "2026-10-29")).IsValid.Should().BeTrue();

    [Fact]
    public void Validador_ArquivoComBytesDeTexto_RecusaMesmoQueOClienteChameDePdf() =>
        Validar(Valido(arquivo: Encoding.ASCII.GetBytes("nao sou pdf"))).Errors.Should().Contain(e => e.PropertyName == "Arquivo");

    [Fact]
    public void Validador_ArquivoAusente_Recusa()
    {
        var dto = new LaudoCriarDto { Tipo = "OUTRO", Titulo = "t", DataExame = "2026-10-01", Arquivo = null };
        Validar(dto).Errors.Should().Contain(e => e.PropertyName == "Arquivo");
    }

    [Fact]
    public void Validador_ArquivoAcimaDe10MiB_Recusa()
    {
        var grande = new byte[(10 * 1024 * 1024) + 1];
        PdfMinimo.CopyTo(grande, 0);
        Validar(Valido(arquivo: grande)).Errors.Should().Contain(e => e.ErrorMessage.Contains("10 MiB"));
    }
}
