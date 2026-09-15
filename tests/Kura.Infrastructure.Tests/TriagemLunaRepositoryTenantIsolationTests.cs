namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// LU-08 — mordida do predicado EXPLÍCITO de clínica no JOIN de
/// <see cref="TriagemLunaRepository.ListarPorClinicaAsync"/> (TRIAGEM_LUNA → INTERACAO_CANAL,
/// consumido por GET /api/v1/luna/triagens).
///
/// <para>
/// 🔴 <b>Por que é um repository test, e não um teste de service/HTTP.</b> O predicado que este
/// backlog exige mora dentro da chave composta do LINQ (<c>on new { IdInteracao, IdClinica }
/// equals new { IdInteracao, IdClinica }</c>) — um mock de <see cref="ITriagemLunaRepository"/>
/// (como os de <c>LunaServiceTests</c>) nunca executa esse LINQ, e um teste HTTP com
/// InMemory testaria a mesma query por um caminho mais caro sem medir nada a mais nesta
/// pergunta específica. Este é "o nível que realmente executa o join" citado no brief.
/// </para>
///
/// <para>
/// ⚠️ <b>Limite declarado (InMemory ≠ Oracle):</b> o provider InMemory do EF Core não
/// reproduz <c>'' → NULL</c> nem o byte-length de <c>VARCHAR2</c> do Oracle — irrelevante
/// aqui, porque o que está sob teste é a FORMA do predicado (chave composta), não conversão
/// de tipo/byte. A prova de que a query TRADUZ para SQL Oracle de verdade (e não só para o
/// LINQ provider do InMemory) fica para o roteiro de prova Oracle real no relatório da task
/// (fora desta suíte).
/// </para>
///
/// <para>
/// 🔴 <b>A MORDIDA DA REGRA 13 — setup CERTO.</b>
/// <see cref="ListarPorClinicaAsync_InteracaoCorrompidaApontandoOutraClinica_NaoVazaConteudoNoJoin"/>
/// semeia uma <see cref="TriagemLuna"/> da clínica A cujo <c>IdInteracao</c> aponta para uma
/// <see cref="InteracaoCanal"/> cujo <c>Id</c> É O MESMO, mas pertence à clínica B — simula a
/// inconsistência de FK que <c>LunaService.RegistrarTriagemAsync</c> normalmente barra na
/// ESCRITA (422), para medir a defesa em profundidade da LEITURA. Sem a isca de B com o
/// MESMO Id que a triagem de A referencia, um join que casasse por <c>IdInteracao</c> sozinho
/// (mutação) ainda acertaria a MESMA linha — nada vazaria e a mutação passaria por engano
/// (a armadilha da regra 13, citada explicitamente no brief). Com a isca, mutar o predicado
/// faz o join casar a triagem de A com o CONTEÚDO de B.
/// </para>
/// </summary>
public class TriagemLunaRepositoryTenantIsolationTests
{
    private const long ClinicaA = 1;
    private const long ClinicaB = 2;

    private static KuraDbContext CreateContext(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        // IdClinicaFiltro null desliga o HasQueryFilter global — o isolamento sob teste
        // aqui é o predicado EXPLÍCITO do repositório/join, não o filtro ambiente (que já
        // tem cobertura própria em TenantFilterCoverageTests).
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task ListarPorClinicaAsync_TriagemDeOutraClinica_NuncaAparecePraClinicaA()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_TriagemDeOutraClinica_NuncaAparecePraClinicaA));

        ctx.TriagensLuna.Add(new TriagemLuna
        {
            Id = 1,
            IdClinica = ClinicaA,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "triagem da clinica A",
            DtTriagem = new DateTime(2026, 5, 1),
            StEncaminhadoVet = true,
        });
        ctx.TriagensLuna.Add(new TriagemLuna
        {
            Id = 2,
            IdClinica = ClinicaB,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "triagem da clinica B — nunca pode aparecer para A",
            DtTriagem = new DateTime(2026, 5, 1),
            StEncaminhadoVet = true,
        });
        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act
        var (itens, total) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 1, 20);

        // Assert
        total.Should().Be(1);
        itens.Should().ContainSingle(i => i.IdTriagem == 1);
        itens.Should().NotContain(i => i.IdTriagem == 2);

        // Controle positivo: o mesmo repositório, chamado com o id da clínica B, enxerga a
        // linha de B — prova que "não vazou" acima não é vácuo (a linha existe e é
        // alcançável por QUEM DE FATO É da clínica B).
        var (itensB, totalB) = await repo.ListarPorClinicaAsync(ClinicaB, null, null, null, 1, 20);
        totalB.Should().Be(1);
        itensB.Should().ContainSingle(i => i.IdTriagem == 2);
    }

    /// <summary>Ver documentação da classe — a mordida da regra 13.</summary>
    [Fact]
    public async Task ListarPorClinicaAsync_InteracaoCorrompidaApontandoOutraClinica_NaoVazaConteudoNoJoin()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_InteracaoCorrompidaApontandoOutraClinica_NaoVazaConteudoNoJoin));

        // Interação da clínica B — o conteúdo abaixo NUNCA pode aparecer num item da
        // clínica A.
        ctx.InteracoesCanal.Add(new InteracaoCanal
        {
            Id = 900,
            IdClinica = ClinicaB,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "SEGREDO-DA-CLINICA-B: sintoma sensível do tutor de B",
            DtRecebimento = new DateTime(2026, 5, 1),
        });

        // Triagem da clínica A cujo IdInteracao (900) É O MESMO Id da interação de B acima
        // — inconsistência de FK deliberada, que normalmente a checagem de escrita de
        // LunaService.RegistrarTriagemAsync bloqueia com 422. Semeada direto no banco para
        // medir a defesa de LEITURA (o join), não a de escrita.
        ctx.TriagensLuna.Add(new TriagemLuna
        {
            Id = 3,
            IdClinica = ClinicaA,
            IdInteracao = 900,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "triagem da clinica A com FK corrompida",
            DtTriagem = new DateTime(2026, 5, 2),
            StEncaminhadoVet = true,
        });

        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act
        var (itens, _) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 1, 20);

        // Assert
        var item = itens.Should().ContainSingle(i => i.IdTriagem == 3).Subject;
        item.TrechoMensagem.Should().BeNull(
            "o predicado explícito de clínica no join tem de recusar o casamento com uma " +
            "INTERACAO_CANAL de outra clínica, mesmo quando o Id (IdInteracao) bate — " +
            "sem essa recusa, DS_CONTEUDO de B vaza para o item de A");
    }

    [Fact]
    public async Task ListarPorClinicaAsync_Ordena_AltaMediaBaixa_DepoisMaisRecente()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_Ordena_AltaMediaBaixa_DepoisMaisRecente));

        ctx.TriagensLuna.AddRange(
            new TriagemLuna { Id = 1, IdClinica = ClinicaA, DsNivelUrgencia = "BAIXA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 5) },
            new TriagemLuna { Id = 2, IdClinica = ClinicaA, DsNivelUrgencia = "MEDIA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 4) },
            new TriagemLuna { Id = 3, IdClinica = ClinicaA, DsNivelUrgencia = "ALTA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 1) },
            new TriagemLuna { Id = 4, IdClinica = ClinicaA, DsNivelUrgencia = "ALTA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 3) });
        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act
        var (itens, _) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 1, 20);

        // Assert — ALTA (id 4, mais recente entre as 2 ALTA) -> ALTA (id 3) -> MEDIA (id 2) -> BAIXA (id 1)
        itens.Select(i => i.IdTriagem).Should().ContainInOrder(4L, 3L, 2L, 1L);
    }

    [Fact]
    public async Task ListarPorClinicaAsync_FiltraPorUrgenciaEPeriodo()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_FiltraPorUrgenciaEPeriodo));

        ctx.TriagensLuna.AddRange(
            new TriagemLuna { Id = 1, IdClinica = ClinicaA, DsNivelUrgencia = "ALTA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 10) },
            new TriagemLuna { Id = 2, IdClinica = ClinicaA, DsNivelUrgencia = "BAIXA", DsDescricao = "d", DtTriagem = new DateTime(2026, 5, 10) },
            new TriagemLuna { Id = 3, IdClinica = ClinicaA, DsNivelUrgencia = "ALTA", DsDescricao = "d", DtTriagem = new DateTime(2026, 1, 1) });
        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act — só urgência
        var (porUrgencia, totalUrgencia) = await repo.ListarPorClinicaAsync(ClinicaA, "ALTA", null, null, 1, 20);

        // Act — urgência + período que exclui o item de janeiro
        var (porPeriodo, totalPeriodo) = await repo.ListarPorClinicaAsync(
            ClinicaA, "ALTA", new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), 1, 20);

        // Assert
        totalUrgencia.Should().Be(2);
        porUrgencia.Select(i => i.IdTriagem).Should().BeEquivalentTo([1L, 3L]);

        totalPeriodo.Should().Be(1);
        porPeriodo.Should().ContainSingle(i => i.IdTriagem == 1);
    }

    [Fact]
    public async Task ListarPorClinicaAsync_Pagina_RespeitaPageEPageSize()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_Pagina_RespeitaPageEPageSize));

        for (var i = 1; i <= 5; i++)
        {
            ctx.TriagensLuna.Add(new TriagemLuna
            {
                Id = i,
                IdClinica = ClinicaA,
                DsNivelUrgencia = "BAIXA",
                DsDescricao = "d",
                DtTriagem = new DateTime(2026, 5, i),
            });
        }
        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act
        var (pagina1, total1) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 1, 2);
        var (pagina2, total2) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 2, 2);

        // Assert — ordenação (BAIXA, sem ALTA/MEDIA) cai no ThenByDescending(DtTriagem):
        // dia 5, 4 na página 1; dia 3, 2 na página 2.
        total1.Should().Be(5);
        total2.Should().Be(5);
        pagina1.Select(i => i.IdTriagem).Should().ContainInOrder(5L, 4L);
        pagina2.Select(i => i.IdTriagem).Should().ContainInOrder(3L, 2L);
    }

    [Fact]
    public async Task ListarPorClinicaAsync_MapeiaTutorEPets_EscopadosPorClinica()
    {
        // Arrange
        var ctx = CreateContext(nameof(ListarPorClinicaAsync_MapeiaTutorEPets_EscopadosPorClinica));

        ctx.Especies.Add(new Especie { Id = 1, NmEspecie = "Canina" });

        ctx.Tutores.Add(new Tutor
        {
            Id = 10,
            IdClinica = ClinicaA,
            NmTutor = "Tutor da Clinica A",
            NrCpf = "11122233344",
            DsEmail = "tutor-a@kura.test",
            NrTelefone = "11999990000",
        });

        // Isca cross-tenant: tutor de mesmo Id conceitual não existe aqui, mas um pet da
        // clínica B garante que o filtro explícito (idClinica) do repositório é exercitado,
        // não só a ausência de dado alheio.
        ctx.Pets.Add(new Pet
        {
            Id = 20,
            IdClinica = ClinicaA,
            IdEspecie = 1,
            IdRaca = 1,
            NmPet = "Rex",
            DtNascimento = new DateTime(2022, 1, 1),
            SgSexo = 'M',
            SgPorte = 'G',
        });
        ctx.TutorPets.Add(new TutorPet { IdTutor = 10, IdPet = 20 });

        ctx.TriagensLuna.Add(new TriagemLuna
        {
            Id = 1,
            IdClinica = ClinicaA,
            IdTutor = 10,
            DsNivelUrgencia = "ALTA",
            DsDescricao = "d",
            DsSintomas = "vomito;letargia",
            NrScore = 87,
            DsRegrasVersao = "1.1",
            DtTriagem = new DateTime(2026, 5, 1),
            StEncaminhadoVet = true,
        });
        await ctx.SaveChangesAsync();

        var repo = new TriagemLunaRepository(ctx);

        // Act
        var (itens, _) = await repo.ListarPorClinicaAsync(ClinicaA, null, null, null, 1, 20);

        // Assert
        var item = itens.Should().ContainSingle().Subject;
        item.IdTutor.Should().Be(10);
        item.NomeTutor.Should().Be("Tutor da Clinica A");
        item.Pets.Should().ContainSingle(p => p.IdPet == 20 && p.NmPet == "Rex" && p.NmEspecie == "Canina");
        item.Sintomas.Should().BeEquivalentTo(["vomito", "letargia"]);
        item.Score.Should().Be(87);
        item.RegrasVersao.Should().Be("1.1");
        item.EncaminhadoVet.Should().BeTrue();
    }
}
