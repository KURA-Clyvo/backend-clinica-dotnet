namespace Kura.IntegrationTests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>Fixture própria (banco isolado) da fix wave pós-G2 da S4-NET-04.</summary>
public sealed class ListasV2FixWaveFixture : IDisposable
{
    public WebApplicationFactory<Program> Factory { get; } = new KuraApiFactory();
    public long PetAtivo, PetInativo, TutorAtivo, TutorInativo;

    public ListasV2FixWaveFixture()
    {
        using var escopo = Factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        Pet P(string n, bool ativo) => new()
        {
            IdClinica = KuraApiFactory.IdClinicaSemeada, IdEspecie = 1, IdRaca = 1, NmPet = n,
            DtNascimento = new DateTime(2020, 1, 1), SgSexo = 'M', SgPorte = 'G', StAtiva = ativo,
        };
        Tutor T(string n, string cpf, bool ativo) => new()
        {
            IdClinica = KuraApiFactory.IdClinicaSemeada, NmTutor = n, NrCpf = cpf,
            DsEmail = "t@fw.test", NrTelefone = "11988887777", StAtiva = ativo,
        };
        var pa = P("FW Pet Ativo", true); var pi = P("FW Pet Inativo", false);
        var ta = T("FW Tutor Ativo", "12312312312", true); var ti = T("FW Tutor Inativo", "32132132132", false);
        db.Pets.AddRange(pa, pi); db.Tutores.AddRange(ta, ti);
        db.SaveChanges();
        (PetAtivo, PetInativo, TutorAtivo, TutorInativo) = (pa.Id, pi.Id, ta.Id, ti.Id);
    }

    public async Task<HttpClient> ClienteAsync()
    {
        var c = Factory.CreateClient();
        c.UsarToken(await AutenticacaoHelper.ObterTokenAsync(c, KuraApiFactory.EmailClinica));
        return c;
    }

    /// <summary>JWT válido (assinatura/emissor/audiência certos) SEM a claim <c>clinicaId</c>.</summary>
    public HttpClient ClienteSemClinica()
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KuraApiFactory.ChaveJwt));
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            KuraApiFactory.EmissorJwt, KuraApiFactory.AudienciaJwt, [new Claim("sub", "x")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1),
            new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)));
        var c = Factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return c;
    }

    public void Dispose() => Factory.Dispose();
}

/// <summary>S4-NET-04, fix wave pós-G2: I-1 (datas extremas), m-1 (detalhe escopado), m-3 (links) e inativos.</summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class ListasV2FixWaveTests : IClassFixture<ListasV2FixWaveFixture>
{
    private readonly ListasV2FixWaveFixture _fx;
    public ListasV2FixWaveTests(ListasV2FixWaveFixture fx) => _fx = fx;

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    // ---- G2-I-1: 9999-12-01..9999-12-31 (30 dias) estourava DateTime.MaxValue ⇒ 500
    [Theory]
    [InlineData("/api/v2/agendamentos?dataInicio=9999-12-01&dataFim=9999-12-31")]
    [InlineData("/api/v1/agenda?dataInicio=9999-12-01&dataFim=9999-12-31")]
    [InlineData("/api/v2/agendamentos?dataInicio=0001-01-01&dataFim=0001-01-10")]
    [InlineData("/api/v1/agenda?dataInicio=1999-12-30&dataFim=2000-01-02")]
    [InlineData("/api/v2/agendamentos?dataInicio=2100-12-30&dataFim=2101-01-02")]
    public async Task Agenda_AnoForaDaJanela_Da422_NuncaEstoura500(string rota)
    {
        var vet = await _fx.ClienteAsync();

        (await vet.GetAsync(rota)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Agenda_AnoDentroDaJanela_ContinuaDando200()
    {
        var vet = await _fx.ClienteAsync();

        (await vet.GetAsync("/api/v2/agendamentos?dataInicio=2100-12-01&dataFim=2100-12-31")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await vet.GetAsync("/api/v1/agenda?dataInicio=2000-01-01&dataFim=2000-01-10")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    // ---- G2-m-1: detalhe de pet escopado à mão
    [Fact]
    public async Task PetDetalheV2_ComClinicaNoToken_Da200ComTutores()
    {
        var vet = await _fx.ClienteAsync();

        var r = await vet.GetAsync($"/api/v2/pets/{_fx.PetAtivo}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(r)).GetProperty("nmPet").GetString().Should().Be("FW Pet Ativo");
    }

    [Fact]
    public async Task PetDetalheV2_TokenSemClinica_NaoDevolveOPet()
    {
        var semClinica = _fx.ClienteSemClinica();

        var r = await semClinica.GetAsync($"/api/v2/pets/{_fx.PetAtivo}");

        // IClinicaContext.IdClinica lança sem a claim ⇒ 401 (medido na G2 nas listas); nunca 200.
        r.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    // ---- G2-m-3: adicionarTutor não é anunciado (a rota v1 responde 500)
    [Fact]
    public async Task PetV2_NaoAnunciaAdicionarTutor()
    {
        var vet = await _fx.ClienteAsync();

        var corpo = await Json(await vet.GetAsync($"/api/v2/pets/{_fx.PetAtivo}"));

        corpo.GetProperty("_links").TryGetProperty("adicionarTutor", out _).Should().BeFalse();
    }

    // ---- Observação da G2: inativos ficam fora da v2
    [Fact]
    public async Task PetInativo_ForaDaListaEDoDetalheV2()
    {
        var vet = await _fx.ClienteAsync();

        var lista = await Json(await vet.GetAsync("/api/v2/pets?nome=FW"));
        var detalhe = await vet.GetAsync($"/api/v2/pets/{_fx.PetInativo}");

        lista.GetProperty("total").GetInt64().Should().Be(1);
        lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmPet").GetString())
            .Should().Equal("FW Pet Ativo");
        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TutorInativo_ForaDaListaEDoDetalheV2()
    {
        var vet = await _fx.ClienteAsync();

        var lista = await Json(await vet.GetAsync("/api/v2/tutores?busca=FW"));
        var detalhe = await vet.GetAsync($"/api/v2/tutores/{_fx.TutorInativo}");

        lista.GetProperty("total").GetInt64().Should().Be(1);
        lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nmTutor").GetString())
            .Should().Equal("FW Tutor Ativo");
        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
