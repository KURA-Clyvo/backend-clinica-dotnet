namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Pet;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Kura.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FT-04 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — <c>GET /api/v1/fotos/{*chave}</c> e as
/// URLs assinadas em <c>PetResponseDto</c>, exercitados por ROTA HTTP real.
///
/// <para><b>Por que cada teste cria sua PRÓPRIA <see cref="KuraApiFactory"/></b> — mesmo
/// padrão de <c>PetFotoIntegridadeDiscoHttpTests</c> (FT-03): este endpoint lê do
/// <see cref="Kura.Infrastructure.Storage.ArmazenamentoLocalDisco"/> REAL (não mock), e cada
/// teste precisa de um <c>Storage:BasePath</c> isolado — dois testes rodando com o mesmo
/// diretório poderiam colidir em chaves geradas por <c>Guid.NewGuid()</c> só por acidente de
/// timing, e a limpeza (<c>Directory.Delete</c> no <c>finally</c>) fica mais simples por
/// teste do que compartilhada.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class PetFotoServirHttpTests
{
    private static byte[] Trailing(byte semente) => [.. Enumerable.Range(0, 8).Select(i => (byte)(semente + i))];

    private static byte[] Imagem(string formato, byte semente) => formato switch
    {
        "jpg" => [0xFF, 0xD8, 0xFF, 0xE0, .. Trailing(semente)],
        "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Trailing(semente)],
        "webp" => [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray(), .. Trailing(semente)],
        _ => throw new ArgumentOutOfRangeException(nameof(formato)),
    };

    private static readonly IReadOnlyDictionary<string, string> ContentTypeEsperado = new Dictionary<string, string>
    {
        ["jpg"] = "image/jpeg",
        ["png"] = "image/png",
        ["webp"] = "image/webp",
    };

    private sealed record Ambiente(
        WebApplicationFactory<Program> Factory, HttpClient Client, string DiretorioTemporario, long IdPet, long IdOutroPet)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            if (Directory.Exists(DiretorioTemporario))
                Directory.Delete(DiretorioTemporario, recursive: true);
        }
    }

    private static async Task<Ambiente> CriarAmbienteAsync()
    {
        var diretorioTemporario = Path.Combine(Path.GetTempPath(), "ft04-servir-" + Guid.NewGuid().ToString("N"));
        var factoryBase = new KuraApiFactory();
        var factory = factoryBase.WithWebHostBuilder(
            builder => builder.UseSetting("Storage:BasePath", diretorioTemporario));

        long idPet, idOutroPet;
        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            var pet = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Rex FT-04",
                DtNascimento = new DateTime(2022, 1, 1),
                SgSexo = 'M',
                SgPorte = 'M',
            };
            var outroPet = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Bob FT-04",
                DtNascimento = new DateTime(2021, 1, 1),
                SgSexo = 'M',
                SgPorte = 'P',
            };
            db.Pets.AddRange(pet, outroPet);
            db.SaveChanges();
            idPet = pet.Id;
            idOutroPet = outroPet.Id;
        }

        var client = factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        return new Ambiente(factory, client, diretorioTemporario, idPet, idOutroPet);
    }

    private static async Task<PetFotoResponseDto> UploadAsync(
        HttpClient client, long idPet, byte[] thumb, byte[] media)
    {
        var conteudo = new MultipartFormDataContent
        {
            { new ByteArrayContent(thumb), "thumb", "t.bin" },
            { new ByteArrayContent(media), "media", "m.bin" },
        };

        var resposta = await client.PostAsync($"/api/v1/pets/{idPet}/foto", conteudo);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return (await resposta.Content.ReadFromJsonAsync<PetFotoResponseDto>())!;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite: a URL do DTO baixa o arquivo — ponta a ponta, bytes iguais, headers corretos
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task UploadEGetPet_DsFotoUrlEDsFotoThumbUrl_BaixamOsBytesCorretosComHeadersCertos(string formato)
    {
        await using var ambiente = await CriarAmbienteAsync();
        var thumb = Imagem(formato, 0x01);
        var media = Imagem(formato, 0x11);

        var upload = await UploadAsync(ambiente.Client, ambiente.IdPet, thumb, media);
        upload.DsFotoChave.Should().EndWith($".{formato}");

        var respostaPet = await ambiente.Client.GetAsync($"/api/v1/pets/{ambiente.IdPet}");
        respostaPet.StatusCode.Should().Be(HttpStatusCode.OK);
        var pet = (await respostaPet.Content.ReadFromJsonAsync<PetResponseDto>())!;

        pet.DsFotoUrl.Should().NotBeNullOrEmpty();
        pet.DsFotoThumbUrl.Should().NotBeNullOrEmpty();
        pet.DsFotoUrl.Should().Contain("_1080.");
        pet.DsFotoThumbUrl.Should().Contain("_256.");
        pet.DsFotoUrl.Should().NotBe(pet.DsFotoThumbUrl);

        var respostaMedia = await ambiente.Client.GetAsync(pet.DsFotoUrl);
        var respostaThumb = await ambiente.Client.GetAsync(pet.DsFotoThumbUrl);

        respostaMedia.StatusCode.Should().Be(HttpStatusCode.OK);
        respostaThumb.StatusCode.Should().Be(HttpStatusCode.OK);

        respostaMedia.Content.Headers.ContentType!.MediaType.Should().Be(ContentTypeEsperado[formato]);
        respostaThumb.Content.Headers.ContentType!.MediaType.Should().Be(ContentTypeEsperado[formato]);

        (await respostaMedia.Content.ReadAsByteArrayAsync()).Should().Equal(media,
            "a URL de detalhe (_1080) tem de devolver EXATAMENTE os bytes de 'media', não os de 'thumb'");
        (await respostaThumb.Content.ReadAsByteArrayAsync()).Should().Equal(thumb,
            "a URL de lista (_256) tem de devolver EXATAMENTE os bytes de 'thumb', não os de 'media'");

        foreach (var resposta in new[] { respostaMedia, respostaThumb })
        {
            // System.Net.Http normaliza a ORDEM dos tokens de Cache-Control ao fazer parse
            // (o valor NA REDE é "private, max-age=31536000, immutable", literal do
            // controller — conferir aqui pelas propriedades, não por igualdade de string,
            // evita depender de como o cliente reordena ao montar CacheControlHeaderValue).
            var cacheControl = resposta.Headers.CacheControl!;
            cacheControl.Private.Should().BeTrue();
            cacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(31536000));
            cacheControl.Extensions.Should().Contain(e => e.Name == "immutable");

            resposta.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
            nosniff!.Should().ContainSingle().Which.Should().Be("nosniff");
        }
    }

    [Fact]
    public async Task GetPet_SemFoto_DsFotoUrlEDsFotoThumbUrlSaoNulos()
    {
        await using var ambiente = await CriarAmbienteAsync();

        var resposta = await ambiente.Client.GetAsync($"/api/v1/pets/{ambiente.IdPet}");
        var pet = (await resposta.Content.ReadFromJsonAsync<PetResponseDto>())!;

        pet.DsFotoUrl.Should().BeNull();
        pet.DsFotoThumbUrl.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite: adulterações de sig/exp → 403 (mesmo status/corpo para as duas classes de erro)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFoto_SigAdulterada_Devolve403()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var upload = await UploadAsync(
            ambiente.Client, ambiente.IdPet, Imagem("jpg", 0x01), Imagem("jpg", 0x11));
        var pet = await ObterPetAsync(ambiente.Client, ambiente.IdPet);

        var urlAdulterada = TrocarQuery(pet.DsFotoThumbUrl!, sig: TrocarPrimeiroChar(ExtrairQuery(pet.DsFotoThumbUrl!)["sig"]));

        var resposta = await ambiente.Client.GetAsync(urlAdulterada);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFoto_ExpAlterado_Devolve403()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await UploadAsync(ambiente.Client, ambiente.IdPet, Imagem("jpg", 0x01), Imagem("jpg", 0x11));
        var pet = await ObterPetAsync(ambiente.Client, ambiente.IdPet);

        var query = ExtrairQuery(pet.DsFotoThumbUrl!);
        var expAlterado = (long.Parse(query["exp"]) + 3600).ToString();
        var url = TrocarQuery(pet.DsFotoThumbUrl!, exp: expAlterado);

        var resposta = await ambiente.Client.GetAsync(url);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "exp alterado muda a mensagem assinada — a MESMA sig não cobre o novo exp");
    }

    [Fact]
    public async Task GetFoto_ExpVencido_Devolve403()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var upload = await UploadAsync(ambiente.Client, ambiente.IdPet, Imagem("jpg", 0x01), Imagem("jpg", 0x11));

        // Assina de propósito com exp no PASSADO, usando o MESMO IAssinadorUrlFoto do host —
        // isola "exp vencido" (sig genuína, mas fora da validade) de "sig forjada".
        using var escopo = ambiente.Factory.Services.CreateScope();
        var assinador = escopo.ServiceProvider.GetRequiredService<IAssinadorUrlFoto>();
        var chaveThumb = ChaveFotoPet.Variante(upload.DsFotoChave, ChaveFotoPet.SufixoThumb);
        var expiraEm = DateTimeOffset.UtcNow.AddHours(-1);
        var sig = assinador.Assinar(chaveThumb, expiraEm);

        var resposta = await ambiente.Client.GetAsync(
            $"/api/v1/fotos/{chaveThumb}?exp={expiraEm.ToUnixTimeSeconds()}&sig={sig}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFoto_SemSig_Devolve403()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await UploadAsync(ambiente.Client, ambiente.IdPet, Imagem("jpg", 0x01), Imagem("jpg", 0x11));
        var pet = await ObterPetAsync(ambiente.Client, ambiente.IdPet);

        var query = ExtrairQuery(pet.DsFotoThumbUrl!);
        var caminho = new Uri(pet.DsFotoThumbUrl!).AbsolutePath;
        var urlSemSig = $"{caminho}?exp={query["exp"]}";

        var resposta = await ambiente.Client.GetAsync(urlSemSig);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFoto_ChaveDeOutroPetComSigDeste_Devolve403()
    {
        await using var ambiente = await CriarAmbienteAsync();
        await UploadAsync(ambiente.Client, ambiente.IdPet, Imagem("jpg", 0x01), Imagem("jpg", 0x11));
        var uploadOutro = await UploadAsync(
            ambiente.Client, ambiente.IdOutroPet, Imagem("jpg", 0x21), Imagem("jpg", 0x31));

        var pet = await ObterPetAsync(ambiente.Client, ambiente.IdPet);
        var query = ExtrairQuery(pet.DsFotoThumbUrl!);
        var chaveThumbOutro = ChaveFotoPet.Variante(uploadOutro.DsFotoChave, ChaveFotoPet.SufixoThumb);

        // Chave do OUTRO pet, com exp/sig do PRIMEIRO — a sig não cobre esta chave.
        var url = $"/api/v1/fotos/{chaveThumbOutro}?exp={query["exp"]}&sig={query["sig"]}";

        var resposta = await ambiente.Client.GetAsync(url);
        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFoto_ChaveForaDoPadraoComAssinaturaValida_Devolve403()
    {
        // MORDIDA (registrada no relatório da task): comentar a checagem de PadraoChaveServida
        // em FotosController.Obter faz este teste passar a receber 404 (a assinatura É válida
        // — só a checagem de padrão barra) — provando que a checagem de padrão é o que
        // protege a base de storage compartilhada com o receituário (achado F7-c).
        await using var ambiente = await CriarAmbienteAsync();

        using var escopo = ambiente.Factory.Services.CreateScope();
        var assinador = escopo.ServiceProvider.GetRequiredService<IAssinadorUrlFoto>();
        const string chaveForaDoPadrao = "receituario-1-abc123.pdf";
        var expiraEm = DateTimeOffset.UtcNow.AddHours(1);
        var sig = assinador.Assinar(chaveForaDoPadrao, expiraEm);

        var resposta = await ambiente.Client.GetAsync(
            $"/api/v1/fotos/{chaveForaDoPadrao}?exp={expiraEm.ToUnixTimeSeconds()}&sig={sig}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a chave não bate o padrão fechado clinica/{id}/pet/{id}/..._(256|1080).(webp|jpg|png) " +
            "— mesmo com assinatura genuína, o formato sozinho já reprova (achado F7-c)");
    }

    [Fact]
    public async Task GetFoto_ChaveNoPadraoMasArquivoInexistente_Devolve404()
    {
        await using var ambiente = await CriarAmbienteAsync();

        using var escopo = ambiente.Factory.Services.CreateScope();
        var assinador = escopo.ServiceProvider.GetRequiredService<IAssinadorUrlFoto>();
        var chave = $"clinica/{KuraApiFactory.IdClinicaSemeada}/pet/{ambiente.IdPet}/inexistente_256.webp";
        var expiraEm = DateTimeOffset.UtcNow.AddHours(1);
        var sig = assinador.Assinar(chave, expiraEm);

        var resposta = await ambiente.Client.GetAsync(
            $"/api/v1/fotos/{chave}?exp={expiraEm.ToUnixTimeSeconds()}&sig={sig}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "chave no formato certo e assinatura válida, mas o arquivo nunca foi gravado " +
            "neste Storage:BasePath isolado — 404, não 403 (o cliente já provou ter uma URL genuína)");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Helpers de query string
    // ─────────────────────────────────────────────────────────────────────────────────────

    private static async Task<PetResponseDto> ObterPetAsync(HttpClient client, long idPet)
    {
        var resposta = await client.GetAsync($"/api/v1/pets/{idPet}");
        return (await resposta.Content.ReadFromJsonAsync<PetResponseDto>())!;
    }

    private static Dictionary<string, string> ExtrairQuery(string url) =>
        new Uri(url, UriKind.RelativeOrAbsolute) is { IsAbsoluteUri: true } uri
            ? ParseQuery(uri.Query)
            : ParseQuery(url[url.IndexOf('?')..]);

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(par => par.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => Uri.UnescapeDataString(kv[1]));

    /// <summary>
    /// Troca o PRIMEIRO caractere da <c>sig</c> — de propósito, não o último. Medido nesta
    /// task (achado F3-a do G2, <c>g2-ft01-ft02.md</c>, confirmado aqui): o último caractere
    /// de uma sig base64url de 32 bytes carrega só 4 bits significativos (256 bits / 6 =
    /// 42,67 ⇒ 43º char tem 2 bits "não usados" na decodificação) — trocar SÓ o último char
    /// entre 'A' e 'B' (0x00/0x01, mesmos 4 bits altos) produz os MESMOS bytes decodificados
    /// e a "adulteração" validaria como se nada tivesse mudado. O primeiro caractere cobre um
    /// grupo de 6 bits inteiramente significativo — trocá-lo garante bytes diferentes.
    /// </summary>
    private static string TrocarPrimeiroChar(string valor)
    {
        var primeiro = valor[0];
        var trocado = primeiro == 'A' ? 'B' : 'A';
        return trocado + valor[1..];
    }

    private static string TrocarQuery(string url, string? exp = null, string? sig = null)
    {
        var caminho = new Uri(url, UriKind.RelativeOrAbsolute) is { IsAbsoluteUri: true } uri
            ? uri.AbsolutePath
            : url[..url.IndexOf('?')];
        var query = ExtrairQuery(url);
        if (exp is not null) query["exp"] = exp;
        if (sig is not null) query["sig"] = sig;
        return $"{caminho}?exp={query["exp"]}&sig={query["sig"]}";
    }
}
