namespace Kura.IntegrationTests;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Host real com dados semeados nas DUAS clínicas para as listas <c>/api/v2</c>. Banco próprio
/// (fábrica própria), então os totais são determinísticos.
/// </summary>
public sealed class ListasV2Fixture : IDisposable
{
    public const string EmailGestorClinicaB = "gestor-b-v2@kura.test";
    public static readonly DateTime Dia = new(2026, 10, 26);

    public WebApplicationFactory<Program> Factory { get; } = new KuraApiFactory();
    public long PetRex, PetMia, PetZeus, PetDaB, TutorMaria, TutorJoao, TutorDaB;
    public long AgAgendado, AgConfirmado, AgRealizado, AgComCheckin;
    public HashSet<long> AgendamentosDaB { get; } = [];

    public ListasV2Fixture()
    {
        using var escopo = Factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        Pet NovoPet(long clinica, string nome, DateTime nasc, char porte) => new()
        {
            IdClinica = clinica, IdEspecie = 1, IdRaca = 1, NmPet = nome,
            DtNascimento = nasc, SgSexo = 'M', SgPorte = porte,
        };
        Tutor NovoTutor(long clinica, string nome, string cpf) => new()
        {
            IdClinica = clinica, NmTutor = nome, NrCpf = cpf, DsEmail = "t@v2.test", NrTelefone = "11988887777",
        };

        var rex = NovoPet(KuraApiFactory.IdClinicaSemeada, "V2 Rex", new DateTime(2020, 1, 1), 'G');
        var mia = NovoPet(KuraApiFactory.IdClinicaSemeada, "V2 Mia", new DateTime(2021, 1, 1), 'P');
        var zeus = NovoPet(KuraApiFactory.IdClinicaSemeada, "V2 Zeus", new DateTime(2022, 1, 1), 'P');
        var daB = NovoPet(KuraApiFactory.IdClinicaOutroTenant, "V2 Rex da B", new DateTime(2020, 1, 1), 'G');
        var maria = NovoTutor(KuraApiFactory.IdClinicaSemeada, "V2 Maria Silva", "11122233344");
        var joao = NovoTutor(KuraApiFactory.IdClinicaSemeada, "V2 Joao Souza", "55566677788");
        var tutorB = NovoTutor(KuraApiFactory.IdClinicaOutroTenant, "V2 Tutor da B", "99900011122");
        db.Pets.AddRange(rex, mia, zeus, daB);
        db.Tutores.AddRange(maria, joao, tutorB);
        db.SaveChanges();

        Agendamento Ag(long clinica, int hora, string status, long? idPet = null, DateTime? checkin = null) => new()
        {
            IdClinica = clinica, DtAgendamento = Dia.AddHours(hora), StStatus = status, IdPet = idPet,
            NmPaciente = "x", DtCheckin = checkin, NrDuracaoMinutos = 30,
        };
        var a1 = Ag(KuraApiFactory.IdClinicaSemeada, 9, "AGENDADO", rex.Id);
        var a2 = Ag(KuraApiFactory.IdClinicaSemeada, 10, "CONFIRMADO", mia.Id);
        var a3 = Ag(KuraApiFactory.IdClinicaSemeada, 11, "REALIZADO", zeus.Id);
        var a4 = Ag(KuraApiFactory.IdClinicaSemeada, 12, "AGENDADO", rex.Id, checkin: Dia.AddHours(11));
        var b1 = Ag(KuraApiFactory.IdClinicaOutroTenant, 9, "AGENDADO", daB.Id);
        var b2 = Ag(KuraApiFactory.IdClinicaOutroTenant, 10, "AGENDADO", daB.Id);
        db.Agendamentos.AddRange(a1, a2, a3, a4, b1, b2);
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 901, IdClinica = KuraApiFactory.IdClinicaOutroTenant, IdVeterinario = null,
            DsEmail = EmailGestorClinicaB, DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(KuraApiFactory.SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor, StAtiva = true,
        });
        db.SaveChanges();

        (PetRex, PetMia, PetZeus, PetDaB) = (rex.Id, mia.Id, zeus.Id, daB.Id);
        (TutorMaria, TutorJoao, TutorDaB) = (maria.Id, joao.Id, tutorB.Id);
        (AgAgendado, AgConfirmado, AgRealizado, AgComCheckin) = (a1.Id, a2.Id, a3.Id, a4.Id);
        AgendamentosDaB.Add(b1.Id);
        AgendamentosDaB.Add(b2.Id);
    }

    public async Task<HttpClient> ClienteAsync(string email)
    {
        var c = Factory.CreateClient();
        c.UsarToken(await AutenticacaoHelper.ObterTokenAsync(c, email));
        return c;
    }

    public void Dispose() => Factory.Dispose();
}

/// <summary>
/// S4-NET-04 — <c>/api/v2</c> de pets, tutores e agendamentos pelo HTTP real: envelope + <c>_links</c>,
/// validação (400/422), escopo de clínica (duas clínicas) e ações condicionadas ao estado.
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class ListasV2HttpTests : IClassFixture<ListasV2Fixture>
{
    private readonly ListasV2Fixture _fx;

    public ListasV2HttpTests(ListasV2Fixture fx) => _fx = fx;

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private static string Href(JsonElement links, string nome) => links.GetProperty(nome).GetProperty("href").GetString()!;

    private static string QueryAgenda(string extra = "") =>
        $"/api/v2/agendamentos?dataInicio=2026-10-26&dataFim=2026-10-28{extra}";

    // ------------------------------------------------------------------ pets

    [Fact]
    public async Task Pets_Lista_TrazEnvelopeELinks_SoDaClinicaDoToken()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync("/api/v2/pets?nome=v2&pageSize=2");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await Json(r);
        corpo.GetProperty("total").GetInt64().Should().Be(3, "o pet da clínica B não entra");
        corpo.GetProperty("totalPages").GetInt32().Should().Be(2);
        corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmPet").GetString())
            .Should().Equal("V2 Mia", "V2 Rex");
        var link = corpo.GetProperty("_links");
        Href(link, "self").Should().Contain("/api/v2/pets").And.Contain("nome=v2").And.Contain("pageSize=2");
        Href(link, "next").Should().Contain("page=2").And.Contain("nome=v2");
        link.TryGetProperty("prev", out _).Should().BeFalse();
        var item = corpo.GetProperty("items")[0].GetProperty("_links");
        Href(item, "self").Should().Be($"/api/v2/pets/{_fx.PetMia}");
        Href(item, "laudos").Should().Be($"/api/v1/pets/{_fx.PetMia}/laudos");
        item.GetProperty("editar").GetProperty("method").GetString().Should().Be("PUT");
        item.GetProperty("inativar").GetProperty("method").GetString().Should().Be("DELETE");
    }

    /// <summary>
    /// S4-NET-07 (G4-m-1) — o OpenAPI publica <c>page</c>/<c>pageSize</c>/<c>sort</c>/<c>nome</c> em camelCase
    /// (<c>[FromQuery(Name=...)]</c>); o binding de query segue sem distinção de caixa, então quem usa a
    /// grafia antiga (<c>Page</c>, <c>PageSize</c>, <c>Sort</c>, <c>Nome</c>) recebe a MESMA página.
    /// </summary>
    [Theory]
    [InlineData("page=2&pageSize=2&sort=-nome&nome=v2")]
    [InlineData("Page=2&PageSize=2&Sort=-nome&Nome=v2")]
    public async Task Pets_Query_AceitaCamelCaseEPascalCase_ComOMesmoResultado(string query)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync("/api/v2/pets?" + query);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await Json(r);
        corpo.GetProperty("page").GetInt32().Should().Be(2, "'page' foi lido, não ignorado");
        corpo.GetProperty("pageSize").GetInt32().Should().Be(2);
        corpo.GetProperty("total").GetInt64().Should().Be(3, "'nome' foi lido: o pet da clínica B e os demais não entram");
        corpo.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmPet").GetString())
            .Should().Equal("V2 Mia");
    }

    [Fact]
    public async Task Pets_Ordenacao_DescEFiltrosCombinados()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var desc = await Json(await vet.GetAsync("/api/v2/pets?nome=V2&sort=-nome"));
        var porte = await Json(await vet.GetAsync("/api/v2/pets?nome=V2&porte=p&sort=-dtNascimento"));

        desc.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmPet").GetString())
            .Should().Equal("V2 Zeus", "V2 Rex", "V2 Mia");
        porte.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmPet").GetString())
            .Should().Equal("V2 Zeus", "V2 Mia");
    }

    [Theory]
    [InlineData("/api/v2/pets?sort=nmPet; DROP TABLE PET")]
    [InlineData("/api/v2/pets?sort=porte")]
    [InlineData("/api/v2/pets?pageSize=101")]
    [InlineData("/api/v2/pets?pageSize=0")]
    [InlineData("/api/v2/pets?page=0")]
    [InlineData("/api/v2/pets?porte=X")]
    public async Task Pets_ParametroInvalido_Da400(string rota)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync(rota);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pets_Detalhe_TemLinks_EPetDeOutraClinicaDa404()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var ok = await vet.GetAsync($"/api/v2/pets/{_fx.PetRex}");
        var deOutra = await vet.GetAsync($"/api/v2/pets/{_fx.PetDaB}");

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await Json(ok);
        corpo.GetProperty("nmPet").GetString().Should().Be("V2 Rex");
        Href(corpo.GetProperty("_links"), "self").Should().Be($"/api/v2/pets/{_fx.PetRex}");
        Href(corpo.GetProperty("_links"), "vacinas").Should().Be($"/api/v1/pets/{_fx.PetRex}/proximas-vacinas");
        deOutra.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pets_V2NaoExpoeCampoAMaisQueAV1()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var v1 = await Json(await vet.GetAsync($"/api/v1/pets/{_fx.PetRex}"));
        var v2 = await Json(await vet.GetAsync($"/api/v2/pets/{_fx.PetRex}"));

        var camposV2 = v2.EnumerateObject().Select(p => p.Name).Where(n => n != "_links");
        camposV2.Should().BeEquivalentTo(v1.EnumerateObject().Select(p => p.Name));
    }

    // ------------------------------------------------------------------ tutores

    [Fact]
    public async Task Tutores_ListaBuscaOrdenaEDetalhe_SoDaClinicaDoToken()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var lista = await Json(await vet.GetAsync("/api/v2/tutores?busca=v2&sort=-nome"));
        var porCpf = await Json(await vet.GetAsync("/api/v2/tutores?busca=1122"));

        lista.GetProperty("total").GetInt64().Should().Be(2, "o tutor da clínica B não entra");
        lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmTutor").GetString())
            .Should().Equal("V2 Maria Silva", "V2 Joao Souza");
        porCpf.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64())
            .Should().Equal(_fx.TutorMaria);
        var item = lista.GetProperty("items")[0].GetProperty("_links");
        Href(item, "self").Should().Be($"/api/v2/tutores/{_fx.TutorMaria}");
        Href(item, "pets").Should().Be($"/api/v1/tutores/{_fx.TutorMaria}/pets");

        var detalhe = await vet.GetAsync($"/api/v2/tutores/{_fx.TutorMaria}");
        detalhe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await vet.GetAsync($"/api/v2/tutores/{_fx.TutorDaB}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tutores_V2NaoExpoeCampoAMaisQueAV1()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var v1 = await Json(await vet.GetAsync($"/api/v1/tutores/{_fx.TutorMaria}"));
        var v2 = await Json(await vet.GetAsync($"/api/v2/tutores/{_fx.TutorMaria}"));

        v2.EnumerateObject().Select(p => p.Name).Where(n => n != "_links")
            .Should().BeEquivalentTo(v1.EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData("/api/v2/tutores?sort=nrCpf")]
    [InlineData("/api/v2/tutores?pageSize=101")]
    public async Task Tutores_ParametroInvalido_Da400(string rota)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        (await vet.GetAsync(rota)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tutores_LinksDeNavegacaoRepetemABusca_RiscoAceitoDocumentado()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var corpo = await Json(await vet.GetAsync("/api/v2/tutores?busca=v2&pageSize=1"));

        Href(corpo.GetProperty("_links"), "next").Should().Contain("busca=v2", "sem ela a página 2 seria de outra busca");
    }

    // ------------------------------------------------------------------ agendamentos

    [Fact]
    public async Task Agendamentos_DuasClinicas_CadaTokenSoVeOsSeus_ETotalSoContaOsSeus()
    {
        var vetA = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var gestorB = await _fx.ClienteAsync(ListasV2Fixture.EmailGestorClinicaB);

        var daA = await Json(await vetA.GetAsync(QueryAgenda("&pageSize=100")));
        var daB = await Json(await gestorB.GetAsync(QueryAgenda("&pageSize=100")));

        daA.GetProperty("total").GetInt64().Should().Be(4);
        daA.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("idAgendamento").GetInt64())
            .Should().NotIntersectWith(_fx.AgendamentosDaB, "agendamento da clínica B nunca aparece para a A");
        daB.GetProperty("total").GetInt64().Should().Be(2);
        daB.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("idAgendamento").GetInt64())
            .Should().BeEquivalentTo(_fx.AgendamentosDaB);
    }

    [Fact]
    public async Task Agendamentos_EnvelopeOrdenacaoEFiltroDeStatus()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var padrao = await Json(await vet.GetAsync(QueryAgenda("&pageSize=2")));
        var desc = await Json(await vet.GetAsync(QueryAgenda("&sort=-dtAgendamento&pageSize=1")));
        var soAgendados = await Json(await vet.GetAsync(QueryAgenda("&status=agendado")));

        padrao.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("idAgendamento").GetInt64())
            .Should().Equal(_fx.AgAgendado, _fx.AgConfirmado);
        Href(padrao.GetProperty("_links"), "next").Should().Contain("dataInicio=2026-10-26").And.Contain("page=2");
        desc.GetProperty("items")[0].GetProperty("idAgendamento").GetInt64().Should().Be(_fx.AgComCheckin);
        soAgendados.GetProperty("total").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task Agendamentos_AcoesNoLinksSoQuandoOEstadoAdmite()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var corpo = await Json(await vet.GetAsync(QueryAgenda("&sort=id&pageSize=100")));
        var porId = corpo.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("idAgendamento").GetInt64(), i => i.GetProperty("_links"));

        var agendado = porId[_fx.AgAgendado];
        agendado.TryGetProperty("checkin", out _).Should().BeTrue();
        agendado.TryGetProperty("inicio-atendimento", out _).Should().BeTrue();
        agendado.TryGetProperty("status", out _).Should().BeTrue();
        Href(agendado, "checkin").Should().Be($"/api/v1/agendamentos/{_fx.AgAgendado}/checkin");
        agendado.GetProperty("status").GetProperty("method").GetString().Should().Be("PATCH");
        Href(agendado, "pet").Should().Be($"/api/v2/pets/{_fx.PetRex}");

        var comCheckin = porId[_fx.AgComCheckin];
        comCheckin.TryGetProperty("checkin", out _).Should().BeFalse("já fez check-in");
        comCheckin.TryGetProperty("inicio-atendimento", out _).Should().BeTrue();

        var realizado = porId[_fx.AgRealizado];
        realizado.TryGetProperty("checkin", out _).Should().BeFalse();
        realizado.TryGetProperty("inicio-atendimento", out _).Should().BeFalse();
        realizado.TryGetProperty("status", out _).Should().BeFalse("REALIZADO é terminal");
    }

    [Theory]
    [InlineData("/api/v2/agendamentos")]
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-26")]
    [InlineData("/api/v2/agendamentos?dataInicio=ontem&dataFim=2026-10-28")]
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-26&dataFim=2026-10-28&sort=idPet")]
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-26&dataFim=2026-10-28&status=FINGIDO")]
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-26&dataFim=2026-10-28&pageSize=101")]
    public async Task Agendamentos_ParametroInvalidoOuAusente_Da400(string rota)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        (await vet.GetAsync(rota)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-01&dataFim=2026-11-02")]   // 32 dias
    [InlineData("/api/v2/agendamentos?dataInicio=2026-10-10&dataFim=2026-10-09")]   // invertido
    public async Task Agendamentos_IntervaloForaDaRegra_Da422(string rota)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        (await vet.GetAsync(rota)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Agendamentos_Intervalo31Dias_Da200()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        (await vet.GetAsync("/api/v2/agendamentos?dataInicio=2026-10-01&dataFim=2026-11-01")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ v1 intacta

    [Theory]
    [InlineData("/api/v1/pets")]
    [InlineData("/api/v1/tutores")]
    public async Task V1_ContinuaSendoArrayPuro(string rota)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync(rota);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(r)).ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task V1_AgendaContinuaComOEnvelopeAntigo()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var corpo = await Json(await vet.GetAsync("/api/v1/agenda?dataInicio=2026-10-26&dataFim=2026-10-28"));

        corpo.TryGetProperty("agendamentos", out var lista).Should().BeTrue();
        lista.GetArrayLength().Should().Be(4);
        corpo.TryGetProperty("_links", out _).Should().BeFalse();
    }
}
