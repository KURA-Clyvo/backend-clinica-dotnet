namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// REC-15, fix wave G2 (achado I-1, LGPD) — prova que <c>GetMaisRecenteAsync</c>
/// resolve empate de <c>DT_ACEITE</c> da MESMA forma que
/// <c>VW_VACINAS_VENCENDO</c> (backend-tutor-java, V21): a revogação/recusa vence o
/// empate, nunca o aceite.
/// </summary>
public class ConsentimentoRepositoryTests
{
    private KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    /// <summary>
    /// Mordida principal do achado I-1: dois registros de CONSENTIMENTO empatados
    /// EXATAMENTE no mesmo DT_ACEITE, um aceito e não revogado, outro revogado — o
    /// resultado tem que ser o REVOGADO (consentimento negado), nunca o aceito. Sem o
    /// ThenBy de desempate, qual linha o EF devolve depende só da ordem física de
    /// inserção/scan — este teste fixa a ordem de inserção com o ACEITO primeiro, que é
    /// exatamente o cenário em que "pegar a primeira" erraria sem o fix.
    /// </summary>
    [Fact]
    public async Task GetMaisRecenteAsync_EmpateDeDataComUmAceitoEUmRevogado_DevolveORevogado()
    {
        // Arrange
        var ctx = CreateContext();
        var empate = new DateTime(2026, 9, 1, 10, 0, 0);

        ctx.Consentimentos.AddRange(
            new Consentimento
            {
                Id = 1, IdTutor = 7, DsTipo = "LEMBRETES", StAceito = 'S',
                NrVersaoTermo = "v1", DtConsentimento = empate, DtRevogacao = null
            },
            new Consentimento
            {
                Id = 2, IdTutor = 7, DsTipo = "LEMBRETES", StAceito = 'S',
                NrVersaoTermo = "v1", DtConsentimento = empate, DtRevogacao = new DateTime(2026, 9, 15)
            });
        await ctx.SaveChangesAsync();

        var repository = new ConsentimentoRepository(ctx);

        // Act
        var resultado = await repository.GetMaisRecenteAsync(7, "LEMBRETES");

        // Assert
        resultado.Should().NotBeNull();
        resultado!.DtRevogacao.Should().NotBeNull("a linha revogada deve vencer o empate — LGPD-safe");
        resultado.Id.Should().Be(2);
    }

    /// <summary>Contraparte do teste acima: quando a linha de DT_ACEITE mais recente NÃO
    /// está empatada, o desempate não entra em jogo — o resultado continua sendo
    /// simplesmente o mais recente, igual ao comportamento anterior ao fix.</summary>
    [Fact]
    public async Task GetMaisRecenteAsync_SemEmpate_DevolveOMaisRecenteDeFato()
    {
        var ctx = CreateContext();

        ctx.Consentimentos.AddRange(
            new Consentimento
            {
                Id = 1, IdTutor = 7, DsTipo = "LEMBRETES", StAceito = 'S',
                NrVersaoTermo = "v1", DtConsentimento = new DateTime(2026, 1, 1), DtRevogacao = null
            },
            new Consentimento
            {
                Id = 2, IdTutor = 7, DsTipo = "LEMBRETES", StAceito = 'N',
                NrVersaoTermo = "v1", DtConsentimento = new DateTime(2026, 6, 1), DtRevogacao = null
            });
        await ctx.SaveChangesAsync();

        var repository = new ConsentimentoRepository(ctx);

        var resultado = await repository.GetMaisRecenteAsync(7, "LEMBRETES");

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(2, "é o mais recente, mesmo sendo StAceito='N' — não é o caso de empate");
    }
}
