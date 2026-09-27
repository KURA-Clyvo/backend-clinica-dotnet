namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Agenda;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// G2 REC-10 (m-4) — <c>POST /api/v1/agendamentos</c> exercitado sobre HTTP real. Antes desta
/// classe, NENHUM teste HTTP cobria o endpoint novo — a revisão mediu isso mutando a rota
/// (<c>/api/v1/agendamentos-x</c>) e o status (<c>200</c> em vez de <c>201</c>) e a suíte
/// <c>Kura.IntegrationTests</c> continuava com o mesmo total (179), 0 falhas.
///
/// <para>
/// Cobre também a mordida HTTP exigida para a I-1 (fuso em <c>DtAgendamento</c>): a G2 mediu
/// <c>"...T10:00:00Z"</c>/<c>"...T10:00:00-03:00"</c> aceitos com <c>201</c> e gravados
/// DESLOCADOS — depois do fix, os dois viram <c>400</c> e não gravam linha nenhuma.
/// </para>
///
/// <para>
/// ⚠️ <b>Host PRÓPRIO (<see cref="IClassFixture{TFixture}"/> de <see cref="KuraApiFactory"/>),
/// não a <see cref="ColecaoDeIntegracao"/></b> — mesmo raciocínio de
/// <see cref="AgendamentoStatusHttpTests"/>: esta classe ESCREVE (tutor, pet, vínculo e
/// agendamentos), e o InMemory da collection é compartilhado entre classes.
/// </para>
///
/// <para>
/// <see cref="KuraApiFactory"/> não semeia <c>Tutor</c>/<c>Pet</c>/<c>TutorPet</c> nenhum (só
/// <c>Veterinario</c>, via <c>IdVeterinarioSemeado</c>) — esta classe semeia os próprios, na
/// clínica semeada (<c>IdClinicaSemeada</c>), para ter um payload minimamente válido.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class AgendamentoCriarHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;

    private const long IdTutorSemeado = 9301;
    private const long IdPetSemeado = 9301;

    public AgendamentoCriarHttpTests(KuraApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    /// <summary>
    /// Idempotente por Id fixo — xUnit pode reordenar/paralelizar entre CLASSES (não dentro
    /// de uma), mas todo teste desta classe roda contra o MESMO host/banco (class fixture),
    /// então semear uma vez por teste com <c>AddAsync</c> + checagem "já existe" evita
    /// duplicidade sem precisar de <c>IAsyncLifetime</c>.
    /// </summary>
    private async Task GarantirTutorEPetSemeadosAsync()
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        if (await db.Tutores.IgnoreQueryFilters().AnyAsync(t => t.Id == IdTutorSemeado))
            return;

        db.Tutores.Add(new Tutor
        {
            Id = IdTutorSemeado,
            IdClinica = KuraApiFactory.IdClinicaSemeada,
            NmTutor = "Tutor HTTP REC-10",
            NrCpf = "11122233396",
            DsEmail = "tutor-rec10-http@kura.test",
            NrTelefone = "11999990001",
            StAtiva = true,
        });
        db.Pets.Add(new Pet
        {
            Id = IdPetSemeado,
            IdClinica = KuraApiFactory.IdClinicaSemeada,
            IdEspecie = 1,
            IdRaca = 1,
            NmPet = "Pet HTTP REC-10",
            DtNascimento = new DateTime(2022, 1, 1),
            SgSexo = 'M',
            SgPorte = 'M',
            StAtiva = true,
        });
        db.TutorPets.Add(new TutorPet { IdTutor = IdTutorSemeado, IdPet = IdPetSemeado });

        await db.SaveChangesAsync();
    }

    private static string DataFutura(int addMinutes = 0)
    {
        var sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var agoraSp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, sp);
        return agoraSp.AddDays(2).AddMinutes(addMinutes).ToString("yyyy-MM-ddTHH:mm:ss");
    }

    private object BodyValido(string? dtAgendamento = null, string? dsObservacoes = null) => new
    {
        idTutor = IdTutorSemeado,
        idPet = IdPetSemeado,
        idVeterinario = KuraApiFactory.IdVeterinarioSemeado,
        dtAgendamento = dtAgendamento ?? DataFutura(),
        dsTipo = "CONSULTA",
        dsObservacoes
    };

    private async Task<int> ContarAgendamentosComObservacaoAsync(string marcador)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        return await db.Agendamentos.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(a => a.DsObservacoes == marcador);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // m-4 — a mordida central: rota exata, 201, corpo com id e dsOrigem.
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 <b>A prova de mordida do m-4.</b> Mutar a rota para
    /// <c>/api/v1/agendamentos-x</c> ou o status de <c>201</c> para <c>200</c> deixava a
    /// suíte inteira verde antes desta classe existir — nenhum teste HTTP alcançava o
    /// endpoint. Este teste falha das DUAS formas: rota errada ⇒ <c>404</c> nesta chamada;
    /// status errado ⇒ a asserção de <c>201</c> abaixo falha.
    /// </summary>
    [Fact]
    public async Task Criar_ComPayloadValido_Devolve201ComIdEDsOrigemRecepcao()
    {
        // Arrange
        await GarantirTutorEPetSemeadosAsync();
        var client = await ClienteAutenticadoAsync();
        const string marcador = "REC10-HTTP-m4-happy";

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/agendamentos", BodyValido(dsObservacoes: marcador));

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);

        var corpo = await resposta.Content.ReadFromJsonAsync<AgendamentoItemDto>();
        corpo.Should().NotBeNull();
        corpo!.IdAgendamento.Should().BeGreaterThan(0);
        corpo.DsOrigem.Should().Be("RECEPCAO");
        corpo.DsStatus.Should().Be("AGENDADO");
        corpo.NrVersion.Should().Be(0);

        (await ContarAgendamentosComObservacaoAsync(marcador)).Should().Be(1);
    }

    [Fact]
    public async Task Criar_RotaInexistente_Devolve404()
    {
        // Arrange -- controle do m-4: uma rota que NÃO é a real tem que continuar 404.
        await GarantirTutorEPetSemeadosAsync();
        var client = await ClienteAutenticadoAsync();

        // Act
        var resposta = await client.PostAsJsonAsync("/api/v1/agendamentos-x", BodyValido());

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ───────────────────────────────────────────────────────────────────────────────
    // I-1 — DtAgendamento com fuso é recusado (400), sem gravar linha.
    // ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 <b>A mordida da I-1 (o cenário exato que a G2 mediu).</b> Antes do fix:
    /// <c>"...T10:00:00Z"</c> devolvia <c>201</c> e gravava <c>10:00</c> tratado como hora
    /// de SP — <b>+3h de erro</b> em relação ao instante pedido (07:00 SP) — e a própria
    /// resposta ecoava a data COM <c>"Z"</c>. Depois do fix: <c>400</c>, nenhuma linha.
    /// </summary>
    [Fact]
    public async Task Criar_ComDtAgendamentoZ_Devolve400ENenhumaLinhaGravada()
    {
        // Arrange
        await GarantirTutorEPetSemeadosAsync();
        var client = await ClienteAutenticadoAsync();
        const string marcador = "REC10-HTTP-I1-Z";
        var dtComZ = DataFutura() + "Z";

        // Act
        var resposta = await client.PostAsJsonAsync(
            "/api/v1/agendamentos", BodyValido(dtAgendamento: dtComZ, dsObservacoes: marcador));

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ContarAgendamentosComObservacaoAsync(marcador)).Should().Be(0);
    }

    /// <summary>Mesma mordida, para o segundo caso medido pela G2: offset explícito
    /// (<c>Kind=Local</c> na deserialização), não só <c>Z</c> (<c>Kind=Utc</c>).</summary>
    [Fact]
    public async Task Criar_ComDtAgendamentoOffsetExplicito_Devolve400ENenhumaLinhaGravada()
    {
        // Arrange
        await GarantirTutorEPetSemeadosAsync();
        var client = await ClienteAutenticadoAsync();
        const string marcador = "REC10-HTTP-I1-offset";
        var dtComOffset = DataFutura() + "-03:00";

        // Act
        var resposta = await client.PostAsJsonAsync(
            "/api/v1/agendamentos", BodyValido(dtAgendamento: dtComOffset, dsObservacoes: marcador));

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ContarAgendamentosComObservacaoAsync(marcador)).Should().Be(0);
    }

    /// <summary>Controle positivo do I-1: SEM fuso continua sendo aceito com 201 — a regra
    /// nova não vira "toda data é recusada".</summary>
    [Fact]
    public async Task Criar_ComDtAgendamentoSemFuso_Devolve201()
    {
        // Arrange
        await GarantirTutorEPetSemeadosAsync();
        var client = await ClienteAutenticadoAsync();
        const string marcador = "REC10-HTTP-I1-semfuso";

        // Act
        var resposta = await client.PostAsJsonAsync(
            "/api/v1/agendamentos", BodyValido(dtAgendamento: DataFutura(), dsObservacoes: marcador));

        // Assert
        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ContarAgendamentosComObservacaoAsync(marcador)).Should().Be(1);
    }
}
