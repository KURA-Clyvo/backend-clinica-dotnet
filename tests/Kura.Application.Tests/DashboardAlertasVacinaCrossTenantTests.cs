namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// BE-01 (ciclo Branding) -- GET /api/v1/dashboard/alertas devolvia alerta de VACINA de TODAS as
/// clinicas: Vacina nao declara IdClinica nem tem HasQueryFilter, e o predicado de
/// DashboardService.GetAlertasAsync filtrava so por data (Repository.FindAsync = Where(predicate),
/// o filtro de EventoClinico nao participa de consulta que nao navega ate ele).
///
/// KuraDbContext REAL (InMemory) com IdClinicaFiltro = null de proposito: o filtro ambiente fica
/// DESLIGADO, entao so o escopo EXPLICITO do service pode isolar as clinicas (precedente: FD-17).
/// </summary>
public class DashboardAlertasVacinaCrossTenantTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    private static KuraDbContext CreateContext(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static DashboardService BuildSut(KuraDbContext ctx, long idClinica)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinica).Returns(idClinica);

        return new DashboardService(
            new Mock<IEventoClinicoRepository>().Object,
            new Mock<IRepository<AlertaTemperatura>>().Object,
            new Mock<IRepository<Pet>>().Object,
            new Repository<Vacina>(ctx),
            new Mock<IAgendamentoRepository>().Object,
            clinicaContext.Object,
            new RelogioClinicaFixo(new DateTime(2026, 10, 9, 12, 0, 0)));
    }

    private static async Task<string> SeedVacinaVencendoNaClinicaAAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seed = CreateContext(dbName);
        seed.EventosClinicos.Add(new EventoClinico
        {
            Id = 10, IdClinica = ClinicaA, IdPet = 1, IdVeterinario = 1, IdTipoEvento = 1,
            DtEvento = DateTime.UtcNow, DsObservacao = "x", StAtiva = true
        });
        seed.Vacinas.Add(new Vacina
        {
            Id = 100, IdEventoClinico = 10, NmVacina = "Raiva-ClinicaA", NrLote = "L1", DsFabricante = "F",
            DtProximaDose = DateTime.UtcNow.Date.AddDays(5), StAtiva = true
        });
        await seed.SaveChangesAsync();
        return dbName;
    }

    [Fact]
    public async Task GetAlertasAsync_ClinicaB_NaoVeVacinaVencendoDaClinicaA()
    {
        var dbName = await SeedVacinaVencendoNaClinicaAAsync();
        await using var ctx = CreateContext(dbName);

        var alertas = (await BuildSut(ctx, ClinicaB).GetAlertasAsync()).ToList();

        alertas.Should().BeEmpty("a vacina pertence a um evento da clinica A");
    }

    [Fact]
    public async Task GetAlertasAsync_ClinicaA_VeSuaPropriaVacinaVencendo()
    {
        // Controle positivo: sem ele, o teste acima passaria tambem com fixture quebrada.
        var dbName = await SeedVacinaVencendoNaClinicaAAsync();
        await using var ctx = CreateContext(dbName);

        var alertas = (await BuildSut(ctx, ClinicaA).GetAlertasAsync()).ToList();

        alertas.Should().ContainSingle();
        alertas[0].ToString().Should().Contain("Raiva-ClinicaA");
    }
}
