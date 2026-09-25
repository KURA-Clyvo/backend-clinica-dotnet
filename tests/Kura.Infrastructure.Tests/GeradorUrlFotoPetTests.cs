namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.Services;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Kura.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;

/// <summary>
/// FT-04 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — <see cref="GeradorUrlFotoPet"/> em
/// isolamento (sem HTTP real): base da URL (config × derivada do request), validade (default
/// × configurada) e que a <c>sig</c> produzida realmente valida contra o MESMO algoritmo
/// (<see cref="AssinadorUrlFotoHmac"/>, FT-02) — sem duplicar o vetor fixo daquela task, só
/// confirmando que os dois lados desta classe (gerar/validar) concordam.
/// </summary>
public sealed class GeradorUrlFotoPetTests
{
    private const string Segredo = "chave-de-teste-com-mais-de-32-bytes-ok!!";
    private static readonly DateTimeOffset Agora = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private static IConfiguration Config(Dictionary<string, string?>? chaves = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(chaves ?? []).Build();

    private static IHttpContextAccessor AccessorComRequest(string scheme, string host, string pathBase = "")
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Scheme = scheme;
        contexto.Request.Host = new HostString(host);
        contexto.Request.PathBase = pathBase;

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(contexto);
        return accessor.Object;
    }

    /// <summary>Sem <c>IHttpContextAccessor.HttpContext</c> (fora de um request) e sem <c>Foto:UrlBase</c>.</summary>
    private static IHttpContextAccessor AccessorSemContexto()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        return accessor.Object;
    }

    private static Dictionary<string, string> QueryDe(string url)
    {
        var uri = new Uri(url);
        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(par => par.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => Uri.UnescapeDataString(kv[1]));
    }

    [Fact]
    public void GerarUrl_ChaveBaseNula_DevolveNull()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorComRequest("http", "localhost"),
            Config(), new RelogioFixo(Agora));

        sut.GerarUrl(null, ChaveFotoPet.SufixoThumb).Should().BeNull();
    }

    [Fact]
    public void GerarUrl_ChaveBaseVazia_DevolveNull()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorComRequest("http", "localhost"),
            Config(), new RelogioFixo(Agora));

        sut.GerarUrl(string.Empty, ChaveFotoPet.SufixoThumb).Should().BeNull();
    }

    [Fact]
    public void GerarUrl_SemFotoUrlBase_DerivaSchemeHostEPathBaseDoRequestAtual()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo),
            AccessorComRequest("https", "kura-api:8080", "/back"),
            Config(), new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoThumb);

        url.Should().StartWith("https://kura-api:8080/back/api/v1/fotos/clinica/1/pet/2/abc_256.webp?");
    }

    [Fact]
    public void GerarUrl_ComFotoUrlBaseConfigurado_UsaConfigEIgnoraORequestAtual()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo),
            AccessorComRequest("http", "host-que-nao-deveria-aparecer-na-url"),
            Config(new Dictionary<string, string?> { ["Foto:UrlBase"] = "https://cdn.kura.vet/" }),
            new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoMedia);

        url.Should().StartWith("https://cdn.kura.vet/api/v1/fotos/clinica/1/pet/2/abc_1080.webp?");
        url.Should().NotContain("host-que-nao-deveria-aparecer-na-url");
    }

    [Fact]
    public void GerarUrl_SemHttpContextEComFotoUrlBaseConfigurado_FuncionaMesmoAssim()
    {
        // Cenário de job em background (sem request HTTP em curso) — só funciona porque
        // Foto:UrlBase está configurado; ver o próximo teste para o caso sem os dois.
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorSemContexto(),
            Config(new Dictionary<string, string?> { ["Foto:UrlBase"] = "https://cdn.kura.vet" }),
            new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoThumb);

        url.Should().StartWith("https://cdn.kura.vet/api/v1/fotos/");
    }

    [Fact]
    public void GerarUrl_SemHttpContextEDemFotoUrlBase_Lanca()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorSemContexto(),
            Config(), new RelogioFixo(Agora));

        var act = () => sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoThumb);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GerarUrl_SemFotoValidadeUrlHoras_UsaDefault24Horas()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorComRequest("http", "localhost"),
            Config(), new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoThumb)!;
        var exp = long.Parse(QueryDe(url)["exp"]);

        exp.Should().Be(Agora.AddHours(24).ToUnixTimeSeconds());
    }

    [Fact]
    public void GerarUrl_ComFotoValidadeUrlHorasConfigurado_UsaOValorConfigurado()
    {
        var sut = new GeradorUrlFotoPet(
            new AssinadorUrlFotoHmac(Segredo), AccessorComRequest("http", "localhost"),
            Config(new Dictionary<string, string?> { ["Foto:ValidadeUrlHoras"] = "1" }),
            new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", ChaveFotoPet.SufixoThumb)!;
        var exp = long.Parse(QueryDe(url)["exp"]);

        exp.Should().Be(Agora.AddHours(1).ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(ChaveFotoPet.SufixoThumb, "clinica/1/pet/2/abc_256.webp")]
    [InlineData(ChaveFotoPet.SufixoMedia, "clinica/1/pet/2/abc_1080.webp")]
    public void GerarUrl_SigProduzidaValidaContraOMesmoAssinadorParaAVarianteCerta(
        string sufixo, string chaveVarianteEsperada)
    {
        var assinador = new AssinadorUrlFotoHmac(Segredo);
        var sut = new GeradorUrlFotoPet(
            assinador, AccessorComRequest("http", "localhost"), Config(), new RelogioFixo(Agora));

        var url = sut.GerarUrl("clinica/1/pet/2/abc.webp", sufixo)!;
        url.Should().Contain($"/api/v1/fotos/{chaveVarianteEsperada}?");

        var query = QueryDe(url);
        var exp = long.Parse(query["exp"]);
        var sig = query["sig"];

        assinador.Validar(chaveVarianteEsperada, exp, sig, Agora).Should().BeTrue(
            "a sig que este gerador produz tem de validar contra o MESMO IAssinadorUrlFoto " +
            "usado para gerar — senão a URL que o DTO devolve nunca abriria a própria foto");

        // Controle negativo: a MESMA sig contra a variante ERRADA (thumb×media) não bate —
        // prova que a sig está ligada à chave da variante, não só à chave base.
        var outraVariante = sufixo == ChaveFotoPet.SufixoThumb
            ? "clinica/1/pet/2/abc_1080.webp"
            : "clinica/1/pet/2/abc_256.webp";
        assinador.Validar(outraVariante, exp, sig, Agora).Should().BeFalse();
    }
}
