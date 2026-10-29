namespace Kura.IntegrationTests;

using System.Net;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Gateway FALSO dos packages Oracle: grava o <c>idClinica</c> recebido e devolve o que o teste
/// mandar. Não executa PL/SQL — a chamada real ao Oracle é NÃO VERIFICADA nesta task (G4).
/// </summary>
public sealed class RotinasCobrancaOracleFake : IRotinasCobrancaOracle
{
    public List<(string Rotina, long IdClinica)> Chamadas { get; } = [];

    /// <summary>Comportamento da próxima chamada; por padrão um JSON que identifica a clínica.</summary>
    public Func<string, long, string> Comportamento { get; set; } = Padrao;

    public static string Padrao(string rotina, long idClinica) =>
        rotina == "listar"
            ? $"[{{\"idClinica\":{idClinica}}}]"
            : $"{{\"idClinica\":{idClinica},\"grupos\":[],\"subTotal\":0.00}}";

    public Task<string> ListarJsonAsync(long idClinica, CancellationToken ct = default) =>
        Executar("listar", idClinica);

    public Task<string> RelatorioJsonAsync(long idClinica, CancellationToken ct = default) =>
        Executar("relatorio", idClinica);

    private Task<string> Executar(string rotina, long idClinica)
    {
        Chamadas.Add((rotina, idClinica));
        return Task.FromResult(Comportamento(rotina, idClinica));
    }
}

public sealed class FinanceiroOracleFixture : IDisposable
{
    /// <summary>GESTOR da clínica 2 (a semeada não tem um; <c>EmailAmbiguo</c> existe nas duas).</summary>
    public const string EmailGestorClinicaB = "gestor-oracle-b@kura.test";

    public RotinasCobrancaOracleFake Gateway { get; } = new();
    public WebApplicationFactory<Program> Factory { get; }

    public FinanceiroOracleFixture()
    {
        Factory = new KuraApiFactory().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IRotinasCobrancaOracle>();
            s.AddSingleton<IRotinasCobrancaOracle>(Gateway);
        }));

        using var escopo = Factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
        db.UsuariosClinica.Add(new UsuarioClinica
        {
            Id = 910,
            IdClinica = KuraApiFactory.IdClinicaOutroTenant,
            IdVeterinario = null,
            DsEmail = EmailGestorClinicaB,
            DsSenhaHash = BCrypt.Net.BCrypt.HashPassword(KuraApiFactory.SenhaClinica),
            TpPerfil = PerfisUsuarioClinica.Gestor,
            StAtiva = true,
        });
        db.SaveChanges();
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
/// S4-DB-06 — as duas rotas que chamam os packages Oracle, pelo host real e com o gateway FALSO.
/// Prova: política (GESTOR 200 / VETERINARIO 403 / anônimo 401), escopo de clínica com DUAS
/// clínicas, e o mapeamento de erro para 503/404/500 com título fixo e sem texto do Oracle.
/// ⚠️ Não prova o Oracle: <b>chamada ao Oracle real NÃO VERIFICADA nesta task</b>.
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class FinanceiroOracleHttpTests : IClassFixture<FinanceiroOracleFixture>
{
    private const string RotaExportacao = "/api/v1/financeiro/cobrancas/exportacao-json";
    private const string RotaRelatorio = "/api/v1/financeiro/cobrancas/relatorio-oracle";

    private readonly FinanceiroOracleFixture _fx;

    public FinanceiroOracleHttpTests(FinanceiroOracleFixture fx)
    {
        _fx = fx;
        _fx.Gateway.Comportamento = RotinasCobrancaOracleFake.Padrao;
        _fx.Gateway.Chamadas.Clear();
    }

    [Theory]
    [InlineData(RotaExportacao, "listar")]
    [InlineData(RotaRelatorio, "relatorio")]
    public async Task Gestor_recebe_200_com_o_json_do_gateway_e_a_clinica_do_token(string rota, string rotina)
    {
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await resposta.Content.ReadAsStringAsync())
            .Should().Be(RotinasCobrancaOracleFake.Padrao(rotina, KuraApiFactory.IdClinicaSemeada));
        _fx.Gateway.Chamadas.Should().ContainSingle()
            .Which.Should().Be((rotina, KuraApiFactory.IdClinicaSemeada));
    }

    [Theory]
    [InlineData(RotaExportacao, "listar")]
    [InlineData(RotaRelatorio, "relatorio")]
    public async Task Gestor_da_outra_clinica_chega_ao_gateway_com_a_SUA_clinica(string rota, string rotina)
    {
        // Duas clínicas: com uma só, "usou a do token" seria indistinguível de "usou um id fixo".
        var client = await _fx.ClienteAsync(FinanceiroOracleFixture.EmailGestorClinicaB);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        _fx.Gateway.Chamadas.Should().ContainSingle()
            .Which.Should().Be((rotina, KuraApiFactory.IdClinicaOutroTenant));
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Clinica_passada_na_query_string_e_ignorada(string rota)
    {
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        var resposta = await client.GetAsync(
            $"{rota}?idClinica={KuraApiFactory.IdClinicaOutroTenant}&clinicaId={KuraApiFactory.IdClinicaOutroTenant}");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        _fx.Gateway.Chamadas.Should().ContainSingle()
            .Which.IdClinica.Should().Be(KuraApiFactory.IdClinicaSemeada);
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Veterinario_e_barrado_com_403_e_o_gateway_nao_e_chamado(string rota)
    {
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailClinica);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _fx.Gateway.Chamadas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Sem_token_devolve_401_e_o_gateway_nao_e_chamado(string rota)
    {
        var resposta = await _fx.Factory.CreateClient().GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _fx.Gateway.Chamadas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Rotina_indisponivel_devolve_503_com_titulo_fixo_e_sem_texto_do_Oracle(string rota)
    {
        // O inner carrega o texto que o Oracle de verdade devolve; ele pode ir para o LOG do
        // servidor, nunca para o corpo da resposta.
        _fx.Gateway.Comportamento = (_, _) => throw new RotinaOracleIndisponivelException(
            new InvalidOperationException("ORA-04063: package body \"KURA.PKG_COBRANCA\" has errors"));
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var corpo = await resposta.Content.ReadAsStringAsync();
        System.Text.Json.JsonDocument.Parse(corpo).RootElement.GetProperty("title").GetString()
            .Should().Be("Rotina Oracle indisponível.");
        corpo.Should().NotContain("ORA-").And.NotContain("PKG_").And.NotContain("KURA.");
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Clinica_inexistente_20002_devolve_404_sem_texto_do_Oracle(string rota)
    {
        _fx.Gateway.Comportamento = (_, id) => throw new EntidadeNaoEncontradaException("Clinica", id);
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain("ORA-").And.NotContain("PKG_");
    }

    [Theory]
    [InlineData(RotaExportacao)]
    [InlineData(RotaRelatorio)]
    public async Task Falha_20001_devolve_500_com_titulo_fixo_e_sem_texto_do_Oracle(string rota)
    {
        _fx.Gateway.Comportamento = (_, _) => throw new RotinaOracleFalhouException(
            new InvalidOperationException("ORA-20001: p_id_clinica nulo ORA-06512: at \"KURA.PKG_COBRANCA\", line 9"));
        var client = await _fx.ClienteAsync(KuraApiFactory.EmailGestorPuro);

        var resposta = await client.GetAsync(rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain($"\"title\":\"{RotinaOracleFalhouException.MensagemFixa}\"");
        corpo.Should().NotContain("ORA-").And.NotContain("PKG_").And.NotContain("KURA.");
    }
}
