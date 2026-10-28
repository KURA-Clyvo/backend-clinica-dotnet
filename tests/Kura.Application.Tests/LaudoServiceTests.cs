namespace Kura.Application.Tests;

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Kura.Application.DTOs.Laudo;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Laudos;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>S4-NET-03 — LaudoService com repositório e storage mockados.</summary>
public class LaudoServiceTests
{
    private const long IdClinica = 5;
    private const string Email = "vet@clinica.test";

    private static readonly byte[] PdfMinimo = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\n%%EOF");

    private readonly Mock<ILaudoRepository> _laudos = new();
    private readonly Mock<IPetRepository> _pets = new();
    private readonly Mock<IUsuarioClinicaRepository> _usuarios = new();
    private readonly Mock<IArmazenamentoArquivos> _storage = new();
    private readonly Mock<IClinicaContext> _clinica = new();
    private readonly LaudoService _sut;

    public LaudoServiceTests()
    {
        _clinica.SetupGet(c => c.IdClinica).Returns(IdClinica);
        _pets.Setup(p => p.GetByIdComVinculosAsync(10, IdClinica)).ReturnsAsync(new Pet { Id = 10, IdClinica = IdClinica });
        _usuarios.Setup(u => u.BuscarAtivoPorEmailNaClinicaAsync(IdClinica, Email))
            .ReturnsAsync(new UsuarioClinica { Id = 77, IdClinica = IdClinica, DsEmail = Email });
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long _, Laudo l, CancellationToken _) => { l.Id = "671f00000000000000000001"; return l; });

        _sut = new LaudoService(
            _laudos.Object, _pets.Object, _usuarios.Object, _storage.Object, _clinica.Object,
            NullLogger<LaudoService>.Instance);
    }

    private static LaudoCriarDto Dto(byte[]? arquivo = null, string? dados = null) => new()
    {
        Tipo = "HEMOGRAMA",
        Titulo = "  Hemograma completo ",
        DataExame = "2026-10-01",
        Laboratorio = "Lab X",
        Dados = dados,
        Arquivo = new MemoryStream(arquivo ?? PdfMinimo),
    };

    [Fact]
    public async Task CriarAsync_HappyPath_GravaArquivoDepoisDocumentoComSha256EChaveDaClinica()
    {
        // Arrange
        var ordem = new List<string>();
        _storage.Setup(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), "application/pdf", It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("arquivo")).Returns(Task.CompletedTask);
        Laudo? gravado = null;
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .Callback<long, Laudo, CancellationToken>((_, l, _) => { ordem.Add("documento"); gravado = l; })
            .ReturnsAsync((long _, Laudo l, CancellationToken _) => { l.Id = "671f00000000000000000001"; return l; });

        // Act
        var resposta = await _sut.CriarAsync(10, Dto(), Email, CancellationToken.None);

        // Assert
        ordem.Should().Equal("arquivo", "documento");
        gravado!.IdUsuarioAutor.Should().Be(77);
        gravado.Titulo.Should().Be("Hemograma completo");
        gravado.DataExame.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        gravado.Arquivo.ContentType.Should().Be("application/pdf");
        gravado.Arquivo.TamanhoBytes.Should().Be(PdfMinimo.Length);
        gravado.Arquivo.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(PdfMinimo)));
        gravado.Arquivo.Chave.Should().MatchRegex($@"^clinica/{IdClinica}/pet/10/laudo/[0-9a-f]{{32}}\.pdf$");
        resposta.Id.Should().Be("671f00000000000000000001");
        resposta.Arquivo.Sha256.Should().Be(gravado.Arquivo.Sha256);
    }

    [Fact]
    public async Task CriarAsync_PetDeOutraClinica_Lanca404_ENaoGravaNada()
    {
        _pets.Setup(p => p.GetByIdComVinculosAsync(99, IdClinica)).ReturnsAsync((Pet?)null);

        var act = () => _sut.CriarAsync(99, Dto(), Email, CancellationToken.None);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        _storage.Verify(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _laudos.Verify(l => l.InserirAsync(It.IsAny<long>(), It.IsAny<Laudo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_RepositorioFalha_ApagaOArquivoRecemGravadoEPropaga()
    {
        string? chaveSalva = null;
        _storage.Setup(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((k, _, _, _) => chaveSalva = k).Returns(Task.CompletedTask);
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArmazenamentoDocumentosIndisponivelException("fora"));

        var act = () => _sut.CriarAsync(10, Dto(), Email, CancellationToken.None);

        await act.Should().ThrowAsync<ArmazenamentoDocumentosIndisponivelException>();
        _storage.Verify(s => s.ExcluirAsync(chaveSalva!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_FalhaAoApagarOrfao_NaoMascaraAExcecaoOriginal()
    {
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LaudoRecusadoPeloSchemaException());
        _storage.Setup(s => s.ExcluirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disco"));

        var act = () => _sut.CriarAsync(10, Dto(), Email, CancellationToken.None);

        await act.Should().ThrowAsync<LaudoRecusadoPeloSchemaException>();
    }

    [Fact]
    public async Task CriarAsync_ArquivoQueNaoEPdfNemImagem_Lanca422ENaoGrava()
    {
        var act = () => _sut.CriarAsync(10, Dto(arquivo: Encoding.ASCII.GetBytes("isto nao e um pdf")), Email, CancellationToken.None);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _storage.Verify(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_ContentTypeVemDaAssinatura_NuncaDoCliente()
    {
        Laudo? gravado = null;
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .Callback<long, Laudo, CancellationToken>((_, l, _) => gravado = l)
            .ReturnsAsync((long _, Laudo l, CancellationToken _) => l);

        await _sut.CriarAsync(10, Dto(arquivo: FormFileFixtures.PngValido), Email, CancellationToken.None);

        gravado!.Arquivo.ContentType.Should().Be("image/png");
        gravado.Arquivo.Chave.Should().EndWith(".png");
    }

    [Fact]
    public async Task CriarAsync_DadosComObjetoAninhadoArrayENumero_ViramTiposDotNetAceitosPeloRepositorio()
    {
        Laudo? gravado = null;
        _laudos.Setup(l => l.InserirAsync(IdClinica, It.IsAny<Laudo>(), It.IsAny<CancellationToken>()))
            .Callback<long, Laudo, CancellationToken>((_, l, _) => gravado = l)
            .ReturnsAsync((long _, Laudo l, CancellationToken _) => l);

        await _sut.CriarAsync(10, Dto(dados: """{"hematocrito":42.5,"leucocitos":9000,"ok":true,"nada":null,"faixa":{"min":37,"max":55},"series":[1,2.5,"x",{"a":1}]}"""), Email, CancellationToken.None);

        var d = gravado!.Dados;
        d["hematocrito"].Should().Be(42.5);
        d["leucocitos"].Should().Be(9000L);
        d["ok"].Should().Be(true);
        d["nada"].Should().BeNull();
        d["faixa"].Should().BeOfType<Dictionary<string, object?>>().Which["min"].Should().Be(37L);
        var serie = d["series"].Should().BeOfType<List<object?>>().Subject;
        serie.Should().HaveCount(4);
        serie[1].Should().Be(2.5);
        serie[3].Should().BeOfType<Dictionary<string, object?>>();
        // Nenhum JsonElement sobrevive: era ele que dava ArgumentException cru no repositorio (G2-m3).
        d.Values.Should().NotContain(v => v is System.Text.Json.JsonElement);
    }

    [Fact]
    public async Task CriarAsync_ArquivoAcimaDe10MiB_Lanca422_SemDependerDoValidador()
    {
        // G2-m1: defesa em profundidade (D-NET-1). O service barra sozinho.
        var grande = new byte[(10 * 1024 * 1024) + 1];
        PdfMinimo.CopyTo(grande, 0);

        var act = () => _sut.CriarAsync(10, Dto(arquivo: grande), Email, CancellationToken.None);

        await act.Should().ThrowAsync<RegraDeNegocioException>().WithMessage("*10 MiB*");
        _storage.Verify(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_ArquivoVazio_Lanca422()
    {
        var act = () => _sut.CriarAsync(10, Dto(arquivo: []), Email, CancellationToken.None);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task CriarAsync_UsuarioDesativado_Lanca401_PoisSoOLookupDeAtivosEhUsado()
    {
        // G2-m2: o service usa o lookup de ATIVOS; o antigo (que enxerga desativado) nunca e chamado.
        _usuarios.Setup(u => u.BuscarAtivoPorEmailNaClinicaAsync(IdClinica, "off@x.test")).ReturnsAsync((UsuarioClinica?)null);

        var act = () => _sut.CriarAsync(10, Dto(), "off@x.test", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _usuarios.Verify(u => u.BuscarPorEmailNaClinicaAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>()), Times.Never);
    }

    [Theory]
    [InlineData("""{"x":1e400}""")]
    [InlineData("""{"x":-1e400}""")]
    [InlineData("""{"a\u0000b":1}""")]
    public async Task CriarAsync_DadosNaoFinitoOuChaveComNul_Lanca422ENaoGrava(string dados)
    {
        var act = () => _sut.CriarAsync(10, Dto(dados: dados), Email, CancellationToken.None);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _storage.Verify(s => s.SalvarAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAsync_UsuarioDoTokenSemCorrespondenciaNaClinica_Lanca401()
    {
        var act = () => _sut.CriarAsync(10, Dto(), "fantasma@x.test", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ObterAsync_LaudoDeOutraClinica_Lanca404()
    {
        _laudos.Setup(l => l.ObterAsync(IdClinica, "abc", It.IsAny<CancellationToken>())).ReturnsAsync((Laudo?)null);

        var act = () => _sut.ObterAsync("abc", CancellationToken.None);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ObterArquivoAsync_ContentTypeForaDoMapaFechado_VaiComoOctetStream()
    {
        _laudos.Setup(l => l.ObterAsync(IdClinica, "id1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Laudo { Id = "id1", Arquivo = new ArquivoLaudo { Chave = "k", ContentType = "text/html" } });
        _storage.Setup(s => s.AbrirAsync("k", It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream([1]));

        var arq = await _sut.ObterArquivoAsync("id1", CancellationToken.None);

        arq.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task ExcluirAsync_NaoEncontrado_Lanca404()
    {
        _laudos.Setup(l => l.ExcluirLogicoAsync(IdClinica, "x", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => _sut.ExcluirAsync("x", CancellationToken.None);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task ListarPorPetAsync_PassaFiltrosEOrdemEMapeiaTotal()
    {
        ConsultaLaudos? recebida = null;
        _laudos.Setup(l => l.ListarPorPetAsync(IdClinica, 10, It.IsAny<ConsultaLaudos>(), It.IsAny<CancellationToken>()))
            .Callback<long, long, ConsultaLaudos, CancellationToken>((_, _, c, _) => recebida = c)
            .ReturnsAsync(new PaginaLaudos([], 2, 5, 11));

        var r = await _sut.ListarPorPetAsync(10, new LaudoListarDto(2, 5, OrdenacaoLaudo.CriadoEmAsc, TipoLaudo.IMAGEM, null, null), CancellationToken.None);

        recebida!.Should().BeEquivalentTo(new ConsultaLaudos(2, 5, TipoLaudo.IMAGEM, null, null, OrdenacaoLaudo.CriadoEmAsc));
        r.Total.Should().Be(11);
        r.Page.Should().Be(2);
    }
}
