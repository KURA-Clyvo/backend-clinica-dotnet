namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// REC-02 (KURA_BACKLOG_RECEPCAO.md): <c>CONTA_TUTOR</c> é tabela do Java, só leitura no .NET
/// (<c>ReadOnlyTablesInterceptor</c> bloqueia escrita — não testado aqui, é responsabilidade do
/// interceptor). <c>ContaTutorRepository.ExisteContaAsync</c> decide o 409 de
/// <c>POST /api/v1/tutores/{id}/convite</c>.
/// </summary>
public class ContaTutorRepositoryTests
{
    private static KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task ExisteContaAsync_TutorComConta_RetornaTrue()
    {
        // Arrange
        var ctx = CreateContext();
        ctx.Set<ContaTutor>().Add(new ContaTutor
        {
            Id = 1,
            IdTutor = 10,
            DsEmail = "maria@email.com",
            StEmailVerificado = 'S',
            DtCadastro = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var repository = new ContaTutorRepository(ctx);

        // Act
        var resultado = await repository.ExisteContaAsync(10);

        // Assert
        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteContaAsync_TutorSemConta_RetornaFalse()
    {
        // Arrange
        var ctx = CreateContext();
        var repository = new ContaTutorRepository(ctx);

        // Act
        var resultado = await repository.ExisteContaAsync(999);

        // Assert
        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task ExisteContaAsync_ContaDeOutroTutor_RetornaFalse()
    {
        // Não basta "existe ALGUMA conta" — tem que ser a conta DESTE tutor.
        var ctx = CreateContext();
        ctx.Set<ContaTutor>().Add(new ContaTutor
        {
            Id = 1,
            IdTutor = 10,
            DsEmail = "maria@email.com",
            StEmailVerificado = 'S',
            DtCadastro = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var repository = new ContaTutorRepository(ctx);

        // Act
        var resultado = await repository.ExisteContaAsync(11);

        // Assert
        resultado.Should().BeFalse();
    }
}
