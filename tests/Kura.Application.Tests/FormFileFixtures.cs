namespace Kura.Application.Tests;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Bytes de amostra e fábrica de <see cref="IFormFile"/> para os testes de upload de foto
/// (FT-03, backlog <c>KURA_BACKLOG_FOTO_PET.md</c>). Cada assinatura tem só o mínimo de
/// bytes necessário para <c>ValidadorAssinaturaImagem</c> reconhecer o formato — não são
/// imagens de verdade (não decodificam), só o suficiente para os primeiros bytes baterem.
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

    public static IFormFile CriarArquivo(byte[] conteudo, string nomeParte, string contentType, string nomeArquivo = "arquivo")
    {
        var stream = new MemoryStream(conteudo);
        var arquivo = new FormFile(stream, 0, conteudo.Length, nomeParte, nomeArquivo)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
        return arquivo;
    }
}
