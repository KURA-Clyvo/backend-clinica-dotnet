namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

public class PetFotoServiceTests
{
    private readonly Mock<IPetRepository> _petRepoMock = new();
    private readonly Mock<IArmazenamentoArquivos> _armazenamentoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<PetFotoService>> _loggerMock = new();
    private readonly PetFotoService _sut;

    public PetFotoServiceTests()
    {
        _sut = new PetFotoService(
            _petRepoMock.Object, _armazenamentoMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    private void SetupPet(long id, long idClinica = 1, string? dsFotoChave = null) =>
        _petRepoMock.Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync(new Pet { Id = id, IdClinica = idClinica, NmPet = "Rex", DsFotoChave = dsFotoChave });

    [Fact]
    public async Task UploadFotoAsync_PetInexistente_LancaEntidadeNaoEncontrada()
    {
        _petRepoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Pet?)null);

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "media", "image/webp");

        var act = async () => await _sut.UploadFotoAsync(99L, thumb, media, CancellationToken.None);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    /// <summary>
    /// Aceite 1 da FT-03: WebP válido → 200 (aqui: sem exceção) e linha atualizada
    /// (DsFotoChave/DtFotoAtualizacao preenchidos, formato refletido na extensão da chave).
    /// </summary>
    [Theory]
    [InlineData("webp")]
    [InlineData("jpg")]
    [InlineData("png")]
    public async Task UploadFotoAsync_ImagemValida_AtualizaPetEChamaArmazenamento(string extensaoEsperada)
    {
        SetupPet(1L);
        var bytes = extensaoEsperada switch
        {
            "jpg" => FormFileFixtures.JpegValido,
            "png" => FormFileFixtures.PngValido,
            _ => FormFileFixtures.WebpValido,
        };
        var thumb = FormFileFixtures.CriarArquivo(bytes, "thumb", "application/octet-stream");
        var media = FormFileFixtures.CriarArquivo(bytes, "media", "application/octet-stream");

        Pet? atualizado = null;
        _petRepoMock.Setup(r => r.Update(It.IsAny<Pet>())).Callback<Pet>(p => atualizado = p);

        var resultado = await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        atualizado.Should().NotBeNull();
        atualizado!.DsFotoChave.Should().NotBeNullOrEmpty();
        atualizado.DsFotoChave.Should().StartWith("clinica/1/pet/1/");
        atualizado.DsFotoChave.Should().EndWith($".{extensaoEsperada}");
        atualizado.DtFotoAtualizacao.Should().NotBeNull();

        resultado.DsFotoChave.Should().Be(atualizado.DsFotoChave);

        _armazenamentoMock.Verify(a => a.SalvarAsync(
            It.Is<string>(k => k.Contains("_256.") && k.EndsWith(extensaoEsperada)),
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _armazenamentoMock.Verify(a => a.SalvarAsync(
            It.Is<string>(k => k.Contains("_1080.") && k.EndsWith(extensaoEsperada)),
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    /// <summary>
    /// Mordida do aceite 5: a ordem tem que ser salva→commit→exclui. Prova positiva de que,
    /// COM foto anterior, os arquivos antigos SÃO excluídos (a mordida "exclusão antes do
    /// commit perde foto antiga" é provada por teste de integração, que consegue fazer o
    /// SaveChanges falhar de verdade — aqui provamos a ORDEM RELATIVA das chamadas de mock).
    /// </summary>
    [Fact]
    public async Task UploadFotoAsync_ComFotoAnterior_ExcluiVariantesAntigasSoDepoisDoCommit()
    {
        const string chaveAntiga = "clinica/1/pet/1/antigo-uuid.webp";
        SetupPet(1L, dsFotoChave: chaveAntiga);

        var sequencia = new MockSequence();
        _uowMock.InSequence(sequencia).Setup(u => u.CommitAsync()).ReturnsAsync(1);
        _armazenamentoMock.InSequence(sequencia)
            .Setup(a => a.ExcluirAsync("clinica/1/pet/1/antigo-uuid_256.webp", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _armazenamentoMock.InSequence(sequencia)
            .Setup(a => a.ExcluirAsync("clinica/1/pet/1/antigo-uuid_1080.webp", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "media", "image/webp");

        await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        _armazenamentoMock.Verify(a => a.ExcluirAsync(
            "clinica/1/pet/1/antigo-uuid_256.webp", It.IsAny<CancellationToken>()), Times.Once);
        _armazenamentoMock.Verify(a => a.ExcluirAsync(
            "clinica/1/pet/1/antigo-uuid_1080.webp", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Mordida do aceite 5, provada de verdade (não só ordem relativa): se
    /// <see cref="IUnitOfWork.CommitAsync"/> falhar, a foto antiga NUNCA pode ter sido
    /// excluída — senão o pet fica com <c>DsFotoChave</c> apontando pro valor antigo (o
    /// commit não foi persistido) e os arquivos que essa chave aponta já sumiram do disco:
    /// "foto antiga perdida", exatamente a frase do brief. Este teste passa com o código
    /// correto (exclui só DEPOIS do commit) e FALHARIA se alguém movesse a exclusão para
    /// antes do commit — verificado manualmente nesta task (mordida aplicada e revertida,
    /// resultado registrado no relatório).
    /// </summary>
    [Fact]
    public async Task UploadFotoAsync_CommitFalha_NuncaExcluiFotoAntiga()
    {
        const string chaveAntiga = "clinica/1/pet/1/antigo-uuid.webp";
        SetupPet(1L, dsFotoChave: chaveAntiga);
        _uowMock.Setup(u => u.CommitAsync()).ThrowsAsync(new InvalidOperationException("Oracle indisponível (simulado)"));

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "media", "image/webp");

        var act = async () => await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _armazenamentoMock.Verify(a => a.ExcluirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "commit falhou — a linha do pet continua apontando pra foto ANTIGA; excluir os " +
            "arquivos antigos aqui seria perder a foto que o banco ainda diz que existe");
    }

    [Fact]
    public async Task UploadFotoAsync_SemFotoAnterior_NaoChamaExcluir()
    {
        SetupPet(1L, dsFotoChave: null);

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "media", "image/webp");

        await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        _armazenamentoMock.Verify(a => a.ExcluirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Se o SEGUNDO arquivo (media) falha ao salvar, o PRIMEIRO (thumb) já salvo é excluído
    /// antes de propagar a exceção — nunca deixar órfão de escrita parcial.
    /// </summary>
    [Fact]
    public async Task UploadFotoAsync_FalhaAoSalvarMedia_ExcluiThumbJaSalvoEPropaga()
    {
        SetupPet(1L);

        string? chaveThumbSalva = null;
        _armazenamentoMock
            .Setup(a => a.SalvarAsync(
                It.Is<string>(k => k.Contains("_256.")), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((k, _, _, _) => chaveThumbSalva = k)
            .Returns(Task.CompletedTask);
        _armazenamentoMock
            .Setup(a => a.SalvarAsync(
                It.Is<string>(k => k.Contains("_1080.")), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disco cheio (simulado)"));

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "thumb", "image/webp");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.WebpValido, "media", "image/webp");

        var act = async () => await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        chaveThumbSalva.Should().NotBeNull();
        _armazenamentoMock.Verify(a => a.ExcluirAsync(chaveThumbSalva!, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never,
            "falha ao salvar o 2º arquivo não pode deixar a linha do pet apontando pra foto incompleta");
    }

    [Fact]
    public async Task UploadFotoAsync_ThumbEMediaFormatosDiferentes_LancaRegraDeNegocio()
    {
        // Defesa em profundidade do service (ver XML doc) — o caminho HTTP real já bloqueia
        // isso no validator (400, ruling F7-a); este teste prova a rede de segurança para
        // quem chamar o service diretamente, como este próprio teste faz.
        SetupPet(1L);

        var thumb = FormFileFixtures.CriarArquivo(FormFileFixtures.JpegValido, "thumb", "image/jpeg");
        var media = FormFileFixtures.CriarArquivo(FormFileFixtures.PngValido, "media", "image/png");

        var act = async () => await _sut.UploadFotoAsync(1L, thumb, media, CancellationToken.None);

        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }
}
