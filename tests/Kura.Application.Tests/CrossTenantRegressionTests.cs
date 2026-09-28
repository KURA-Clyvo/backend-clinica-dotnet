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
            new Mock<IContaTutorRepository>().Object,
            new UnitOfWork(ctx),
            clinicaContextMock.Object,
            new Mock<IGeradorUrlFotoPet>().Object,
            new Mock<IGeradorLinkConvite>().Object);
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
            new UnitOfWork(ctxClinicaA),
            new Mock<IGeradorUrlFotoPet>().Object,
            NullLogger<AgendaService>.Instance,
            new Mock<ITutorRepository>().Object,
            new Mock<IPetRepository>().Object,
            new Mock<IVeterinarioRepository>().Object,
            new Mock<ITriagemLunaRepository>().Object,
            new Mock<IRelogioClinica>().Object);

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
            new UnitOfWork(ctxClinicaA),
            new Mock<IGeradorUrlFotoPet>().Object,
            NullLogger<AgendaService>.Instance,
            new Mock<ITutorRepository>().Object,
            new Mock<IPetRepository>().Object,
            new Mock<IVeterinarioRepository>().Object,
            new Mock<ITriagemLunaRepository>().Object,
            new Mock<IRelogioClinica>().Object);

        var dto = new AtualizarStatusAgendamentoDto { DsStatus = "REALIZADO", NrVersion = 1 };
        // Act
        var result = await sut.AtualizarStatusAsync(20L, dto);

        // Assert
        result.DsStatus.Should().Be("REALIZADO");
    }

    // ---------- Agendamento: CheckinAsync/IniciarAtendimentoAsync (REC-11) — mesmo padrão manual ----------

    private static AgendaService BuildAgendaService(KuraDbContext ctx, long idClinica, DateTime agora)
    {
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(idClinica);

        var relogioMock = new Mock<IRelogioClinica>();
        relogioMock.Setup(r => r.Agora()).Returns(agora);
        // G2 REC-11 (m-2): a guarda de data compara com Hoje(), não com Agora() -- sem este
        // setup, o mock devolveria default(DateTime) e a guarda recusaria com 422 todo teste
        // que semeia DtAgendamento num dia real (2026-10-01).
        relogioMock.Setup(r => r.Hoje()).Returns(agora.Date);

        return new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctx),
            new UnitOfWork(ctx),
            new Mock<IGeradorUrlFotoPet>().Object,
            NullLogger<AgendaService>.Instance,
            new Mock<ITutorRepository>().Object,
            new Mock<IPetRepository>().Object,
            new Mock<IVeterinarioRepository>().Object,
            new Mock<ITriagemLunaRepository>().Object,
            relogioMock.Object);
    }

    [Fact]
    public async Task AgendaService_Checkin_AgendamentoDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 30,
                IdClinica = ClinicaB,
                DtAgendamento = new DateTime(2026, 10, 1, 9, 0, 0),
                StStatus = "AGENDADO",
                NrVersion = 0,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaService(ctxClinicaA, ClinicaA, new DateTime(2026, 10, 1, 9, 5, 0));

        var dto = new RegistrarEventoRecepcaoDto { NrVersion = 0 };
        // Act -- pede check-in de agendamento da clínica B autenticado como clínica A.
        var act = async () => await sut.CheckinAsync(30L, dto);

        // Assert -- mesma resposta do inexistente, sem oráculo (A-7).
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AgendaService_Checkin_AgendamentoDaMesmaClinica_RegistraCheckin()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var agora = new DateTime(2026, 10, 1, 9, 5, 0);

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 40,
                IdClinica = ClinicaA,
                DtAgendamento = new DateTime(2026, 10, 1, 9, 0, 0),
                StStatus = "AGENDADO",
                NrVersion = 0,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaService(ctxClinicaA, ClinicaA, agora);

        var dto = new RegistrarEventoRecepcaoDto { NrVersion = 0 };
        // Act
        var result = await sut.CheckinAsync(40L, dto);

        // Assert
        result.DtCheckin.Should().Be(agora);
        result.NrVersion.Should().Be(1);
    }

    [Fact]
    public async Task AgendaService_IniciarAtendimento_AgendamentoDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 50,
                IdClinica = ClinicaB,
                DtAgendamento = new DateTime(2026, 10, 1, 9, 0, 0),
                StStatus = "AGENDADO",
                NrVersion = 0,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaService(ctxClinicaA, ClinicaA, new DateTime(2026, 10, 1, 9, 5, 0));

        var dto = new RegistrarEventoRecepcaoDto { NrVersion = 0 };
        // Act
        var act = async () => await sut.IniciarAtendimentoAsync(50L, dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task AgendaService_IniciarAtendimento_AgendamentoDaMesmaClinica_RegistraInicioSemCheckin()
    {
        // Arrange -- walk-in: sem check-in prévio.
        var dbName = Guid.NewGuid().ToString();
        var agora = new DateTime(2026, 10, 1, 9, 5, 0);

        await using (var seedCtx = CreateContext(dbName, idClinicaFiltro: null))
        {
            seedCtx.Agendamentos.Add(new Agendamento
            {
                Id = 60,
                IdClinica = ClinicaA,
                DtAgendamento = new DateTime(2026, 10, 1, 9, 0, 0),
                StStatus = "AGENDADO",
                NrVersion = 0,
                StAtiva = true
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaService(ctxClinicaA, ClinicaA, agora);

        var dto = new RegistrarEventoRecepcaoDto { NrVersion = 0 };
        // Act
        var result = await sut.IniciarAtendimentoAsync(60L, dto);

        // Assert
        result.DtInicioAtendimento.Should().Be(agora);
        result.DtCheckin.Should().BeNull("início sem check-in é permitido e não inventa DtCheckin");
        result.NrVersion.Should().Be(1);
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

    // ---------- Agendamento: CriarAsync (REC-10) -- isolamento cross-tenant com REPOSITÓRIOS
    // e HasQueryFilter REAIS (não mocks), duas clínicas de verdade no mesmo InMemory ----------

    private static readonly DateTime DataDeTeste = new(2026, 10, 7, 9, 0, 0);

    private static AgendaService BuildAgendaServiceParaCriar(KuraDbContext ctx, long idClinica)
    {
        var clinicaContextMock = new Mock<IClinicaContext>();
        clinicaContextMock.Setup(c => c.IdClinica).Returns(idClinica);

        var relogioMock = new Mock<IRelogioClinica>();
        relogioMock.Setup(r => r.Agora()).Returns(DataDeTeste); // == DtAgendamento -- dentro da tolerância de encaixe.

        return new AgendaService(
            new Mock<IAgendamentoReadRepository>().Object,
            clinicaContextMock.Object,
            new AgendamentoRepository(ctx),
            new UnitOfWork(ctx),
            new Mock<IGeradorUrlFotoPet>().Object,
            NullLogger<AgendaService>.Instance,
            new TutorRepository(ctx, NullLogger<TutorRepository>.Instance),
            new PetRepository(ctx),
            new VeterinarioRepository(ctx),
            new TriagemLunaRepository(ctx),
            relogioMock.Object);
    }

    /// <summary>
    /// Semeia duas clínicas completas (tutor + pet vinculado + veterinário + triagem), mais
    /// um TERCEIRO tutor NA CLÍNICA A (id 3) com sua própria triagem (id 3) -- para o teste
    /// de "triagem de outro tutor da MESMA clínica" (G2/m-4), que tem resposta diferente
    /// (422) de "triagem de outra clínica" (404).
    /// </summary>
    private static async Task SeedDuasClinicasParaAgendamentoAsync(string dbName)
    {
        await using var seedCtx = CreateContext(dbName, idClinicaFiltro: null);

        seedCtx.Tutores.AddRange(
            new Tutor { Id = 1, IdClinica = ClinicaA, NmTutor = "Tutor A", NrCpf = "111", DsEmail = "a@a.com", NrTelefone = "11111" },
            new Tutor { Id = 2, IdClinica = ClinicaB, NmTutor = "Tutor B", NrCpf = "222", DsEmail = "b@b.com", NrTelefone = "22222" },
            new Tutor { Id = 3, IdClinica = ClinicaA, NmTutor = "Outro Tutor A", NrCpf = "333", DsEmail = "c@a.com", NrTelefone = "33333" });

        seedCtx.Pets.AddRange(
            new Pet { Id = 1, IdClinica = ClinicaA, IdEspecie = 1, IdRaca = 1, NmPet = "Pet A", DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'G' },
            new Pet { Id = 2, IdClinica = ClinicaB, IdEspecie = 1, IdRaca = 1, NmPet = "Pet B", DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'G' });

        seedCtx.Veterinarios.AddRange(
            new Veterinario { Id = 1, IdClinica = ClinicaA, NmVeterinario = "Dr. A", NrCrmv = "CRMV-A", DsEmail = "veta@a.com" },
            new Veterinario { Id = 2, IdClinica = ClinicaB, NmVeterinario = "Dr. B", NrCrmv = "CRMV-B", DsEmail = "vetb@b.com" });

        seedCtx.TriagensLuna.AddRange(
            new TriagemLuna { Id = 1, IdClinica = ClinicaA, IdTutor = 1, DsNivelUrgencia = "ALTA", DsDescricao = "Triagem A", DtTriagem = DataDeTeste },
            new TriagemLuna { Id = 2, IdClinica = ClinicaB, IdTutor = 2, DsNivelUrgencia = "ALTA", DsDescricao = "Triagem B (SEGREDO)", DtTriagem = DataDeTeste },
            new TriagemLuna { Id = 3, IdClinica = ClinicaA, IdTutor = 3, DsNivelUrgencia = "BAIXA", DsDescricao = "Triagem de outro tutor da mesma clinica A", DtTriagem = DataDeTeste });

        await seedCtx.SaveChangesAsync();

        // TutorPet é uma FK composta sem coluna gerada -- inserido à parte, na tabela ponte.
        seedCtx.TutorPets.AddRange(
            new TutorPet { IdTutor = 1, IdPet = 1 },
            new TutorPet { IdTutor = 2, IdPet = 2 });
        await seedCtx.SaveChangesAsync();
    }

    private static AgendamentoCreateDto DtoValidoParaClinicaA(long? idTriagemOrigem = null) => new()
    {
        IdTutor = 1,
        IdPet = 1,
        IdVeterinario = 1,
        DtAgendamento = DataDeTeste,
        DsTipo = "CONSULTA",
        IdTriagemOrigem = idTriagemOrigem
    };

    [Fact]
    public async Task CriarAsync_PetDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        var dtoComPetDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 1,
            IdPet = 2, // pet da Clínica B
            IdVeterinario = 1,
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComPetDeOutraClinica);

        // Assert -- resposta idêntica à de um pet inexistente (sem oráculo).
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        var dtoComTutorDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 2, // tutor da Clínica B
            IdPet = 1,
            IdVeterinario = 1,
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComTutorDeOutraClinica);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_VeterinarioDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange -- G0 item 9: validação que o Java NÃO faz; o REC-10 adiciona de propósito.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        var dtoComVetDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 1,
            IdPet = 1,
            IdVeterinario = 2, // veterinário da Clínica B
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComVetDeOutraClinica);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    /// <summary>
    /// Medido ao escrever esta suíte (mordida real, registrada no relatório da REC-10):
    /// mutar <c>VeterinarioRepository.GetByIdAsync</c> para remover o predicado
    /// <c>idClinica</c> do LINQ deixa o teste ACIMA (<see
    /// cref="CriarAsync_VeterinarioDeOutraClinica_LancaEntidadeNaoEncontrada"/>) VERDE
    /// mesmo assim -- porque o teste acima roda com <c>idClinicaFiltro: ClinicaA</c>, e o
    /// <c>HasQueryFilter</c> GLOBAL de <c>Veterinario</c> (KuraDbContext) sozinho já
    /// bloqueia o veterinário da Clínica B. O teste acima prova o comportamento ponta a
    /// ponta; NÃO prova que o predicado EXPLÍCITO do repositório (defesa em profundidade,
    /// A-7) faz alguma coisa. Mesma classe de achado que
    /// <c>TriagemLunaListaTenantIsolationTests</c> já documentou para o join
    /// TRIAGEM_LUNA→INTERACAO_CANAL (regra 13 do CLAUDE.md: "controle positivo" tem que
    /// exercitar o MESMO predicado da medição).
    ///
    /// <para>Este teste roda com <c>idClinicaFiltro: null</c> (filtro global DESLIGADO --
    /// simula, por exemplo, um consumidor futuro autenticado por API Key, sem JWT de
    /// clínica, como os endpoints da Luna) enquanto o <see cref="IClinicaContext.IdClinica"/>
    /// que o AgendaService usa continua fixo em <c>ClinicaA</c> -- isolando o predicado
    /// explícito do repositório como ÚNICA proteção restante.</para>
    /// </summary>
    [Fact]
    public async Task CriarAsync_SemFiltroGlobalAtivo_VeterinarioDeOutraClinica_AindaAssimBloqueadoPeloPredicadoExplicito()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        // idClinicaFiltro: null -- desliga o HasQueryFilter global inteiro (mesma forma que
        // os endpoints sem JWT de clínica o desligam em produção).
        await using var ctxSemFiltroGlobal = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaServiceParaCriar(ctxSemFiltroGlobal, ClinicaA);

        var dtoComVetDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 1,
            IdPet = 1,
            IdVeterinario = 2, // veterinário da Clínica B
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComVetDeOutraClinica);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    /// <summary>
    /// G2-REC10/m-2 — mesma classe de achado do isolador do Veterinário, medida pela G2
    /// (M7 do `g2-rec10.md`): mutar <c>TutorRepository.GetByIdAsync</c> removendo
    /// <c>&amp;&amp; t.IdClinica == idClinica</c> deixava a suíte 1027/0 VERDE, porque
    /// <c>CriarAsync_TutorDeOutraClinica_LancaEntidadeNaoEncontrada</c> roda com
    /// <c>idClinicaFiltro: ClinicaA</c> e o <c>HasQueryFilter</c> GLOBAL de <c>Tutor</c>
    /// sozinho já bloqueava. Este teste isola o predicado EXPLÍCITO (filtro global
    /// desligado, <c>idClinicaFiltro: null</c>).
    /// </summary>
    [Fact]
    public async Task CriarAsync_SemFiltroGlobalAtivo_TutorDeOutraClinica_AindaAssimBloqueadoPeloPredicadoExplicito()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxSemFiltroGlobal = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaServiceParaCriar(ctxSemFiltroGlobal, ClinicaA);

        var dtoComTutorDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 2, // tutor da Clínica B
            IdPet = 1,
            IdVeterinario = 1,
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComTutorDeOutraClinica);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    /// <summary>G2-REC10/m-2 — idem, para <c>PetRepository.GetByIdComVinculosAsync</c>.</summary>
    [Fact]
    public async Task CriarAsync_SemFiltroGlobalAtivo_PetDeOutraClinica_AindaAssimBloqueadoPeloPredicadoExplicito()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxSemFiltroGlobal = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaServiceParaCriar(ctxSemFiltroGlobal, ClinicaA);

        var dtoComPetDeOutraClinica = new AgendamentoCreateDto
        {
            IdTutor = 1,
            IdPet = 2, // pet da Clínica B
            IdVeterinario = 1,
            DtAgendamento = DataDeTeste,
            DsTipo = "CONSULTA"
        };

        // Act
        var act = async () => await sut.CriarAsync(dtoComPetDeOutraClinica);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_TriagemDeOutraClinica_LancaEntidadeNaoEncontrada()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        var dto = DtoValidoParaClinicaA(idTriagemOrigem: 2); // triagem 2 é da Clínica B

        // Act
        var act = async () => await sut.CriarAsync(dto);

        // Assert -- resposta idêntica à de uma triagem inexistente (não vaza que a triagem existe).
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    /// <summary>G2-REC10/m-2 — idem, para <c>TriagemLunaRepository.GetByIdAsync</c>. O G2
    /// apontou que este é o caso que mais importa a longo prazo: se a REC-15 (endpoints da
    /// Luna, autenticados por API Key, SEM JWT de clínica) reaproveitar este mesmo método,
    /// o filtro global de <c>TriagemLuna</c> fica inerte (mesma razão documentada em
    /// <c>KuraDbContext.ApplyTenantFilters</c> para <c>InteracaoCanal</c>) e o predicado
    /// explícito passa a ser a ÚNICA proteção.</summary>
    [Fact]
    public async Task CriarAsync_SemFiltroGlobalAtivo_TriagemDeOutraClinica_AindaAssimBloqueadoPeloPredicadoExplicito()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxSemFiltroGlobal = CreateContext(dbName, idClinicaFiltro: null);
        var sut = BuildAgendaServiceParaCriar(ctxSemFiltroGlobal, ClinicaA);

        var dto = DtoValidoParaClinicaA(idTriagemOrigem: 2); // triagem 2 é da Clínica B

        // Act
        var act = async () => await sut.CriarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task CriarAsync_TriagemDeOutroTutorDaMesmaClinica_LancaRegraDeNegocio()
    {
        // Arrange -- G2/m-4: triagem 3 é da MESMA clínica A, mas do tutor 3, não do tutor 1
        // do corpo. Resposta DIFERENTE da anterior (422, não 404) -- a existência da
        // triagem não é escondida quando ela é da própria clínica.
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        var dto = DtoValidoParaClinicaA(idTriagemOrigem: 3); // triagem do tutor 3, corpo pede tutor 1

        // Act
        var act = async () => await sut.CriarAsync(dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
    }

    [Fact]
    public async Task CriarAsync_ClinicaA_TodosOsIdsDaMesmaClinica_CriaComSucesso()
    {
        // Arrange -- sanidade: o isolamento acima não bloqueia o caminho legítimo (a mesma
        // massa de dados, todos os ids da própria clínica).
        var dbName = Guid.NewGuid().ToString();
        await SeedDuasClinicasParaAgendamentoAsync(dbName);
        await using var ctxClinicaA = CreateContext(dbName, idClinicaFiltro: ClinicaA);
        var sut = BuildAgendaServiceParaCriar(ctxClinicaA, ClinicaA);

        // Act
        var result = await sut.CriarAsync(DtoValidoParaClinicaA(idTriagemOrigem: 1));

        // Assert
        result.DsStatus.Should().Be("AGENDADO");
        result.DsOrigem.Should().Be("TRIAGEM_LUNA");
        result.DsNivelUrgenciaOrigem.Should().Be("ALTA");

        // Confere direto no banco (mesmo dbName) -- a clínica gravada é a do contexto, nunca do corpo.
        await using var ctxVerificacao = CreateContext(dbName, idClinicaFiltro: null);
        var gravado = await ctxVerificacao.Agendamentos.SingleAsync(a => a.IdTutor == 1 && a.IdPet == 1);
        gravado.IdClinica.Should().Be(ClinicaA);
        gravado.DsOrigem.Should().Be("TRIAGEM_LUNA");
        gravado.NrVersion.Should().Be(0);
    }
}
