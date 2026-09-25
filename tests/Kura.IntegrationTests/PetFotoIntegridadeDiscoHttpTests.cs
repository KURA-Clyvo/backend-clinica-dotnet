namespace Kura.IntegrationTests;

using System.Net;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FT-03 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — fix wave 2 (re-G2, <c>g2b-ft03.md</c>,
/// achado re-G2-5, "corrupção silenciosa").
///
/// <para><b>O que estava sem prova.</b> O mesmo <see cref="Stream"/> de cada parte multipart é
/// lido 3 vezes ao longo do caminho de upload — <c>PetFotoUploadValidator</c> (magic bytes),
/// <c>PetFotoService</c> (defesa em profundidade, magic bytes de novo) e finalmente
/// <c>ArmazenamentoLocalDisco.SalvarAsync</c> (gravação completa) — e isso só funciona porque
/// <c>ValidadorAssinaturaImagem.Detectar</c> reposiciona o stream em <c>Position = 0</c> ao
/// final de CADA chamada seguido. Nenhum teste anterior desta suíte comparava os BYTES gravados
/// no storage com os bytes enviados: os testes de service usam <c>It.IsAny&lt;Stream&gt;()</c>
/// (Moq não olha conteúdo) e os testes HTTP existentes (<c>PetFotoHttpTests</c>) só conferem
/// status/chave, nunca o arquivo físico. Uma remoção acidental daquele reposicionamento (ex.:
/// um refactor de <c>Detectar</c> para <c>Span</c>/<c>PipeReader</c> que "esquece" o reset
/// final) faria toda foto ser gravada SEM os primeiros 12 bytes — com <c>200</c> e a suíte
/// inteira continuando verde.</para>
///
/// <para><b>Por que HTTP + storage real de disco, não mock.</b> O bug só existe na composição
/// real (o MESMO objeto <see cref="Stream"/> atravessando validator → service → storage) — um
/// mock de <see cref="Kura.Domain.Interfaces.IArmazenamentoArquivos"/> nunca vê os bytes de
/// verdade. Usa <see cref="ArmazenamentoLocalDisco"/> real, apontado (via
/// <c>Storage:BasePath</c>) para uma pasta temporária isolada por teste, apagada no
/// <c>finally</c> do próprio teste.</para>
///
/// <para><b>Conteúdos DIFERENTES em thumb e media</b> (achado g2c-2): com bytes iguais nas
/// duas partes, gravar a thumb no lugar da media (e vice-versa) passaria despercebido.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class PetFotoIntegridadeDiscoHttpTests
{
    // JPEG real de 20 bytes: assinatura FF D8 FF E0 + 16 bytes de conteúdo sequencial
    // (0x01..0x10) — grande o suficiente para o cabeçalho de 12 bytes que
    // ValidadorAssinaturaImagem lê NÃO esgotar o arquivo inteiro, então uma gravação que
    // "esquece" os 12 primeiros bytes produz um arquivo mais curto e com cabeçalho diferente,
    // não um arquivo vazio (o que tornaria a comparação menos informativa).
    private static readonly byte[] JpegConhecido =
    [
        0xFF, 0xD8, 0xFF, 0xE0,
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
    ];

    // Mesma assinatura JPEG, conteúdo diferente — é a parte "media" (variante _1080).
    private static readonly byte[] JpegConhecidoMedia =
    [
        0xFF, 0xD8, 0xFF, 0xE0,
        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20,
    ];

    /// <summary>
    /// MORDIDA obrigatória (re-G2-5): apagar <c>ValidadorAssinaturaImagem.cs:80</c>
    /// (<c>stream.Position = 0;</c> ao final de <c>Detectar</c>) faz este teste FALHAR — o
    /// arquivo gravado no disco fica truncado (faltam os 12 primeiros bytes), com <c>200</c>
    /// e o resto da suíte permanecendo verde. Medido nesta fix wave: sem a mordida, os 2
    /// arquivos batem byte a byte com o enviado; com a mordida
    /// (<c>dotnet test --filter PetFotoIntegridadeDiscoHttpTests</c>), <c>EXIT=1</c>, 1 falha
    /// nominal — restaurado depois.
    /// </summary>
    [Fact]
    public async Task UploadFoto_ImagemValida_ArquivoGravadoNoDiscoEhIgualAoEnviado()
    {
        var diretorioTemporario = Path.Combine(Path.GetTempPath(), "ft03-g2b-" + Guid.NewGuid().ToString("N"));

        var factoryBase = new KuraApiFactory();
        await using var factory = factoryBase.WithWebHostBuilder(
            builder => builder.UseSetting("Storage:BasePath", diretorioTemporario));

        long idPet;
        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            var pet = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Rex Integridade G2b",
                DtNascimento = new DateTime(2022, 1, 1),
                SgSexo = 'M',
                SgPorte = 'M',
            };
            db.Pets.Add(pet);
            db.SaveChanges();
            idPet = pet.Id;
        }

        try
        {
            var client = factory.CreateClient();
            client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

            var conteudo = new MultipartFormDataContent();
            conteudo.Add(new ByteArrayContent(JpegConhecido), "thumb", "t.jpg");
            conteudo.Add(new ByteArrayContent(JpegConhecidoMedia), "media", "m.jpg");

            var resposta = await client.PostAsync($"/api/v1/pets/{idPet}/foto", conteudo);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            var arquivosGravados = Directory.Exists(diretorioTemporario)
                ? Directory.GetFiles(diretorioTemporario, "*", SearchOption.AllDirectories)
                : [];

            arquivosGravados.Should().HaveCount(2,
                "o upload grava 2 variantes (thumb + media) — se os arquivos não existem, o " +
                "resto desta asserção não testaria nada");

            foreach (var caminho in arquivosGravados)
            {
                var bytesGravados = await File.ReadAllBytesAsync(caminho);
                var esperado = Path.GetFileNameWithoutExtension(caminho).EndsWith("_256")
                    ? JpegConhecido
                    : JpegConhecidoMedia;
                bytesGravados.Should().Equal(esperado,
                    $"'{Path.GetFileName(caminho)}' tem de ser BYTE A BYTE igual ao que foi " +
                    "enviado — um stream que perde os primeiros bytes na gravação (ex.: reset " +
                    "de Position ausente) produziria um arquivo mais curto, sem o cabeçalho " +
                    "JPEG, e ainda assim um 200");
            }
        }
        finally
        {
            if (Directory.Exists(diretorioTemporario))
                Directory.Delete(diretorioTemporario, recursive: true);
        }
    }
}
