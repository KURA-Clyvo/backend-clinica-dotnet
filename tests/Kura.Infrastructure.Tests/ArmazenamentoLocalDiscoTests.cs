namespace Kura.Infrastructure.Tests;

using System.Text;
using FluentAssertions;
using Kura.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

/// <summary>
/// FT-02 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>): prova de mordida de
/// <see cref="ArmazenamentoLocalDisco"/> — em especial a defesa de path traversal extraída
/// de <c>ReceituarioPdfService.cs:110-118</c>. A mordida real (checagem de base removida,
/// teste falhando, restaurada) está registrada no artefato
/// <c>.superpowers/sdd/NEXT_EVENTO/ft-02-report.md</c>, não neste arquivo — o comentário
/// abaixo documenta o resultado medido.
/// </summary>
public sealed class ArmazenamentoLocalDiscoTests : IDisposable
{
    private readonly string _basePath;
    private readonly ArmazenamentoLocalDisco _sut;

    public ArmazenamentoLocalDiscoTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "kura-ft02-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_basePath);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _basePath,
            })
            .Build();

        _sut = new ArmazenamentoLocalDisco(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public async Task SalvarDepoisAbrir_DevolveOMesmoConteudo()
    {
        // Arrange
        var chave = "clinica/7/pet/12/foto_256.webp";
        var conteudoOriginal = Encoding.UTF8.GetBytes("conteudo-de-teste-da-foto");

        // Act
        await _sut.SalvarAsync(chave, new MemoryStream(conteudoOriginal), "image/webp", CancellationToken.None);
        await using var lido = await _sut.AbrirAsync(chave, CancellationToken.None);

        // Assert
        lido.Should().NotBeNull();
        using var ms = new MemoryStream();
        await lido!.CopyToAsync(ms);
        ms.ToArray().Should().Equal(conteudoOriginal);
    }

    [Fact]
    public async Task Abrir_ChaveInexistente_DevolveNull()
    {
        var lido = await _sut.AbrirAsync("clinica/7/pet/999/nao-existe.webp", CancellationToken.None);

        lido.Should().BeNull();
    }

    [Fact]
    public async Task Excluir_DuasVezes_NaoLanca()
    {
        // Arrange
        var chave = "clinica/7/pet/12/apagar.webp";
        await _sut.SalvarAsync(chave, new MemoryStream([1, 2, 3]), "image/webp", CancellationToken.None);

        // Act
        var act = async () =>
        {
            await _sut.ExcluirAsync(chave, CancellationToken.None);
            await _sut.ExcluirAsync(chave, CancellationToken.None);
        };

        // Assert
        await act.Should().NotThrowAsync();
        (await _sut.AbrirAsync(chave, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Excluir_ChaveQueNuncaExistiu_NaoLanca()
    {
        var act = async () => await _sut.ExcluirAsync("clinica/7/pet/12/nunca-existiu.webp", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("clinica/7/../../x")]
    [InlineData("a/../../x")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Salvar_ChaveComTraversalOuVazia_Lanca(string chaveInvalida)
    {
        var act = async () =>
            await _sut.SalvarAsync(chaveInvalida, new MemoryStream([1]), "image/webp", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Salvar_ChaveAbsoluta_Lanca()
    {
        var chaveAbsoluta = OperatingSystem.IsWindows() ? "C:\\Windows\\x" : "/etc/passwd";

        var act = async () =>
            await _sut.SalvarAsync(chaveAbsoluta, new MemoryStream([1]), "image/webp", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Salvar_ChaveComCaractereForaDaAllowlist_Lanca()
    {
        var act = async () =>
            await _sut.SalvarAsync("clinica/7/pet/12/foto;drop.webp", new MemoryStream([1]), "image/webp", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Abrir_ChaveComTraversal_Lanca()
    {
        var act = async () => await _sut.AbrirAsync("../../etc/passwd", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Excluir_ChaveComTraversal_Lanca()
    {
        var act = async () => await _sut.ExcluirAsync("../../etc/passwd", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Salvar_CriaSubdiretoriosNecessarios()
    {
        var chave = "clinica/99/pet/1234/subdir_novo.webp";

        await _sut.SalvarAsync(chave, new MemoryStream([9, 9]), "image/webp", CancellationToken.None);

        File.Exists(Path.Combine(_basePath, "clinica", "99", "pet", "1234", "subdir_novo.webp"))
            .Should().BeTrue();
    }
}
