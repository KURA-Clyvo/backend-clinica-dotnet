namespace Kura.IntegrationTests;

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

/// <summary>
/// CORS do app da clínica na web: preflight de origem configurada passa com os cabeçalhos
/// certos; origem fora da lista não recebe <c>Access-Control-Allow-Origin</c>.
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class CorsHttpTests : IClassFixture<CorsHttpTests.FabricaComCors>
{
    private const string OrigemPermitida = "http://localhost:8082";

    public class FabricaComCors : KuraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("CORS_ALLOWED_ORIGINS", $"{OrigemPermitida}, https://clinica.example");
        }
    }

    private readonly FabricaComCors _fabrica;

    public CorsHttpTests(FabricaComCors fabrica) => _fabrica = fabrica;

    private static HttpRequestMessage Preflight(string origem)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        req.Headers.Add("Origin", origem);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return req;
    }

    [Fact]
    public async Task Preflight_de_origem_configurada_e_aceito()
    {
        var resp = await _fabrica.CreateClient().SendAsync(Preflight(OrigemPermitida));

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resp.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle(OrigemPermitida);
    }

    [Fact]
    public async Task Origem_fora_da_lista_nao_recebe_cabecalho()
    {
        var resp = await _fabrica.CreateClient().SendAsync(Preflight("https://outra.example"));

        resp.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
