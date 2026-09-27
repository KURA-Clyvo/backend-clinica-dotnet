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
    private static KuraDbContext CreateContext(string? dbName = null, long? idClinicaFiltro = null)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
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

    /// <summary>
    /// REC-09 aceite (c) — colunas V23 (DT_CHECKIN, DS_ORIGEM, DS_RESPOSTA_CONFIRMACAO, mais
    /// DT_INICIO_ATENDIMENTO/DT_LEMBRETE_CONFIRMACAO/DT_RESPOSTA_CONFIRMACAO/ID_TRIAGEM_ORIGEM)
    /// mapeadas do banco. O round-trip usa DOIS DbContext distintos sobre o MESMO nome de banco
    /// InMemory (seed → dispose → reabre → lê): se não passar por um novo context, o teste
    /// devolveria a mesma instância rastreada e passaria mesmo com a coluna nunca configurada.
    /// </summary>
    [Fact]
    public async Task GetByIntervaloAsync_MapeiaColunasV23_AposRoundTripNoBanco()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var dia = new DateTime(2026, 9, 26);

        await using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 1,
                IdClinica = 1,
                NmPaciente = "V23",
                DtAgendamento = dia.AddHours(9),
                StStatus = "AGENDADO",
                DsOrigem = "RECEPCAO",
                DtCheckin = dia.AddHours(8).AddMinutes(55),
                DtInicioAtendimento = dia.AddHours(9).AddMinutes(2),
                DtLembreteConfirmacao = dia.AddDays(-1).AddHours(18),
                DsRespostaConfirmacao = "SIM",
                DtRespostaConfirmacao = dia.AddDays(-1).AddHours(18).AddMinutes(3)
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var readCtx = CreateContext(dbName);
        var repository = new AgendaReadRepository(readCtx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(1, dia, dia, null)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        var item = resultado[0];
        item.DsOrigem.Should().Be("RECEPCAO");
        item.DtCheckin.Should().Be(dia.AddHours(8).AddMinutes(55));
        item.DtInicioAtendimento.Should().Be(dia.AddHours(9).AddMinutes(2));
        item.DtLembreteConfirmacao.Should().Be(dia.AddDays(-1).AddHours(18));
        item.DsRespostaConfirmacao.Should().Be("SIM");
        item.DtRespostaConfirmacao.Should().Be(dia.AddDays(-1).AddHours(18).AddMinutes(3));
    }

    /// <summary>
    /// REC-09 aceite (b) — agendamento da clínica A com ID_TRIAGEM_ORIGEM apontando para uma
    /// TRIAGEM_LUNA da clínica B: a navegação TriagemOrigem tem que ficar NULA (não a triagem
    /// de B), porque o HasQueryFilter de TriagemLuna (KuraDbContext.ApplyTenantFilters) continua
    /// ativo sobre o Include — ID_TRIAGEM_ORIGEM é FK opcional, então o EF gera LEFT JOIN e a
    /// linha de Agendamento não é derrubada (ao contrário do caso de FK obrigatória documentado
    /// no CLAUDE.md sobre TimelineRepository). idClinicaFiltro=A imita o JWT real do
    /// AgendaController ([Authorize]): IdClinica e IdClinicaFiltro vêm da mesma claim
    /// (ClinicaContext.cs:17,25).
    /// </summary>
    [Fact]
    public async Task GetByIntervaloAsync_TriagemOrigemDeOutraClinica_NavegacaoFicaNula()
    {
        // Arrange
        const long clinicaA = 1;
        const long clinicaB = 2;
        var dbName = Guid.NewGuid().ToString();
        var dia = new DateTime(2026, 9, 26);

        await using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.TriagensLuna.Add(new TriagemLuna
            {
                Id = 900,
                IdClinica = clinicaB,
                DsNivelUrgencia = "ALTA",
                DsDescricao = "Triagem da clinica B",
                DtTriagem = dia.AddDays(-1)
            });
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 1,
                IdClinica = clinicaA,
                NmPaciente = "AgendamentoA",
                DtAgendamento = dia.AddHours(9),
                StStatus = "AGENDADO",
                DsOrigem = "TRIAGEM_LUNA",
                IdTriagemOrigem = 900
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var readCtx = CreateContext(dbName, idClinicaFiltro: clinicaA);
        var repository = new AgendaReadRepository(readCtx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(clinicaA, dia, dia, null)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        resultado[0].IdTriagemOrigem.Should().Be(900, "a FK crua não vaza nada por si só");
        resultado[0].TriagemOrigem.Should().BeNull(
            "a triagem referenciada é da clínica B; o query filter de TriagemLuna tem que barrar a navegação");
    }

    /// <summary>
    /// Contraste do teste acima: MESMA clínica na FK e no filtro -- a navegação deve carregar
    /// normalmente. Sem este caso, o teste anterior não provaria isolamento (só provaria que o
    /// Include nunca funciona).
    /// </summary>
    [Fact]
    public async Task GetByIntervaloAsync_TriagemOrigemDaMesmaClinica_NavegacaoCarrega()
    {
        // Arrange
        const long clinicaA = 1;
        var dbName = Guid.NewGuid().ToString();
        var dia = new DateTime(2026, 9, 26);

        await using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.TriagensLuna.Add(new TriagemLuna
            {
                Id = 901,
                IdClinica = clinicaA,
                DsNivelUrgencia = "MEDIA",
                DsDescricao = "Triagem da clinica A",
                DtTriagem = dia.AddDays(-1)
            });
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 2,
                IdClinica = clinicaA,
                NmPaciente = "AgendamentoA",
                DtAgendamento = dia.AddHours(9),
                StStatus = "AGENDADO",
                DsOrigem = "TRIAGEM_LUNA",
                IdTriagemOrigem = 901
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var readCtx = CreateContext(dbName, idClinicaFiltro: clinicaA);
        var repository = new AgendaReadRepository(readCtx);

        // Act
        var resultado = (await repository.GetByIntervaloAsync(clinicaA, dia, dia, null)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        resultado[0].TriagemOrigem.Should().NotBeNull();
        resultado[0].TriagemOrigem!.DsNivelUrgencia.Should().Be("MEDIA");
    }
}
