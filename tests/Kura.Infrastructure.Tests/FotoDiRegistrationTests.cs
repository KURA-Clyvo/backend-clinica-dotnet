namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.Extensions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FT-02 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>): prova que <c>Foto:UrlSecret</c> ausente
/// ou curto demais derruba <c>ServiceCollectionExtensions.AddInfrastructure</c> — a MESMA
/// extensão que <c>Program.cs</c> chama na partida do processo — de forma SÍNCRONA e
/// IMEDIATA, sem precisar resolver nenhum serviço primeiro. Mesmo padrão de fail-fast já
/// usado para <c>Jwt:Key</c> em <c>Program.cs</c>.
/// </summary>
public sealed class FotoDiRegistrationTests
{
    private static IConfiguration ConfigComChaves(Dictionary<string, string?> chaves)
    {
        var baseChaves = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=teste-inerte;",
        };
        foreach (var (k, v) in chaves)
            baseChaves[k] = v;

        return new ConfigurationBuilder().AddInMemoryCollection(baseChaves).Build();
    }

    [Fact]
    public void AddInfrastructure_SemFotoUrlSecret_LancaNaChamadaSincrona()
    {
        // Arrange — config válida em tudo, exceto Foto:UrlSecret, que está AUSENTE.
        var configuration = ConfigComChaves([]);
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddInfrastructure(configuration);

        // Assert — a exceção estoura na CHAMADA do método de registro, não depois, no
        // primeiro Resolve(): é isso que prova "falha na partida", não "falha no primeiro uso".
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Foto:UrlSecret*");
    }

    [Fact]
    public void AddInfrastructure_ComFotoUrlSecretCurto_Lanca()
    {
        var configuration = ConfigComChaves(new Dictionary<string, string?>
        {
            ["Foto:UrlSecret"] = "muito-curto",
        });
        var services = new ServiceCollection();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Foto:UrlSecret*");
    }

    [Fact]
    public void AddInfrastructure_ComFotoUrlSecretValido_RegistraOsDoisServicos()
    {
        var configuration = ConfigComChaves(new Dictionary<string, string?>
        {
            ["Foto:UrlSecret"] = "chave-de-teste-com-mais-de-32-bytes-ok",
        });
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        services.Should().Contain(d => d.ServiceType == typeof(IArmazenamentoArquivos));
        services.Should().Contain(d => d.ServiceType == typeof(IAssinadorUrlFoto));
    }
}
