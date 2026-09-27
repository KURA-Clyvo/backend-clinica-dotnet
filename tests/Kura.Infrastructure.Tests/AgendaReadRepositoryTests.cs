namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// REC-08 (backlog <c>KURA_BACKLOG_RECEPCAO.md</c>, A-5, aceite "a") —
/// <see cref="AgendaReadRepository.GetByIntervaloAsync"/> não tinha suíte de testes de
/// infraestrutura antes desta task (<c>AgendaServiceTests</c> mocka
/// <c>IAgendamentoReadRepository</c>, então nunca exercitou a query EF real). Prova, contra
/// InMemory: (1) <c>dataInicio == dataFim == hoje</c> devolve os agendamentos DAQUELE dia (não
/// vazio — bug do G0 item 3, achado novo: intervalo fechado em meia-noite excluía qualquer
/// agendamento com hora diferente de 00:00); (2) a semana INCLUI o domingo (mesmo bug, outro
/// sintoma); (3) duas clínicas, porque este repositório é um dos que compensam manualmente a
/// ausência de <c>Agendamento</c> no <c>ApplyTenantFilters</c> (A-7).
/// </summary>
public sealed class AgendaReadRepositoryTests
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
    public async Task GetByIntervaloAsync_ComDataInicioIgualDataFim_DevolveOsAgendamentosDaqueleDia()
    {
        // Arrange -- "um dia só": dataInicio == dataFim, ambos meia-noite (é isso que o
        // controller recebe de "YYYY-MM-DD" sem hora). Antes do fix, isto devolvia VAZIO
        // mesmo com 2 agendamentos naquele dia (medido ao vivo no G0 item 3).
        var ctx = CreateContext();
        var dia = new DateTime(2026, 9, 26);
        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Manha", DtAgendamento = dia.AddHours(9) },
            new Agendamento { Id = 2, IdClinica = 1, NmPaciente = "Tarde", DtAgendamento = dia.AddHours(15) },
            new Agendamento { Id = 3, IdClinica = 1, NmPaciente = "DiaAnterior", DtAgendamento = dia.AddDays(-1).AddHours(10) },
            new Agendamento { Id = 4, IdClinica = 1, NmPaciente = "DiaSeguinte", DtAgendamento = dia.AddDays(1).AddHours(10) });
        await ctx.SaveChangesAsync();

        var repository = new AgendaReadRepository(ctx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(1, dia, dia, null)).ToList();

        // Assert
        resultado.Should().HaveCount(2);
        resultado.Select(a => a.NmPaciente).Should().BeEquivalentTo("Manha", "Tarde");
    }

    [Fact]
    public async Task GetByIntervaloAsync_ComIntervaloDeUmaSemana_IncluiODomingo()
    {
        // Arrange -- segunda (21/09) a domingo (27/09), meia-noite nos dois extremos (o que o
        // app da clínica manda hoje, useAgenda.ts). Antes do fix, o domingo NUNCA aparecia
        // (exceto exatamente às 00:00) porque `<= dataFim` cortava em meia-noite do domingo.
        var ctx = CreateContext();
        var segunda = new DateTime(2026, 9, 21);
        var domingo = new DateTime(2026, 9, 27);
        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Segunda", DtAgendamento = segunda.AddHours(9) },
            new Agendamento { Id = 2, IdClinica = 1, NmPaciente = "DomingoDeManha", DtAgendamento = domingo.AddHours(10) },
            new Agendamento { Id = 3, IdClinica = 1, NmPaciente = "SegundaSeguinte", DtAgendamento = domingo.AddDays(1).AddHours(10) }); // fora do intervalo
        await ctx.SaveChangesAsync();

        var repository = new AgendaReadRepository(ctx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(1, segunda, domingo, null)).ToList();

        // Assert
        resultado.Should().HaveCount(2);
        resultado.Select(a => a.NmPaciente).Should().BeEquivalentTo("Segunda", "DomingoDeManha");
    }

    /// <summary>A-7: escopo por clínica, teste com DUAS clínicas de propósito.</summary>
    [Fact]
    public async Task GetByIntervaloAsync_ComDuasClinicas_RetornaApenasOsDaClinicaPedida()
    {
        // Arrange
        var ctx = CreateContext();
        var dia = new DateTime(2026, 9, 26);
        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Clinica1", DtAgendamento = dia.AddHours(9) },
            new Agendamento { Id = 2, IdClinica = 2, NmPaciente = "Clinica2", DtAgendamento = dia.AddHours(9) });
        await ctx.SaveChangesAsync();

        var repository = new AgendaReadRepository(ctx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(1, dia, dia, null)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        resultado[0].NmPaciente.Should().Be("Clinica1");
    }
}
