namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

/// <summary>
/// LU-08 — <c>GET /api/v1/luna/triagens</c> exercitado de ponta a ponta sobre HTTP real:
/// <c>POST /luna/interactions</c> (API Key) → <c>POST /luna/triage</c> (API Key) →
/// <c>GET /luna/triagens</c> (JWT) → <c>GET /luna/triagens/relatorio</c> (JWT).
///
/// <para>
/// 🔴 <b>Por que HTTP e não só service/repositório.</b> Os dois arquivos irmãos —
/// <c>LunaServiceTests</c> (mocks) e <c>TriagemLunaRepositoryTenantIsolationTests</c>
/// (repositório contra InMemory direto, é onde mora a MORDIDA da regra 13 sobre o join) —
/// não exercitam o pipeline completo: FluentValidation dos 2 endpoints de escrita,
/// <c>LunaApiKeyAuthFilter</c>, <c>[Authorize]</c> de leitura, e principalmente o contrato
/// de resposta que o <c>LU-09</c> vai consumir como fixture literal. Esta classe fecha essa
/// lacuna e é a fonte do corpo JSON colado no relatório da task.
/// </para>
///
/// <para>
/// ⚠️ <b>Fábrica PRÓPRIA por teste (<c>new KuraApiFactory()</c>), NÃO <c>IClassFixture</c>.</b>
/// Mesmo padrão de <c>UsuariosClinicaHttpTests.Desativar_o_ultimo_gestor_da_clinica_devolve_422</c>:
/// esta classe depende de CONTAGEM exata (<c>total</c>, ordenação, paginação) de
/// <c>GET /luna/triagens</c>. Um host/banco InMemory compartilhado entre os dois métodos desta
/// classe (via <c>IClassFixture</c>) faria o teste de um método contar as triagens gravadas
/// pelo outro — a primeira versão desta classe tentou contornar isso com uma janela de
/// data "agora ± 1 min" e MEDIU o vazamento: os dois métodos rodam a poucos segundos um do
/// outro, então a janela ampla incluía a triagem ALTA do teste de isolamento dentro da
/// contagem do teste de fluxo completo (<c>HaveCount(3)</c> falhando com 4 itens reais).
/// Fábrica isolada por teste elimina a causa em vez de apertar a janela.
/// </para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class LunaTriagemFilaHttpTests
{
    private const string ApiKeyHeader = "X-Api-Key";
    private const string ApiKeyValor = "luna-api-key-de-integracao"; // ver KuraApiFactory.ConfigureWebHost

    private readonly ITestOutputHelper _output;

    public LunaTriagemFilaHttpTests(ITestOutputHelper output) => _output = output;

    private static HttpClient ClienteApiKey(KuraApiFactory fabrica)
    {
        var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyHeader, ApiKeyValor);
        return client;
    }

    private static async Task<HttpClient> ClienteJwtAsync(KuraApiFactory fabrica, string? email = null)
    {
        var client = fabrica.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client, email));
        return client;
    }

    /// <summary>Semeia um tutor mínimo na clínica informada — necessário para
    /// POST /luna/interactions (deriva IdClinica do tutor) e para o mapeamento
    /// tutor{id,nome} de GET /luna/triagens.</summary>
    private static async Task<long> SemearTutorAsync(KuraApiFactory fabrica, long id, long idClinica, string nome)
    {
        using var escopo = fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();

        db.Tutores.Add(new Tutor
        {
            Id = id,
            IdClinica = idClinica,
            NmTutor = nome,
            NrCpf = $"1112223{id:D4}",
            DsEmail = $"tutor{id}@kura.test",
            NrTelefone = $"1199999{id:D4}",
            StAtiva = true,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<long> RegistrarInteracaoAsync(HttpClient apiKeyClient, long idTutor, string conteudo)
    {
        var resposta = await apiKeyClient.PostAsJsonAsync("/api/v1/luna/interactions", new
        {
            id_tutor = idTutor,
            ds_canal = "WHATSAPP",
            ds_direcao = "INBOUND",
            ds_conteudo = conteudo,
            dt_recebimento = DateTime.UtcNow,
        });
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id_interacao").GetInt64();
    }

    private static async Task<long> RegistrarTriagemAsync(
        HttpClient apiKeyClient,
        long idInteracao,
        long idTutor,
        string urgencia,
        int score,
        string[] sintomas,
        string? regrasVersao = "1.1")
    {
        // regrasVersao null ⇒ a chave "regras_versao" é OMITIDA do corpo (retrocompat real:
        // payload sem o campo, não payload com o campo setado como null).
        object corpo = regrasVersao is null
            ? new
            {
                id_interacao = idInteracao,
                id_tutor = idTutor,
                sintomas,
                ds_urgencia = urgencia,
                nr_score = score,
                ds_recomendacao = "Orientação automática — não substitui avaliação veterinária",
            }
            : new
            {
                id_interacao = idInteracao,
                id_tutor = idTutor,
                sintomas,
                ds_urgencia = urgencia,
                nr_score = score,
                ds_recomendacao = "Orientação automática — não substitui avaliação veterinária",
                regras_versao = regrasVersao,
            };

        var resposta = await apiKeyClient.PostAsJsonAsync("/api/v1/luna/triage", corpo);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id_triagem").GetInt64();
    }

    /// <summary>
    /// 🔴 A MORDIDA PRINCIPAL do critério de aceite do brief, de ponta a ponta: retrocompat
    /// de <c>regras_versao</c> (com e sem o campo), <c>StEncaminhadoVet</c> por urgência
    /// (ALTA=true, MEDIA/BAIXA=false), paginação, filtro de urgência, ordenação
    /// ALTA→MEDIA→BAIXA, e o corpo JSON literal — logado via <see cref="ITestOutputHelper"/>
    /// para colar no relatório (fixture do LU-09).
    /// </summary>
    [Fact]
    public async Task FluxoCompleto_RetrocompatEncaminhamentoPaginacaoEOrdenacao()
    {
        using var fabrica = new KuraApiFactory();

        var apiKey = ClienteApiKey(fabrica);
        var tutorId = await SemearTutorAsync(fabrica, 9001, KuraApiFactory.IdClinicaSemeada, "Tutor do Fluxo Completo");

        // ALTA, COM regras_versao — encaminha, versão persistida.
        var interacaoAlta = await RegistrarInteracaoAsync(
            apiKey, tutorId, "Meu cachorro está vomitando muito e muito fraco, o que eu faço?");
        var idAlta = await RegistrarTriagemAsync(
            apiKey, interacaoAlta, tutorId, "ALTA", 88, ["vomito", "letargia"], regrasVersao: "1.1");

        // MEDIA, SEM regras_versao — retrocompat: payload de uma versão anterior ao LU-07.
        var interacaoMedia = await RegistrarInteracaoAsync(apiKey, tutorId, "Meu gato está espirrando bastante");
        var idMedia = await RegistrarTriagemAsync(
            apiKey, interacaoMedia, tutorId, "MEDIA", 40, ["espirro"], regrasVersao: null);

        // BAIXA
        var interacaoBaixa = await RegistrarInteracaoAsync(apiKey, tutorId, "Só uma dúvida sobre ração");
        var idBaixa = await RegistrarTriagemAsync(
            apiKey, interacaoBaixa, tutorId, "BAIXA", 5, ["duvida"]);

        var jwt = await ClienteJwtAsync(fabrica);

        // ── GET /luna/triagens sem filtro — ordenação + corpo JSON literal ──
        var respostaLista = await jwt.GetAsync("/api/v1/luna/triagens?page=1&pageSize=20");
        respostaLista.StatusCode.Should().Be(HttpStatusCode.OK);

        var corpoJson = await respostaLista.Content.ReadAsStringAsync();
        _output.WriteLine("=== CORPO JSON LITERAL — GET /api/v1/luna/triagens (fixture LU-09) ===");
        _output.WriteLine(corpoJson);
        _output.WriteLine("=== FIM DO CORPO ===");

        using (var doc = JsonDocument.Parse(corpoJson))
        {
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
            items.Should().HaveCount(3);
            doc.RootElement.GetProperty("total").GetInt32().Should().Be(3);

            // Ordenação: ALTA -> MEDIA -> BAIXA.
            items[0].GetProperty("idTriagem").GetInt64().Should().Be(idAlta);
            items[0].GetProperty("urgencia").GetString().Should().Be("ALTA");
            items[0].GetProperty("encaminhadoVet").GetBoolean().Should().BeTrue(
                "D-L5: ALTA marca StEncaminhadoVet=true");
            items[0].GetProperty("regrasVersao").GetString().Should().Be("1.1");
            items[0].GetProperty("score").GetInt32().Should().Be(88);
            items[0].GetProperty("sintomas").EnumerateArray().Select(s => s.GetString())
                .Should().BeEquivalentTo(["vomito", "letargia"]);
            items[0].GetProperty("tutor").GetProperty("nome").GetString().Should().Be("Tutor do Fluxo Completo");
            items[0].GetProperty("trechoMensagem").GetString().Should().NotBeNullOrEmpty();
            items[0].GetProperty("trechoMensagem").GetString()!.Length.Should().BeLessThanOrEqualTo(280);
            // LGPD (brief item 3): telefone do tutor NÃO entra no item.
            doc.RootElement.GetRawText().Should().NotContain(
                "1199999", "trechoMensagem/telefone do tutor não pode vazar na fila");

            items[1].GetProperty("idTriagem").GetInt64().Should().Be(idMedia);
            items[1].GetProperty("urgencia").GetString().Should().Be("MEDIA");
            items[1].GetProperty("encaminhadoVet").GetBoolean().Should().BeFalse();
            items[1].GetProperty("regrasVersao").ValueKind.Should().Be(JsonValueKind.Null,
                "retrocompat: payload sem regras_versao persiste e lista null, não quebra");

            items[2].GetProperty("idTriagem").GetInt64().Should().Be(idBaixa);
            items[2].GetProperty("urgencia").GetString().Should().Be("BAIXA");
            items[2].GetProperty("encaminhadoVet").GetBoolean().Should().BeFalse();
        }

        // ── Filtro de urgência ──
        var respostaFiltroAlta = await jwt.GetAsync("/api/v1/luna/triagens?urgencia=ALTA");
        var filtroAlta = await respostaFiltroAlta.Content.ReadFromJsonAsync<JsonElement>();
        filtroAlta.GetProperty("total").GetInt32().Should().Be(1);
        filtroAlta.GetProperty("items")[0].GetProperty("idTriagem").GetInt64().Should().Be(idAlta);

        // ── Filtro de período: mesmo validador/limite do relatório (90 dias) ──
        var hoje = DateTime.UtcNow;
        var respostaPeriodo = await jwt.GetAsync(
            $"/api/v1/luna/triagens?dataInicio={Uri.EscapeDataString(hoje.AddDays(-1).ToString("O"))}"
            + $"&dataFim={Uri.EscapeDataString(hoje.AddDays(1).ToString("O"))}");
        respostaPeriodo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respostaPeriodo.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("total").GetInt32().Should().Be(3, "as 3 triagens foram gravadas AGORA, dentro da janela de ±1 dia");

        var respostaPeriodoInvalido = await jwt.GetAsync(
            $"/api/v1/luna/triagens?dataInicio={Uri.EscapeDataString(hoje.AddDays(-91).ToString("O"))}"
            + $"&dataFim={Uri.EscapeDataString(hoje.ToString("O"))}");
        respostaPeriodoInvalido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "período > 90 dias usa o mesmo validador do relatório — 422 documentado no controller");

        // ── Paginação: pageSize=1 mantém total=3, cada página com 1 item, sem overlap ──
        var pagina1 = await (await jwt.GetAsync("/api/v1/luna/triagens?page=1&pageSize=1"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var pagina2 = await (await jwt.GetAsync("/api/v1/luna/triagens?page=2&pageSize=1"))
            .Content.ReadFromJsonAsync<JsonElement>();

        pagina1.GetProperty("total").GetInt32().Should().Be(3);
        pagina1.GetProperty("items").GetArrayLength().Should().Be(1);
        pagina2.GetProperty("items").GetArrayLength().Should().Be(1);
        pagina1.GetProperty("items")[0].GetProperty("idTriagem").GetInt64()
            .Should().NotBe(pagina2.GetProperty("items")[0].GetProperty("idTriagem").GetInt64());
        pagina1.GetProperty("items")[0].GetProperty("idTriagem").GetInt64().Should().Be(idAlta,
            "página 1 com pageSize=1 traz a primeira da ordenação (ALTA)");

        // ── Relatório: encaminhadasParaVet >= 1 (fecha A2 — KPI deixou de ser zero) ──
        var respostaRelatorio = await jwt.GetAsync(
            $"/api/v1/luna/triagens/relatorio?dataInicio={Uri.EscapeDataString(hoje.AddDays(-1).ToString("O"))}"
            + $"&dataFim={Uri.EscapeDataString(hoje.AddDays(1).ToString("O"))}");
        respostaRelatorio.StatusCode.Should().Be(HttpStatusCode.OK);
        var relatorio = await respostaRelatorio.Content.ReadFromJsonAsync<JsonElement>();
        relatorio.GetProperty("encaminhadasParaVet").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        relatorio.GetProperty("totalTriagens").GetInt32().Should().Be(3);
    }

    /// <summary>
    /// Isolamento de ponta a ponta (pipeline completo, não só o join do repositório —
    /// esse já está coberto, com a mutação da regra 13, em
    /// <c>TriagemLunaRepositoryTenantIsolationTests</c>). Duas clínicas, tutor e triagem
    /// próprios de cada uma; JWT de A nunca vê a triagem de B.
    /// </summary>
    [Fact]
    public async Task Isolamento_triagem_de_outra_clinica_nunca_aparece_para_jwt_da_clinica_A()
    {
        using var fabrica = new KuraApiFactory();

        var apiKey = ClienteApiKey(fabrica);
        var tutorA = await SemearTutorAsync(fabrica, 9101, KuraApiFactory.IdClinicaSemeada, "Tutor A");
        var tutorB = await SemearTutorAsync(fabrica, 9102, KuraApiFactory.IdClinicaOutroTenant, "Tutor B");

        var interacaoA = await RegistrarInteracaoAsync(apiKey, tutorA, "Mensagem da clinica A");
        var idTriagemA = await RegistrarTriagemAsync(apiKey, interacaoA, tutorA, "ALTA", 70, ["sintomaA"]);

        var interacaoB = await RegistrarInteracaoAsync(apiKey, tutorB, "Mensagem da clinica B — nao pode vazar");
        var idTriagemB = await RegistrarTriagemAsync(apiKey, interacaoB, tutorB, "ALTA", 70, ["sintomaB"]);

        var jwtA = await ClienteJwtAsync(fabrica);
        var respostaA = await jwtA.GetAsync("/api/v1/luna/triagens?pageSize=50");
        var corpoA = await respostaA.Content.ReadFromJsonAsync<JsonElement>();

        corpoA.GetProperty("total").GetInt32().Should().Be(1,
            "só a triagem da própria clínica (A) pode aparecer — a de B não deve contar");
        var idsA = corpoA.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("idTriagem").GetInt64()).ToList();

        idsA.Should().Contain(idTriagemA);
        idsA.Should().NotContain(idTriagemB, "triagem da clínica B não pode aparecer para o JWT de A");

        // Controle positivo: o mesmo host/banco, JWT da clínica B, enxerga a própria
        // triagem — prova que a ausência acima não é vácuo (o registro existe e é
        // alcançável por quem de fato é da clínica B).
        var jwtB = await ClienteJwtAsync(fabrica, email: "outro-tenant@kura.test");
        var respostaB = await jwtB.GetAsync("/api/v1/luna/triagens?pageSize=50");
        var corpoB = await respostaB.Content.ReadFromJsonAsync<JsonElement>();
        var idsB = corpoB.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("idTriagem").GetInt64()).ToList();

        idsB.Should().Contain(idTriagemB);
        idsB.Should().NotContain(idTriagemA);
    }
}
