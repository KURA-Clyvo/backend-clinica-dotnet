namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

/// <summary>
/// G2 fix wave (KURA_BACKLOG_RECEPCAO.md, REC-01) — achado Important #4: o aceite "o link
/// contém o token e o clinicaId da clínica do JWT" estava provado só até a fronteira de um
/// <c>Mock&lt;IGeradorLinkConvite&gt;</c> (ver <c>TutorServiceTests</c>); nenhum teste instanciava
/// o <see cref="GeradorLinkConvite"/> REAL e conferia a URL byte a byte. A G2 mutou o `return`
/// (`clinicaId=999` fixo, rota errada, `//`, parâmetro removido) e a suíte inteira ficou verde —
/// porque o único teste que usa a implementação real (`TutorTokenNaoVazaNoLogTests`) só afirma
/// <c>Contains(token)</c>, nunca a forma completa.
/// </summary>
public class GeradorLinkConviteTests
{
    /// <summary>
    /// <see cref="IConfiguration"/> mínima só com o que <see cref="GeradorLinkConvite"/>
    /// realmente lê (o indexador) — mesmo padrão de <c>TutorTokenNaoVazaNoLogTests</c>.
    /// </summary>
    private sealed class ConfiguracaoFake(string? urlBase) : IConfiguration
    {
        public string? this[string key]
        {
            get => key == "Convite:UrlBaseAppTutor" ? urlBase : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => throw new NotSupportedException();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private static GeradorLinkConvite Criar(string? urlBase) =>
        new(new ConfiguracaoFake(urlBase), NullLogger<GeradorLinkConvite>.Instance);

    [Fact]
    public void GerarLink_BaseSemBarraFinal_MontaUrlExata()
    {
        var sut = Criar("https://tutor.exemplo.com");
        var token = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var link = sut.GerarLink(token, 7L);

        link.Should().Be("https://tutor.exemplo.com/register?token=11111111-2222-3333-4444-555555555555&clinicaId=7");
    }

    [Fact]
    public void GerarLink_BaseComBarraFinal_NaoDuplicaBarra()
    {
        // Mordida (c) original da REC-01 e achado M2 do G2: uma implementação que concatenasse
        // a base com barra sem TrimEnd produziria "...com//register..." — este teste é exato
        // sobre o ponto de junção, não só "contém a rota".
        var sut = Criar("https://tutor.exemplo.com/");
        var token = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var link = sut.GerarLink(token, 7L);

        link.Should().Be("https://tutor.exemplo.com/register?token=11111111-2222-3333-4444-555555555555&clinicaId=7");
        link.Should().NotContain("//register");
    }

    [Fact]
    public void GerarLink_DoisIdClinicaDiferentes_ClinicaIdNaUrlAcompanhaOParametro()
    {
        // Mordida (c)/achado Important #4 do G2: `clinicaId=999` fixo no `return` fazia a
        // suíte inteira ficar verde (874/0) porque nenhum teste conferia a URL exata da
        // implementação real. Este teste falha com qualquer valor fixo — 42 e 77 têm que
        // aparecer, nunca um terceiro número.
        var sut = Criar("https://tutor.exemplo.com");
        var token = Guid.NewGuid();

        sut.GerarLink(token, 42L).Should().Contain("clinicaId=42").And.NotContain("clinicaId=999");
        sut.GerarLink(token, 77L).Should().Contain("clinicaId=77").And.NotContain("clinicaId=999");
    }

    [Fact]
    public void GerarLink_TokenNaUrlEhOTokenPassado()
    {
        var sut = Criar("https://tutor.exemplo.com");
        var token = Guid.NewGuid();

        var link = sut.GerarLink(token, 1L);

        link.Should().Contain($"token={token}");
    }

    // ── URL-encode: nota honesta em vez de teste vazio ──────────────────────
    // GerarLink chama Uri.EscapeDataString sobre token.ToString() (Guid) e idClinica.ToString()
    // (long) — nenhum dos dois produz caractere que RFC 3986 exija escapar (dígitos, letras,
    // hífen), então não existe entrada observável hoje que distinga "com escape" de "sem
    // escape" para estes 2 tipos. O escape existe para blindar contra um tipo futuro que
    // carregasse caractere especial (ex.: se o parâmetro de clínica um dia virasse um código
    // alfanumérico) — não fabricamos um teste que pareça provar isso sem provar nada de verdade.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GerarLink_ConfigAusenteVaziaOuWhitespace_DevolveNull(string? urlBase)
    {
        var sut = Criar(urlBase);

        var link = sut.GerarLink(Guid.NewGuid(), 1L);

        link.Should().BeNull();
    }

    [Fact]
    public void Construtor_ConfigAusente_NaoLanca()
    {
        // Config ausente/vazia não derruba o processo (decisão do maestro) — o teste mais
        // básico de "não lança" antes de qualquer asserção sobre o WARN.
        var act = () => Criar(null);

        act.Should().NotThrow();
    }
}
