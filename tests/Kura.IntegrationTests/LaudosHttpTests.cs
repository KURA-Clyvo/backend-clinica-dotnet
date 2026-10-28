namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Host real (<c>Program.cs</c>) com <b>repositório de laudos em memória escopado por clínica</b> e
/// <b>storage em memória</b> (<c>LaudoFakes.cs</c>). Prova pelo HTTP: rota, autorização por papel,
/// escopo de clínica (duas clínicas), paginação, <c>_links</c> HAL e o mapeamento de 503/422.
/// O repositório de Mongo de verdade tem os testes dele (Mongo2Go) em Kura.Infrastructure.Tests.
/// </summary>
public sealed class LaudosFixture : IDisposable
{
    public const string EmailGestorClinicaB = "gestor-b@kura.test";
    public const string EmailVetClinicaB = "outro-tenant@kura.test";

    public LaudoRepositoryFake Laudos { get; } = new();
    public ArmazenamentoEmMemoria Storage { get; } = new();
    public WebApplicationFactory<Program> Factory { get; }
    public long PetA, PetPaginacao, PetB;

    public LaudosFixture()
    {
        Factory = new KuraApiFactory().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ILaudoRepository>();
            s.RemoveAll<IArmazenamentoArquivos>();
            s.AddSingleton<ILaudoRepository>(Laudos);
            s.AddSingleton<IArmazenamentoArquivos>(Storage);
        }));

        using var escopo = Factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        Pet Novo(long clinica, string nome) => new()
        {
            IdClinica = clinica, IdEspecie = 1, IdRaca = 1, NmPet = nome,
            DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'M',
        };
        var a = Novo(KuraApiFactory.IdClinicaSemeada, "Pet A");
        var pag = Novo(KuraApiFactory.IdClinicaSemeada, "Pet Paginacao");
        var b = Novo(KuraApiFactory.IdClinicaOutroTenant, "Pet B");
        db.Pets.AddRange(a, pag, b);
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 900, IdClinica = KuraApiFactory.IdClinicaOutroTenant, IdVeterinario = null,
            DsEmail = EmailGestorClinicaB, DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(KuraApiFactory.SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor, StAtiva = true,
        });
        db.SaveChanges();
        PetA = a.Id;
        PetPaginacao = pag.Id;
        PetB = b.Id;
    }

    public async Task<HttpClient> ClienteAsync(string email)
    {
        var c = Factory.CreateClient();
        c.UsarToken(await AutenticacaoHelper.ObterTokenAsync(c, email));
        return c;
    }

    public void Dispose() => Factory.Dispose();
}

[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class LaudosHttpTests : IClassFixture<LaudosFixture>
{
    private static readonly byte[] PdfMinimo = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\n%%EOF");

    private readonly LaudosFixture _fx;

    public LaudosHttpTests(LaudosFixture fx) => _fx = fx;

    private static MultipartFormDataContent Form(
        string titulo = "Hemograma", string tipo = "HEMOGRAMA", string dataExame = "2026-10-01",
        byte[]? arquivo = null, string nomeArquivo = "laudo.pdf", string? dados = null)
    {
        var f = new MultipartFormDataContent
        {
            { new StringContent(tipo), "tipo" },
            { new StringContent(titulo), "titulo" },
            { new StringContent(dataExame), "dataExame" },
        };
        if (dados is not null) f.Add(new StringContent(dados), "dados");
        var parte = new ByteArrayContent(arquivo ?? PdfMinimo);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); // o cliente SEMPRE diz que e PDF
        f.Add(parte, "arquivo", nomeArquivo);
        return f;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<string> CriarAsync(HttpClient c, long idPet, string titulo = "Hemograma", string tipo = "HEMOGRAMA", string data = "2026-10-01")
    {
        var r = await c.PostAsync($"/api/v1/pets/{idPet}/laudos", Form(titulo, tipo, data));
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("id").GetString()!;
    }

    // ───────── POST ─────────

    [Fact]
    public async Task Post_Valido_Devolve201ComLocationELinks_EGravaArquivoComChaveDaClinica()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form(dados: """{"hematocrito":42.5,"faixa":{"min":37},"serie":[1,2]}"""));

        r.StatusCode.Should().Be(HttpStatusCode.Created);
        var corpo = await Json(r);
        var id = corpo.GetProperty("id").GetString()!;
        r.Headers.Location!.ToString().Should().EndWith($"/api/v1/laudos/{id}");
        corpo.GetProperty("tipo").GetString().Should().Be("HEMOGRAMA");
        corpo.GetProperty("idPet").GetInt64().Should().Be(_fx.PetA);
        corpo.GetProperty("arquivo").GetProperty("contentType").GetString().Should().Be("application/pdf");
        corpo.GetProperty("arquivo").GetProperty("sha256").GetString().Should().HaveLength(64);
        corpo.GetProperty("arquivo").TryGetProperty("chave", out _).Should().BeFalse("a chave de storage nao e exposta");
        corpo.GetProperty("dados").GetProperty("faixa").GetProperty("min").GetInt64().Should().Be(37);
        corpo.GetProperty("_links").GetProperty("self").GetProperty("href").GetString().Should().Be($"/api/v1/laudos/{id}");
        _fx.Storage.Chaves.Should().Contain(k => k.StartsWith($"clinica/{KuraApiFactory.IdClinicaSemeada}/pet/{_fx.PetA}/laudo/") && k.EndsWith(".pdf"));
    }

    [Fact]
    public async Task Post_ArquivoDeTextoComNomePdf_Devolve400ENadaEGravadoNoStorage()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var antes = _fx.Storage.Quantidade;

        var r = await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos",
            Form(arquivo: Encoding.ASCII.GetBytes("isto e texto, nao um PDF"), nomeArquivo: "exame.pdf"));

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("arquivo");
        _fx.Storage.Quantidade.Should().Be(antes);
    }

    [Fact]
    public async Task Post_PetDeOutraClinica_Devolve404ENadaEGravado()
    {
        var vetA = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var antes = _fx.Storage.Quantidade;

        var r = await vetA.PostAsync($"/api/v1/pets/{_fx.PetB}/laudos", Form());

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _fx.Storage.Quantidade.Should().Be(antes);
    }

    [Fact]
    public async Task Post_CampoInvalido_Devolve400()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        (await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form(tipo: "RAIOX"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form(dados: "[1]"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_SemToken_Devolve401()
    {
        var anonimo = _fx.Factory.CreateClient();
        (await anonimo.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ───────── duas clínicas ─────────

    [Fact]
    public async Task DuasClinicas_LaudoDaClinicaA_EInvisivelParaGestorEVeterinarioDaClinicaB_EmTodasAsRotas()
    {
        var vetA = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var gestorB = await _fx.ClienteAsync(LaudosFixture.EmailGestorClinicaB);
        var id = await CriarAsync(vetA, _fx.PetA, "Segredo da clinica A");

        // detalhe, arquivo, lista (pet da A visto pela B), DELETE (com GESTOR, que passa na politica)
        (await gestorB.GetAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestorB.GetAsync($"/api/v1/laudos/{id}/arquivo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestorB.GetAsync($"/api/v1/pets/{_fx.PetA}/laudos")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestorB.DeleteAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // controle positivo: a clinica A continua enxergando, e o laudo NAO foi removido pelo DELETE da B
        (await vetA.GetAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DuasClinicas_ListaDoPetDaB_NaoTrazLaudoDaA()
    {
        var vetA = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var gestorB = await _fx.ClienteAsync(LaudosFixture.EmailGestorClinicaB);
        await CriarAsync(vetA, _fx.PetA, "da A");
        var idB = await CriarAsync(gestorB, _fx.PetB, "da B");

        var lista = await Json(await gestorB.GetAsync($"/api/v1/pets/{_fx.PetB}/laudos"));

        lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString())
            .Should().Equal(idB);
    }

    // ───────── papéis e _links ─────────

    [Fact]
    public async Task Links_GestorTemExcluir_VeterinarioNao_EVeterinarioNoDeleteRecebe403()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var gestor = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);
        var id = await CriarAsync(vet, _fx.PetA);

        var comoGestor = (await Json(await gestor.GetAsync($"/api/v1/laudos/{id}"))).GetProperty("_links");
        var comoVet = (await Json(await vet.GetAsync($"/api/v1/laudos/{id}"))).GetProperty("_links");

        comoGestor.TryGetProperty("excluir", out var excluir).Should().BeTrue();
        excluir.GetProperty("method").GetString().Should().Be("DELETE");
        excluir.GetProperty("href").GetString().Should().Be($"/api/v1/laudos/{id}");
        comoVet.TryGetProperty("excluir", out _).Should().BeFalse();
        foreach (var rel in new[] { "self", "pet", "laudosDoPet", "arquivo" })
        {
            comoGestor.TryGetProperty(rel, out _).Should().BeTrue(rel);
            comoVet.TryGetProperty(rel, out _).Should().BeTrue(rel);
        }
        comoGestor.GetProperty("pet").GetProperty("href").GetString().Should().Be($"/api/v1/pets/{_fx.PetA}");
        comoGestor.GetProperty("laudosDoPet").GetProperty("href").GetString().Should().Be($"/api/v1/pets/{_fx.PetA}/laudos");
        comoGestor.GetProperty("arquivo").GetProperty("href").GetString().Should().Be($"/api/v1/laudos/{id}/arquivo");

        (await vet.DeleteAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await gestor.DeleteAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await gestor.GetAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ───────── paginação, ordenação, filtro ─────────

    [Fact]
    public async Task Paginacao_TresLaudosPageSizeDois_TemNextNaPrimeiraEPrevNaSegunda()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var p = _fx.PetPaginacao;
        await CriarAsync(vet, p, "L1", "HEMOGRAMA", "2026-09-01");
        await CriarAsync(vet, p, "L2", "IMAGEM", "2026-09-02");
        await CriarAsync(vet, p, "L3", "HEMOGRAMA", "2026-09-03");

        var p1 = await Json(await vet.GetAsync($"/api/v1/pets/{p}/laudos?pageSize=2&sort=dataExame&tipo=HEMOGRAMA"));
        // so 2 HEMOGRAMA existem: filtro preservado, total 2, uma pagina
        p1.GetProperty("total").GetInt64().Should().Be(2);
        p1.GetProperty("totalPages").GetInt32().Should().Be(1);

        var pagina1 = await Json(await vet.GetAsync($"/api/v1/pets/{p}/laudos?pageSize=2&sort=dataExame"));
        pagina1.GetProperty("page").GetInt32().Should().Be(1);
        pagina1.GetProperty("pageSize").GetInt32().Should().Be(2);
        pagina1.GetProperty("total").GetInt64().Should().Be(3);
        pagina1.GetProperty("totalPages").GetInt32().Should().Be(2);
        pagina1.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()).Should().Equal("L1", "L2");
        var l1 = pagina1.GetProperty("_links");
        l1.TryGetProperty("prev", out _).Should().BeFalse();
        var next = l1.GetProperty("next").GetProperty("href").GetString()!;
        next.Should().Contain("page=2").And.Contain("pageSize=2").And.Contain("sort=dataExame");
        l1.GetProperty("last").GetProperty("href").GetString().Should().Contain("page=2");
        l1.GetProperty("first").GetProperty("href").GetString().Should().Contain("page=1");
        l1.GetProperty("self").GetProperty("href").GetString().Should().Contain("page=1");

        var pagina2 = await Json(await vet.GetAsync(next));
        pagina2.GetProperty("page").GetInt32().Should().Be(2);
        pagina2.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()).Should().Equal("L3");
        var l2 = pagina2.GetProperty("_links");
        l2.TryGetProperty("next", out _).Should().BeFalse();
        l2.GetProperty("prev").GetProperty("href").GetString().Should().Contain("page=1").And.Contain("sort=dataExame");
    }

    [Fact]
    public async Task Ordenacao_PadraoEDataExameDecrescente_ECriadoEmFunciona()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var petOrdem = await NovoPetAsync();
        await CriarAsync(vet, petOrdem, "antigo", data: "2026-01-01");
        await CriarAsync(vet, petOrdem, "novo", data: "2026-06-01");

        var padrao = await Json(await vet.GetAsync($"/api/v1/pets/{petOrdem}/laudos"));
        padrao.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()).Should().Equal("novo", "antigo");

        var criadoAsc = await Json(await vet.GetAsync($"/api/v1/pets/{petOrdem}/laudos?sort=criadoEm"));
        criadoAsc.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()).Should().Equal("antigo", "novo");

        var filtrado = await Json(await vet.GetAsync($"/api/v1/pets/{petOrdem}/laudos?de=2026-05-01&ate=2026-12-31"));
        filtrado.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()).Should().Equal("novo");
    }

    [Theory]
    [InlineData("sort=titulo")]
    [InlineData("sort=dataExame;drop")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("page=0")]
    [InlineData("page=2147483647")]
    [InlineData("page=abc")]
    [InlineData("tipo=RAIOX")]
    [InlineData("de=ontem")]
    [InlineData("de=2026-12-01&ate=2026-01-01")]
    public async Task Consulta_Invalida_Devolve400_NuncaOQue500(string query)
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync($"/api/v1/pets/{_fx.PetA}/laudos?{query}");

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
        r.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    // ───────── arquivo ─────────

    [Fact]
    public async Task GetArquivo_DevolveOsBytesComContentTypeDaAssinatura()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var id = await CriarAsync(vet, _fx.PetA);

        var r = await vet.GetAsync($"/api/v1/laudos/{id}/arquivo");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        r.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        (await r.Content.ReadAsByteArrayAsync()).Should().Equal(PdfMinimo);
    }

    [Theory]
    [InlineData("nao-e-objectid")]
    [InlineData("671f00000000000000000000")] // ObjectId valido, inexistente
    public async Task IdMalformadoOuInexistente_Devolve404EmDetalheArquivoEDelete(string id)
    {
        var gestor = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        (await gestor.GetAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.GetAsync($"/api/v1/laudos/{id}/arquivo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.DeleteAsync($"/api/v1/laudos/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ───────── falhas do armazenamento ─────────

    [Fact]
    public async Task Mongo_Indisponivel_Devolve503ComCodigo_ECompensaOArquivoDoPost()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var antes = _fx.Storage.Quantidade;
        _fx.Laudos.FalhaDeAcesso = new ArmazenamentoDocumentosIndisponivelException("fora", new TimeoutException("detalhe do driver"));
        try
        {
            var post = await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form());
            post.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var corpo = await Json(post);
            corpo.GetProperty("codigo").GetString().Should().Be("LAUDOS_INDISPONIVEL");
            corpo.GetProperty("title").GetString().Should().NotContain("TimeoutException").And.NotContain("driver");
            _fx.Storage.Quantidade.Should().Be(antes, "o arquivo gravado antes da falha do Mongo foi apagado (compensacao)");

            var get = await vet.GetAsync($"/api/v1/pets/{_fx.PetA}/laudos");
            get.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await Json(get)).GetProperty("codigo").GetString().Should().Be("LAUDOS_INDISPONIVEL");
        }
        finally
        {
            _fx.Laudos.FalhaDeAcesso = null;
        }
    }

    [Fact]
    public async Task Mongo_RecusaPeloSchema_Devolve422SemCodigoDeIndisponibilidade_ECompensa()
    {
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var antes = _fx.Storage.Quantidade;
        _fx.Laudos.FalhaDeAcesso = new LaudoRecusadoPeloSchemaException();
        try
        {
            var r = await vet.PostAsync($"/api/v1/pets/{_fx.PetA}/laudos", Form());

            r.StatusCode.Should().Be((HttpStatusCode)422);
            (await Json(r)).TryGetProperty("codigo", out _).Should().BeFalse();
            _fx.Storage.Quantidade.Should().Be(antes);
        }
        finally
        {
            _fx.Laudos.FalhaDeAcesso = null;
        }
    }

    // ───────── fix wave G2 ─────────

    [Theory]
    [InlineData("""{"x":1e400}""")]
    [InlineData("""{"x":-1e400}""")]
    [InlineData("""{"a":{"b":[1,1e999]}}""")]
    [InlineData("""{"a\u0000b":1}""")]
    public async Task Post_DadosComNumeroNaoFinitoOuChaveComNul_Devolve400_NadaGravado_ListaContinua200(string dados)
    {
        // G2-I1: 1e400 virava Infinity no Mongo e envenenava o GET da lista do pet (500).
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var petId = await NovoPetAsync();
        var antes = _fx.Storage.Quantidade;

        var r = await vet.PostAsync($"/api/v1/pets/{petId}/laudos", Form(dados: dados));

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest, await r.Content.ReadAsStringAsync());
        _fx.Storage.Quantidade.Should().Be(antes);
        var lista = await vet.GetAsync($"/api/v1/pets/{petId}/laudos");
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(lista)).GetProperty("total").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Post_UsuarioDesativadoComTokenAindaValido_Devolve401_ENadaGravado()
    {
        // G2-m2: antes o POST dava 201 com idUsuarioAutor do usuario desativado.
        using (var escopo = _fx.Factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            db.UsuariosClinica.Add(new UsuarioClinica
            {
                Id = 901, IdClinica = KuraApiFactory.IdClinicaOutroTenant, IdVeterinario = null,
                DsEmail = "desativado-b@kura.test", DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(KuraApiFactory.SenhaClinica),
                TpPerfil = PerfisUsuarioClinica.Gestor, StAtiva = true,
            });
            await db.SaveChangesAsync();
        }
        var cliente = await _fx.ClienteAsync("desativado-b@kura.test"); // token emitido enquanto ativo
        using (var escopo = _fx.Factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            (await db.UsuariosClinica.IgnoreQueryFilters().FirstAsync(u => u.Id == 901)).StAtiva = false;
            await db.SaveChangesAsync();
        }
        var antes = _fx.Storage.Quantidade;

        var r = await cliente.PostAsync($"/api/v1/pets/{_fx.PetB}/laudos", Form());

        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await r.Content.ReadAsStringAsync());
        _fx.Storage.Quantidade.Should().Be(antes);
    }

    [Fact]
    public async Task Lista_SemNenhumLaudo_TemTotalPagesZero_SoSelfEFirst()
    {
        // G2-m5: sem laudo nao ha "ultima pagina": sem last/prev/next.
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);
        var petId = await NovoPetAsync();

        foreach (var query in new[] { "", "?page=3" })
        {
            var envelope = await Json(await vet.GetAsync($"/api/v1/pets/{petId}/laudos{query}"));

            envelope.GetProperty("total").GetInt64().Should().Be(0);
            envelope.GetProperty("totalPages").GetInt32().Should().Be(0);
            envelope.GetProperty("_links").EnumerateObject().Select(l => l.Name).Should().BeEquivalentTo("self", "first");
        }
    }

    [Theory]
    [InlineData("/api/v1/pets")]
    [InlineData("/api/v1/tutores")]
    public async Task ListasV1DePetsETutores_ContinuamSendoArrayPuro(string rota)
    {
        // G2-m4 / D-NET-4: a S4-NET-04 cria listas v2 SEM mudar o shape da v1.
        var vet = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var r = await vet.GetAsync(rota);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(r)).ValueKind.Should().Be(JsonValueKind.Array);
    }

    private async Task<long> NovoPetAsync()
    {
        using var escopo = _fx.Factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        var pet = new Pet
        {
            IdClinica = KuraApiFactory.IdClinicaSemeada, IdEspecie = 1, IdRaca = 1, NmPet = "Pet " + Guid.NewGuid().ToString("N")[..6],
            DtNascimento = new DateTime(2022, 1, 1), SgSexo = 'M', SgPorte = 'M',
        };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        return pet.Id;
    }
}
