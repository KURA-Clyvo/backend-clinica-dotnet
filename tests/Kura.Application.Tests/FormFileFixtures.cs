namespace Kura.Application.Tests;

/// <summary>
/// Bytes de amostra para os testes de upload de foto (FT-03, backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>). Cada assinatura tem só o mínimo de bytes necessário para
/// <c>ValidadorAssinaturaImagem</c> reconhecer o formato — não são imagens de verdade (não
/// decodificam), só o suficiente para os primeiros bytes baterem.
///
/// <para>🔴 <b>Fix wave G2 (g2-ft03.md, achado G2-e):</b> deixou de fabricar
/// <c>IFormFile</c> — <c>Kura.Application</c> (e o que ela expõe, incluindo
/// <c>PetFotoUploadDto</c>/<c>IPetFotoService</c>/<c>ValidadorAssinaturaImagem</c>) passou a
/// trabalhar só com <see cref="Stream"/> puro, sem depender de
/// <c>Microsoft.AspNetCore.Http</c>. Os testes fabricam <see cref="Stream"/> diretamente.</para>
/// </summary>
internal static class FormFileFixtures
{
    // JPEG: FF D8 FF + alguns bytes de recheio.
    public static readonly byte[] JpegValido = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    // PNG: assinatura de 8 bytes completa.
    public static readonly byte[] PngValido = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // WebP: "RIFF" + 4 bytes de tamanho (irrelevantes) + "WEBP".
    public static readonly byte[] WebpValido =
        [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray()];

    // Bytes que NÃO batem nenhuma das 3 assinaturas — usado com Content-Type: image/jpeg
    // mentiroso no teste da mordida 2 (aceite FT-03).
    public static readonly byte[] BytesInvalidos = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07];

    public static Stream CriarStream(byte[] conteudo) => new MemoryStream(conteudo);
}
