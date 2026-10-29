namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Domain.Paginacao;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// S4-NET-04 — repositórios das listas <c>/api/v2</c> (pets, tutores, agendamentos) contra EF
/// InMemory: ordem, página, filtros, <c>Total</c> coerente com o predicado da página e, o que mais
/// importa, DUAS clínicas.
///
/// <para><b>Tradução Oracle:</b> o InMemory não prova o SQL. <c>ToLower().Contains()</c> vira
/// <c>LOWER(col) LIKE '%x%'</c> (mesmo padrão do <c>SearchAsync</c> da v1); a prova contra Oracle
/// real fica para o G4.</para>
/// </summary>
public sealed class ListasV2RepositoryTests
{
    private const long ClinicaA = 1;
    private const long ClinicaB = 2;

    private static KuraDbContext CriarContexto(string banco, long? idClinicaFiltro)
    {
        var clinica = new Mock<IClinicaContext>();
        clinica.Setup(x => x.IdClinicaFiltro).Returns(idClinicaFiltro);
        var options = new DbContextOptionsBuilder<KuraDbContext>().UseInMemoryDatabase(banco).Options;
        return new KuraDbContext(options, clinica.Object);
    }

    private static Pet NovoPet(long clinica, string nome, DateTime nasc, char porte = 'M', long especie = 1) => new()
    {
        IdClinica = clinica, IdEspecie = especie, IdRaca = 1, NmPet = nome, DtNascimento = nasc, SgSexo = 'M', SgPorte = porte,
    };

    private static Tutor NovoTutor(long clinica, string nome, string cpf) => new()
    {
        IdClinica = clinica, NmTutor = nome, NrCpf = cpf, DsEmail = nome.Replace(' ', '.') + "@x.test", NrTelefone = "11999990000",
    };

    // ---------------------------------------------------------------- pets

    [Fact]
    public async Task Pets_OrdenaPorNomeComDesempateEPagina_ETotalCobreTodoOFiltro()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, ClinicaA);
        ctx.Pets.AddRange(
            NovoPet(ClinicaA, "Bia", new DateTime(2020, 1, 1)),
            NovoPet(ClinicaA, "Ana", new DateTime(2021, 1, 1)),
            NovoPet(ClinicaA, "Ana", new DateTime(2019, 1, 1)),
            NovoPet(ClinicaA, "Caio", new DateTime(2022, 1, 1)));
        await ctx.SaveChangesAsync();
        var repo = new PetRepository(ctx);

        // Act
        var pag1 = await repo.ListarPaginadoAsync(new ConsultaPets(1, 2, OrdenacaoPet.NomeAsc), ClinicaA);
        var pag2 = await repo.ListarPaginadoAsync(new ConsultaPets(2, 2, OrdenacaoPet.NomeAsc), ClinicaA);

        // Assert
        pag1.Total.Should().Be(4);
        pag1.Itens.Select(p => p.NmPet).Should().Equal("Ana", "Ana");
        pag1.Itens[0].Id.Should().BeLessThan(pag1.Itens[1].Id, "empate de nome desempata por Id");
        pag2.Itens.Select(p => p.NmPet).Should().Equal("Bia", "Caio");
        pag1.Itens.Concat(pag2.Itens).Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(OrdenacaoPet.NomeDesc, "Caio")]
    [InlineData(OrdenacaoPet.DtNascimentoAsc, "Ana")]
    [InlineData(OrdenacaoPet.DtNascimentoDesc, "Caio")]
    public async Task Pets_RespeitaAListaBrancaDeOrdenacao_PrimeiroItem(OrdenacaoPet ordem, string primeiro)
    {
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, ClinicaA);
        ctx.Pets.AddRange(
            NovoPet(ClinicaA, "Bia", new DateTime(2020, 1, 1)),
            NovoPet(ClinicaA, "Ana", new DateTime(2019, 1, 1)),
            NovoPet(ClinicaA, "Caio", new DateTime(2022, 1, 1)));
        await ctx.SaveChangesAsync();

        var pag = await new PetRepository(ctx).ListarPaginadoAsync(new ConsultaPets(1, 10, ordem), ClinicaA);

        pag.Itens[0].NmPet.Should().Be(primeiro);
    }

    [Fact]
    public async Task Pets_FiltrosNomePorteEspecieETutor_ETotalUsaOMesmoPredicado()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, ClinicaA);
        var rex = NovoPet(ClinicaA, "Rex Grande", new DateTime(2020, 1, 1), porte: 'G');
        var rexinho = NovoPet(ClinicaA, "rexinho", new DateTime(2021, 1, 1), porte: 'P');
        var mia = NovoPet(ClinicaA, "Mia", new DateTime(2021, 1, 1), porte: 'P', especie: 2);
        var tutor = NovoTutor(ClinicaA, "Dona Rex", "11111111111");
        ctx.Pets.AddRange(rex, rexinho, mia);
        ctx.Tutores.Add(tutor);
        await ctx.SaveChangesAsync();
        ctx.TutorPets.Add(new TutorPet { IdTutor = tutor.Id, IdPet = mia.Id, DsVinculo = "PROPRIETARIO", StPrincipal = true });
        await ctx.SaveChangesAsync();
        var repo = new PetRepository(ctx);

        // Act
        var porNome = await repo.ListarPaginadoAsync(new ConsultaPets(1, 1, Nome: "REX"), ClinicaA);
        var porPorte = await repo.ListarPaginadoAsync(new ConsultaPets(1, 10, Porte: 'P'), ClinicaA);
        var porEspecie = await repo.ListarPaginadoAsync(new ConsultaPets(1, 10, EspecieId: 2), ClinicaA);
        var porTutor = await repo.ListarPaginadoAsync(new ConsultaPets(1, 10, TutorId: tutor.Id), ClinicaA);

        // Assert
        porNome.Total.Should().Be(2, "'REX' casa 'Rex Grande' e 'rexinho' sem distinguir caixa; o total ignora o tamanho da página");
        porNome.Itens.Should().HaveCount(1);
        porPorte.Itens.Select(p => p.NmPet).Should().BeEquivalentTo("Mia", "rexinho");
        porEspecie.Itens.Select(p => p.NmPet).Should().Equal("Mia");
        porTutor.Itens.Select(p => p.NmPet).Should().Equal("Mia");
    }

    [Fact]
    public async Task Pets_DuasClinicas_ListaETotalSoContamAClinicaPedida_SemIgnoreQueryFilters()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctxA = CriarContexto(banco, ClinicaA);
        ctxA.Pets.AddRange(
            NovoPet(ClinicaA, "DaA-1", new DateTime(2020, 1, 1)),
            NovoPet(ClinicaA, "DaA-2", new DateTime(2020, 1, 1)),
            NovoPet(ClinicaB, "DaB", new DateTime(2020, 1, 1)));
        await ctxA.SaveChangesAsync();
        var repo = new PetRepository(ctxA);

        // Act
        var daA = await repo.ListarPaginadoAsync(new ConsultaPets(1, 10), ClinicaA);
        // Contexto da clínica A pedindo B: se a consulta ignorasse o query filter (ou só dependesse do
        // parâmetro), devolveria o pet de B. A interseção dos dois é vazia.
        var contextoAPedindoB = await repo.ListarPaginadoAsync(new ConsultaPets(1, 10), ClinicaB);

        // Assert
        daA.Total.Should().Be(2);
        daA.Itens.Should().OnlyContain(p => p.IdClinica == ClinicaA);
        contextoAPedindoB.Total.Should().Be(0);
        contextoAPedindoB.Itens.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- tutores

    [Fact]
    public async Task Tutores_BuscaPorNomeSemCaixaOuCpfParcial_ComPaginaETotal()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, ClinicaA);
        ctx.Tutores.AddRange(
            NovoTutor(ClinicaA, "Maria Silva", "12345678901"),
            NovoTutor(ClinicaA, "Joao SILVA", "98765432100"),
            NovoTutor(ClinicaA, "Pedro Souza", "55512345678"));
        await ctx.SaveChangesAsync();
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var porNome = await repo.ListarPaginadoAsync(new ConsultaTutores(1, 1, OrdenacaoTutor.NomeAsc, "silva"), ClinicaA);
        var porCpf = await repo.ListarPaginadoAsync(new ConsultaTutores(1, 10, OrdenacaoTutor.NomeAsc, "12345"), ClinicaA);
        var semBusca = await repo.ListarPaginadoAsync(new ConsultaTutores(1, 10, OrdenacaoTutor.NomeDesc), ClinicaA);

        // Assert
        porNome.Total.Should().Be(2);
        porNome.Itens.Single().NmTutor.Should().Be("Joao SILVA");
        porCpf.Itens.Select(t => t.NmTutor).Should().BeEquivalentTo("Maria Silva", "Pedro Souza");
        semBusca.Itens.Select(t => t.NmTutor).Should().Equal("Pedro Souza", "Maria Silva", "Joao SILVA");
    }

    [Fact]
    public async Task Tutores_DuasClinicas_ListaETotalSoContamAClinicaPedida_SemIgnoreQueryFilters()
    {
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, ClinicaA);
        ctx.Tutores.AddRange(
            NovoTutor(ClinicaA, "Tutor A", "1"),
            NovoTutor(ClinicaB, "Tutor B", "2"));
        await ctx.SaveChangesAsync();
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        var daA = await repo.ListarPaginadoAsync(new ConsultaTutores(), ClinicaA);
        var contextoAPedindoB = await repo.ListarPaginadoAsync(new ConsultaTutores(), ClinicaB);

        daA.Itens.Select(t => t.NmTutor).Should().Equal("Tutor A");
        daA.Total.Should().Be(1);
        contextoAPedindoB.Total.Should().Be(0, "o query filter do Tutor continua valendo");
    }

    // ---------------------------------------------------------------- agendamentos

    private static Agendamento NovoAgendamento(long clinica, DateTime quando, string status = "AGENDADO", long? vet = null) => new()
    {
        IdClinica = clinica, DtAgendamento = quando, StStatus = status, IdVeterinario = vet, NmPaciente = "x",
    };

    [Fact]
    public async Task Agendamentos_OrdenaPorDataComDesempate_PaginaEFiltraStatusEVeterinario()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, null);
        var dia = new DateTime(2026, 10, 26);
        ctx.Agendamentos.AddRange(
            NovoAgendamento(ClinicaA, dia.AddHours(10), vet: 7),
            NovoAgendamento(ClinicaA, dia.AddHours(9), vet: 7),
            NovoAgendamento(ClinicaA, dia.AddHours(9), status: "CANCELADO", vet: 8),
            NovoAgendamento(ClinicaA, dia.AddDays(2).AddHours(23)),   // dentro: último dia inteiro
            NovoAgendamento(ClinicaA, dia.AddDays(3)));               // fora (dia seguinte ao fim)
        await ctx.SaveChangesAsync();
        var repo = new AgendaReadRepository(ctx);
        var fim = dia.AddDays(2);

        // Act
        var asc = await repo.ListarPaginadoAsync(ClinicaA, new ConsultaAgendamentos(dia, fim, 1, 2));
        var desc = await repo.ListarPaginadoAsync(ClinicaA, new ConsultaAgendamentos(dia, fim, 1, 10, OrdenacaoAgendamento.DtAgendamentoDesc));
        var cancelados = await repo.ListarPaginadoAsync(ClinicaA, new ConsultaAgendamentos(dia, fim, Status: "CANCELADO"));
        var doVet7 = await repo.ListarPaginadoAsync(ClinicaA, new ConsultaAgendamentos(dia, fim, IdVeterinario: 7));

        // Assert
        asc.Total.Should().Be(4, "o fim é inclusivo por dia; o agendamento do dia seguinte fica de fora");
        asc.Itens.Should().HaveCount(2);
        asc.Itens[0].DtAgendamento.Should().Be(dia.AddHours(9));
        asc.Itens[0].Id.Should().BeLessThan(asc.Itens[1].Id, "empate de data desempata por Id");
        desc.Itens[0].DtAgendamento.Should().Be(dia.AddDays(2).AddHours(23));
        cancelados.Total.Should().Be(1);
        doVet7.Total.Should().Be(2);
    }

    /// <summary>
    /// O caso que já vazou neste projeto (DashboardService): <c>Agendamento</c> NÃO tem query
    /// filter, então o predicado <c>IdClinica</c> do repositório é a única barreira. Contexto sem
    /// filtro de propósito (<c>IdClinicaFiltro = null</c>) — o escopo tem de vir da consulta.
    /// </summary>
    [Fact]
    public async Task Agendamentos_DuasClinicasNoMesmoPeriodo_ListaETotalSoContamAClinicaPedida()
    {
        // Arrange
        var banco = Guid.NewGuid().ToString();
        await using var ctx = CriarContexto(banco, null);
        var dia = new DateTime(2026, 10, 26);
        ctx.Agendamentos.AddRange(
            NovoAgendamento(ClinicaA, dia.AddHours(9)),
            NovoAgendamento(ClinicaA, dia.AddHours(10)),
            NovoAgendamento(ClinicaB, dia.AddHours(9)),
            NovoAgendamento(ClinicaB, dia.AddHours(11)),
            NovoAgendamento(ClinicaB, dia.AddHours(12)));
        await ctx.SaveChangesAsync();
        var repo = new AgendaReadRepository(ctx);

        // Act
        var daB = await repo.ListarPaginadoAsync(ClinicaB, new ConsultaAgendamentos(dia, dia, 1, 10));
        var daA = await repo.ListarPaginadoAsync(ClinicaA, new ConsultaAgendamentos(dia, dia, 1, 10));

        // Assert
        daB.Itens.Should().HaveCount(3).And.OnlyContain(a => a.IdClinica == ClinicaB);
        daB.Total.Should().Be(3, "o total conta só a clínica B");
        daA.Itens.Should().HaveCount(2).And.OnlyContain(a => a.IdClinica == ClinicaA);
        daA.Total.Should().Be(2);
    }
}
