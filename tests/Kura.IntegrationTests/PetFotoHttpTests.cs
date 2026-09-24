namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kura.Application.DTOs.Pet;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FT-03 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — <c>POST /api/v1/pets/{id}/foto</c>
/// exercitado por ROTA HTTP real, com o <c>Program.cs</c> real de pé.
///
/// <para><b>Por que <c>IClassFixture&lt;KuraApiFactory&gt;</c> PRÓPRIA, não a collection
/// compartilhada.</b> <c>KuraApiFactory.Semear</c> não semeia <c>Especie</c>/<c>Raca</c>/
/// <c>Pet</c> — só Clinica/Veterinario/UsuarioClinica/etc. Em vez de editar
/// <c>KuraApiFactory.cs</c> (regra do brief: só editar se necessário), esta classe semeia os
/// 2 pets que precisa (um por tenant, para o aceite 3 — 404 cross-tenant) direto no
/// construtor, via uma instância PRÓPRIA da fábrica — sem risco de colidir com o que outras
/// classes de teste esperam do banco compartilhado.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class PetFotoHttpTests : IClassFixture<KuraApiFactory>
{
    private readonly KuraApiFactory _factory;
    private readonly long _idPetSemeado;
    private readonly long _idPetOutroTenant;

    // Mesmas assinaturas de FormFileFixtures (Kura.Application.Tests) — duplicadas aqui de
    // propósito: são 2 assemblies de teste diferentes, sem ProjectReference entre eles, e o
    // conteúdo é trivial (poucos bytes fixos).
    private static readonly byte[] JpegValido = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];
    private static readonly byte[] PngValido = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] WebpValido =
        [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray()];
    private static readonly byte[] BytesInvalidos = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    public PetFotoHttpTests(KuraApiFactory factory)
    {
        _factory = factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KuraDbContext>();

        // Idempotente: xUnit pode reconstruir a classe entre [Fact]s desta mesma fixture
        // (mesma instância de KuraApiFactory, mesmo banco InMemory) — sem a guarda, a 2ª
        // instanciação duplicaria os pets.
        var petSemeado = db.Pets.IgnoreQueryFilters()
            .FirstOrDefault(p => p.IdClinica == KuraApiFactory.IdClinicaSemeada && p.NmPet == "Rex FT-03");
        if (petSemeado is null)
        {
            petSemeado = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Rex FT-03",
                DtNascimento = new DateTime(2022, 1, 1),
                SgSexo = 'M',
                SgPorte = 'M',
            };
            db.Pets.Add(petSemeado);
        }

        var petOutroTenant = db.Pets.IgnoreQueryFilters()
            .FirstOrDefault(p => p.IdClinica == KuraApiFactory.IdClinicaOutroTenant && p.NmPet == "Fido Outro Tenant");
        if (petOutroTenant is null)
        {
            petOutroTenant = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaOutroTenant,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Fido Outro Tenant",
                DtNascimento = new DateTime(2021, 1, 1),
                SgSexo = 'F',
                SgPorte = 'P',
            };
            db.Pets.Add(petOutroTenant);
        }

        db.SaveChanges();
        _idPetSemeado = petSemeado.Id;
        _idPetOutroTenant = petOutroTenant.Id;
    }

    private async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var client = _factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));
        return client;
    }

    private static MultipartFormDataContent MontarCorpo(
        byte[] bytesThumb, byte[] bytesMedia, string contentTypeThumb = "application/octet-stream",
        string contentTypeMedia = "application/octet-stream")
    {
        var conteudo = new MultipartFormDataContent();

        var parteThumb = new ByteArrayContent(bytesThumb);
        parteThumb.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentTypeThumb);
        conteudo.Add(parteThumb, "thumb", "thumb.bin");

        var parteMedia = new ByteArrayContent(bytesMedia);
        parteMedia.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentTypeMedia);
        conteudo.Add(parteMedia, "media", "media.bin");

        return conteudo;
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite 1: WebP/JPEG/PNG válidos → 200, linha atualizada
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("webp")]
    [InlineData("jpg")]
    [InlineData("png")]
    public async Task UploadFoto_ImagemValida_Devolve200EAtualizaChave(string extensaoEsperada)
    {
        var bytes = extensaoEsperada switch
        {
            "jpg" => JpegValido,
            "png" => PngValido,
            _ => WebpValido,
        };

        var client = await ClienteAutenticadoAsync();
        var resposta = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto", MontarCorpo(bytes, bytes));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = await resposta.Content.ReadFromJsonAsync<PetFotoResponseDto>();
        corpo!.DsFotoChave.Should().EndWith($".{extensaoEsperada}");
        corpo.DsFotoChave.Should().StartWith($"clinica/{KuraApiFactory.IdClinicaSemeada}/pet/{_idPetSemeado}/");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite 2: bytes que não batem a assinatura, com Content-Type mentiroso → 400
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFoto_BytesInvalidosComContentTypeJpegMentiroso_Devolve400()
    {
        var client = await ClienteAutenticadoAsync();
        var resposta = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto",
            MontarCorpo(BytesInvalidos, WebpValido, contentTypeThumb: "image/jpeg"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "os bytes de 'thumb' não batem nenhuma assinatura conhecida, mesmo com " +
            "Content-Type: image/jpeg — a validação é por magic bytes, não pelo header do cliente");
    }

    [Fact]
    public async Task UploadFoto_PartesDeFormatosDiferentes_Devolve400()
    {
        // Ruling F7-a do maestro: thumb JPEG + media PNG, ambos individualmente válidos,
        // mas de formatos diferentes entre si.
        var client = await ClienteAutenticadoAsync();
        var resposta = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto",
            MontarCorpo(JpegValido, PngValido, "image/jpeg", "image/png"));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadFoto_ParteThumbAusente_Devolve400()
    {
        var client = await ClienteAutenticadoAsync();
        var conteudo = new MultipartFormDataContent();
        var parteMedia = new ByteArrayContent(WebpValido);
        parteMedia.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        conteudo.Add(parteMedia, "media", "media.bin");

        var resposta = await client.PostAsync($"/api/v1/pets/{_idPetSemeado}/foto", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite 3: pet de OUTRA clínica → 404 (setup com duas clínicas)
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFoto_PetDeOutraClinica_Devolve404()
    {
        // Login na clínica SEMEADA, upload num pet que pertence à clínica OUTRO TENANT.
        // Sem a isca (pet real na outra clínica), este teste seria vácuo — ver o comentário
        // de KuraApiFactory sobre a mesma classe de armadilha.
        var client = await ClienteAutenticadoAsync();
        var resposta = await client.PostAsync(
            $"/api/v1/pets/{_idPetOutroTenant}/foto", MontarCorpo(WebpValido, WebpValido));

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Pet está entre as 8 entidades do filtro de tenant (ApplyTenantFilters) — pet de " +
            "outra clínica não é encontrado, 404 genuíno");
    }

    [Fact]
    public async Task UploadFoto_PetInexistente_Devolve404()
    {
        var client = await ClienteAutenticadoAsync();
        var resposta = await client.PostAsync(
            "/api/v1/pets/999999/foto", MontarCorpo(WebpValido, WebpValido));

        var corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, corpo);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite 4: corpo acima do limite (2 MB) → 413 — MEDIDO, não presumido (ver o relatório
    // da task para o que ficou declarado ao G4 se este teste não conseguir provar 413 aqui).
    // ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aceite 4 da FT-03 (413) — armadilha (a) do brief CONFIRMADA, não presumida: sob
    /// <c>WebApplicationFactory</c>/<c>TestServer</c>, <c>[RequestSizeLimit]</c> NÃO É
    /// APLICADO. Payload de 3 MB (limite declarado é 2 MB) com assinatura WebP VÁLIDA no
    /// início (só o cabeçalho importa para <c>ValidadorAssinaturaImagem</c> — o resto é
    /// preenchimento de zeros, isolando o teste de "não é imagem válida" para medir só o
    /// efeito do tamanho) foi ACEITO com <c>200</c> e a foto foi gravada de verdade.
    ///
    /// <para>🔴 <b>Isto não é uma falha desta task — é a medição que o próprio brief pediu.</b>
    /// <c>TestServer</c> não implementa <c>IHttpMaxRequestBodySizeFeature</c> da mesma forma
    /// que o Kestrel real (o corpo inteiro já está em memória via <c>HttpClient</c> in-process,
    /// não chega por streaming de socket TCP). A prova de que o ATRIBUTO está de fato presente
    /// e configurado com o valor certo é <see cref="RequestSizeLimitReflectionTests"/> (por
    /// reflection, independente de runtime); a prova de que o middleware SABERIA converter o
    /// <c>BadHttpRequestException</c> real do Kestrel em <c>413</c> (em vez de <c>500</c>) é
    /// <c>ExceptionHandlerMiddlewareTamanhoTests</c> (Kura.Infrastructure.Tests). <b>O que fica
    /// declarado ao G4 (compose real, Kestrel real, ver ft-03-report.md):</b> confirmar que um
    /// upload de foto acima de 2 MB contra o `.NET` do compose devolve 413 de verdade — nenhum
    /// instrumento deste xUnit consegue provar isso, por construção do TestServer.</para>
    /// </summary>
    [Fact]
    public async Task UploadFoto_CorpoAcimaDoLimite_SobTestServerNaoEhRejeitado_MedidoNaoPresumido()
    {
        var payloadGrande = new byte[3 * 1024 * 1024];
        WebpValido.CopyTo(payloadGrande, 0);
        var client = await ClienteAutenticadoAsync();

        var resposta = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto", MontarCorpo(payloadGrande, WebpValido));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            "MEDIDO: TestServer não impõe IHttpMaxRequestBodySizeFeature como o Kestrel real — " +
            "este teste documenta a limitação do harness, não afirma que a produção aceita 3 MB");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────
    // Aceite 5: troca de foto — a 2ª chamada bem-sucedida SUBSTITUI a chave (prova indireta
    // de que a exclusão do antigo roda; a mordida "exclusão antes do commit" é provada em
    // PetFotoServiceTests, com mock de IArmazenamentoArquivos, que consegue isolar a ORDEM
    // das chamadas — InMemory EF não falha o SaveChanges de propósito para simular isso).
    // ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFoto_SegundaChamada_TrocaAChaveENaoAcumula()
    {
        var client = await ClienteAutenticadoAsync();

        var primeira = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto", MontarCorpo(WebpValido, WebpValido));
        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpoPrimeira = await primeira.Content.ReadFromJsonAsync<PetFotoResponseDto>();

        var segunda = await client.PostAsync(
            $"/api/v1/pets/{_idPetSemeado}/foto", MontarCorpo(JpegValido, JpegValido));
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpoSegunda = await segunda.Content.ReadFromJsonAsync<PetFotoResponseDto>();

        corpoSegunda!.DsFotoChave.Should().NotBe(corpoPrimeira!.DsFotoChave,
            "cada upload gera um UUID novo — nunca reaproveita o nome do arquivo anterior");
        corpoSegunda.DsFotoChave.Should().EndWith(".jpg");
    }
}
