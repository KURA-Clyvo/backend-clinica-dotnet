namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Agenda;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// TASK-21 — Testes de regressão para o vazamento cross-tenant em Tutor
/// (GET /api/v1/tutores retornava CPF/e-mail/telefone de tutores de outras clínicas).
///
/// Diferente dos testes de TutorServiceTests.cs (que mockam ITutorRepository), estes testes
/// usam um KuraDbContext real (InMemory) para provar que o isolamento de tenant funciona de
/// ponta a ponta — tanto pelo HasQueryFilter consolidado em KuraDbContext quanto pela defesa
/// em profundidade adicionada em TutorService (idClinica explícito no repositório).
///
/// Inclui também um teste de regressão equivalente para Agendamento, provando que o padrão
/// já usado em AgendaService (passar _clinicaContext.IdClinica manualmente ao repositório,
/// sem depender de HasQueryFilter — Agendamento não tem um) continua funcionando.
/// </summary>
public class CrossTenantRegressionTests
{
    private const long ClinicaA = 1L;
    private const long ClinicaB = 2L;

    private static KuraDbContext CreateContext(string dbName, long? idClinicaFiltro)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(c => c.IdClinicaFiltro).Returns(idClinicaFiltro);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static TutorService BuildTutorService(KuraDbContext ctx, long idClinica)
    {
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(idClinica);

        return new TutorService(
            new TutorRepository(ctx, NullLogger<TutorRepository>.Instance),
            new TutorPetRepository(ctx),
            new Mock<IRepository<Especie>>().Object,
            new Mock<IRepository<Raca>>().Object,
            new Mock<IInviteTutorRepository>().Object,
            new UnitOfWork(ctx),
            clinicaContextMock.Object,
            new Mock<IGeradorUrlFotoPet>().Object);
    }

    // ---------- Tutor: isolamento cross-tenant ----------

    [Fact]
    public async Task SearchAsync_ClinicaA_NaoRetornaTutoresDaClinicaB()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        // Seed: usa um contexto sem filtro de clínica (simula bypass administrativo de seed).
        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.AddRange(
                new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true },
                new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Act
        var resultado = await sut.SearchAsync(busca: null);

        // Assert
        resultado.Should().ContainSingle();
        resultado.Should().OnlyContain(t => t.NmTutor == "Maria Silva");
        resultado.Should().NotContain(t => t.NmTutor == "João Souza");
    }

    [Fact]
    public async Task SearchAsync_ComBusca_ClinicaA_NaoRetornaTutorDaClinicaBMesmoQuandoTextoBate()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            // Mesmo nome nas duas clínicas — só a diferença de ID_CLINICA deve decidir a visibilidade.
            seedCtx.Tutores.AddRange(
                new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Carlos Pereira", NrCpf = "33333333333", DsEmail = "carlos@a.com", NrTelefone = "11900000003", StAtiva = true },
                new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "Carlos Pereira", NrCpf = "44444444444", DsEmail = "carlos@b.com", NrTelefone = "11900000004", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Act
        var resultado = await sut.SearchAsync(busca: "Carlos");

        // Assert
        resultado.Should().ContainSingle();
        resultado.Single().NrCpf.Should().Be("33333333333");
    }

    [Fact]
    public async Task GetByIdAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Act
        var act = async () => await sut.GetByIdAsync(2L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task GetByIdAsync_TutorDaMesmaClinica_RetornaNormalmente()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Act
        var resultado = await sut.GetByIdAsync(1L);

        // Assert
        resultado.NmTutor.Should().Be("Maria Silva");
    }

    [Fact]
    public async Task GetPetsAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "João Souza", NrCpf = "22222222222", DsEmail = "joao@b.com", NrTelefone = "11900000002", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildTutorService(ctxClinicaA, ClinicaA);

        // Act
        // Antes da TASK-21, isto vazava a lista de pets (e, por consequência, o vínculo com o
        // tutor da clínica B) para qualquer clínica autenticada.
        var act = async () => await sut.GetPetsAsync(2L);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- Tutor: soft delete continua funcionando após consolidação do HasQueryFilter ----------

    [Fact]
    public async Task SoftDelete_TutorInativado_SomeDoSearchAsyncEDoGetByIdAsync()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Tutores.Add(new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Maria Silva", NrCpf = "11111111111", DsEmail = "maria@a.com", NrTelefone = "11900000001", StAtiva = true });
            await seedCtx.SaveChangesAsync();
        }

        // Confirma que o tutor aparece normalmente antes do soft delete.
        await using (var ctxAntes = CreateContext(dbName, idClinicaFiltro: ClinicaA))
        {
            var sutAntes = BuildTutorService(ctxAntes, ClinicaA);
            (await sutAntes.SearchAsync(null)).Should().ContainSingle();
        }

        // Soft delete via repositório real (mesmo caminho usado por TutorService.SoftDeleteAsync).
        await using (var ctxDelete = CreateContext(dbName, idClinicaFiltro: null))
        {
            var repo = new TutorRepository(ctxDelete, NullLogger<TutorRepository>.Instance);
            var tutor = await repo.GetByIdAsync(1L, ClinicaA);
            tutor.Should().NotBeNull();
            repo.SoftDelete(tutor!);
            await ctxDelete.SaveChangesAsync();
        }

        // Após o soft delete, o tutor não deve mais aparecer em nenhuma leitura da clínica A.
        await using var ctxDepois = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sutDepois = BuildTutorService(ctxDepois, ClinicaA);

        (await sutDepois.SearchAsync(null)).Should().BeEmpty();

        var act = async () => await sutDepois.GetByIdAsync(1L);
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    // ---------- Agendamento: regressão — AgendaService já protege corretamente (sem HasQueryFilter) ----------

    [Fact]
    public async Task AgendaService_AtualizarStatus_AgendamentoDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 10,
                IdClinica = ClinicaB,
                DtAgendamento = DateTime.UtcNow,
                StStatus = "CONFIRMADO",
                NrVersion = 1,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null); // Agendamento não tem HasQueryFilter — prova que a proteção é 100% manual.
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(ClinicaA);

        var sut = new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctxClinicaA),
            new UnitOfWork(ctxClinicaA));

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };
        // Act
        var act = async () => await sut.AtualizarStatusAsync(10L, dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AgendaService_AtualizarStatus_AgendamentoDaMesmaClinica_AtualizaNormalmente()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 20,
                IdClinica = ClinicaA,
                DtAgendamento = DateTime.UtcNow,
                StStatus = "CONFIRMADO",
                NrVersion = 1,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(ClinicaA);

        var sut = new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctxClinicaA),
            new UnitOfWork(ctxClinicaA));

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };
        // Act
        var result = await sut.AtualizarStatusAsync(20L, dto);

        // Assert
        result.DsStatus.Should().Be("REALIZADO");
    }

    // ---------- TASK-63: TimelineRepository — isolamento cross-tenant via EventoClinico ----------
    //
    // GetByPetIdAsync não recebe idClinica explícito (ao contrário de AgendaService/TutorService,
    // que compensam manualmente) — o isolamento depende inteiramente do HasQueryFilter de
    // EventoClinico em KuraDbContext.ApplyTenantFilters. Antes da TASK-63, a query original
    // (FromSqlRaw contra VW_TIMELINE_PET) não tinha filtro de tenant nenhum: qualquer JWT
    // autenticado veria a timeline de qualquer pet, de qualquer clínica. Prova aqui de que a
    // reescrita via LINQ sobre EventoClinico herda a proteção automaticamente.

    [Fact]
    public async Task TimelineRepository_GetByPetIdAsync_EventoDeOutraClinica_NaoAparece()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        // Mesmo ID_PET (1) reaproveitado por duas clínicas distintas de propósito — prova que o
        // isolamento é por ID_CLINICA do evento clínico, não por coincidência de não existir o pet.
        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Veterinarios.Add(new Veterinario { Id = 1, IdClinica = ClinicaB, NmVeterinario = "Dr. Outro", NrCrmv = "CRMV-B", DsEmail = "outro@b.com" });
            seedCtx.Pets.Add(new Pet { Id = 1, IdClinica = ClinicaB, IdEspecie = 1, IdRaca = 1, NmPet = "Pet Clinica B", DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'G' });
            seedCtx.TiposEvento.Add(new TipoEvento { Id = 1, CdTipo = "CONSULTA", NmTipo = "Consulta" });
            seedCtx.EventosClinicos.Add(new EventoClinico
            {
                Id = 1,
                IdClinica = ClinicaB,
                IdPet = 1,
                IdVeterinario = 1,
                IdTipoEvento = 1,
                DtEvento = DateTime.UtcNow,
                DsObservacao = "Evento sigiloso da Clínica B",
            });
            await seedCtx.SaveChangesAsync();
        }

        // JWT de um veterinário da Clínica A tentando ler a timeline do ID_PET=1 (que na
        // Clínica B existe e tem evento) — antes da TASK-63 isto vazava o evento inteiro,
        // incluindo DsObservacao (dado clínico sensível).
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = new TimelineRepository(ctxClinicaA);

        // Act
        var resultado = await sut.GetByPetIdAsync(1L);

        // Assert
        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task TimelineRepository_GetByPetIdAsync_EventoDaMesmaClinica_RetornaNormalmente()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Veterinarios.Add(new Veterinario { Id = 1, IdClinica = ClinicaA, NmVeterinario = "Dra. Ana", NrCrmv = "CRMV-A", DsEmail = "ana@a.com" });
            seedCtx.Pets.Add(new Pet { Id = 1, IdClinica = ClinicaA, IdEspecie = 1, IdRaca = 1, NmPet = "Pet Clinica A", DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'G' });
            seedCtx.TiposEvento.Add(new TipoEvento { Id = 1, CdTipo = "CONSULTA", NmTipo = "Consulta" });
            seedCtx.EventosClinicos.Add(new EventoClinico
            {
                Id = 1,
                IdClinica = ClinicaA,
                IdPet = 1,
                IdVeterinario = 1,
                IdTipoEvento = 1,
                DtEvento = DateTime.UtcNow,
                DsObservacao = "Evento normal da Clínica A",
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = new TimelineRepository(ctxClinicaA);

        // Act
        var resultado = (await sut.GetByPetIdAsync(1L)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        resultado.Single().DsObservacao.Should().Be("Evento normal da Clínica A");
    }
}
