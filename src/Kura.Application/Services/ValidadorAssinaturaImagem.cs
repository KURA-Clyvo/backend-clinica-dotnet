namespace Kura.Application.Services;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Detecta o formato REAL de uma imagem pelos primeiros bytes do arquivo (magic bytes) —
/// nunca por <c>Content-Type</c> ou extensão informados pelo cliente, que são declaração,
/// não fato (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>, "Validação no servidor"; FT-03).
///
/// <para>Usado em dois pontos que precisam concordar: <c>PetFotoUploadValidator</c> (decide
/// 400 quando a parte não é um JPEG/PNG/WebP válido) e <see cref="Kura.Application.Services.PetFotoService"/>
/// (decide a extensão do arquivo salvo — <see cref="Formato"/> vira sufixo da chave).</para>
/// </summary>
public static class ValidadorAssinaturaImagem
{
    public enum Formato
    {
        Jpeg,
        Png,
        WebP,
    }

    // JPEG: FF D8 FF (SOI marker + início do próximo marker, sempre presente).
    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];

    // PNG: assinatura de 8 bytes do formato, aqui só os 4 primeiros já bastam para diferenciar
    // de qualquer outro formato suportado.
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47];

    // WebP: container RIFF — bytes 0-3 "RIFF", bytes 4-7 são o tamanho do arquivo (variável,
    // não fazem parte da assinatura), bytes 8-11 "WEBP".
    private static readonly byte[] AssinaturaRiff = "RIFF"u8.ToArray();
    private static readonly byte[] AssinaturaWebp = "WEBP"u8.ToArray();

    private const int TamanhoCabecalhoNecessario = 12; // cobre até o caso WebP (0-11)

    /// <summary>
    /// Lê só o cabeçalho do arquivo (até 12 bytes) e devolve o formato detectado, ou
    /// <see langword="null"/> se o arquivo é nulo, vazio, ou os bytes não batem nenhuma das
    /// 3 assinaturas suportadas. <see cref="IFormFile.OpenReadStream"/> devolve, a cada
    /// chamada, um stream posicionado no início do CONTEÚDO daquela parte (não é
    /// cumulativo/stateful entre chamadas) — chamar de novo depois, para copiar o arquivo
    /// inteiro, é seguro e começa do byte 0 de novo.
    ///
    /// <para><b>SÍNCRONO de propósito, medido:</b> o pipeline de auto-validation do
    /// FluentValidation (<c>AddFluentValidationAutoValidation()</c>) invoca os validadores de
    /// forma SÍNCRONA — uma primeira versão desta task usava <c>MustAsync</c>/
    /// <c>ReadAsync</c> e todo request de foto devolvia <c>500</c> com
    /// <c>AsyncValidatorInvokedSynchronouslyException</c> ("Validator can't be used with
    /// ASP.NET automatic validation as it contains asynchronous rules"), medido com teste
    /// HTTP real. O arquivo já está inteiro em memória/disco local (multipart de até 2 MB,
    /// <see cref="IFormFile"/>) quando chega aqui — ler alguns bytes de um stream já
    /// materializado não bloqueia thread de forma relevante nesta escala.</para>
    /// </summary>
    public static Formato? Detectar(IFormFile? arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
            return null;

        var tamanhoCabecalho = (int)Math.Min(TamanhoCabecalhoNecessario, arquivo.Length);
        var cabecalho = new byte[tamanhoCabecalho];

        using var stream = arquivo.OpenReadStream();
        var totalLido = 0;
        while (totalLido < tamanhoCabecalho)
        {
            var lidos = stream.Read(cabecalho, totalLido, tamanhoCabecalho - totalLido);
            if (lidos == 0)
                break; // EOF antes do esperado — não deveria acontecer com Length correto, mas não trava
            totalLido += lidos;
        }

        return DetectarPorCabecalho(cabecalho.AsSpan(0, totalLido));
    }

    private static Formato? DetectarPorCabecalho(ReadOnlySpan<byte> cabecalho)
    {
        if (ComecaCom(cabecalho, AssinaturaJpeg))
            return Formato.Jpeg;

        if (ComecaCom(cabecalho, AssinaturaPng))
            return Formato.Png;

        if (cabecalho.Length >= TamanhoCabecalhoNecessario
            && ComecaCom(cabecalho, AssinaturaRiff)
            && cabecalho.Slice(8, 4).SequenceEqual(AssinaturaWebp))
            return Formato.WebP;

        return null;
    }

    private static bool ComecaCom(ReadOnlySpan<byte> cabecalho, byte[] assinatura) =>
        cabecalho.Length >= assinatura.Length && cabecalho[..assinatura.Length].SequenceEqual(assinatura);

    /// <summary>Extensão de arquivo (sem ponto) correspondente ao formato detectado.</summary>
    public static string ExtensaoPara(Formato formato) => formato switch
    {
        Formato.Jpeg => "jpg",
        Formato.Png => "png",
        Formato.WebP => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de imagem desconhecido."),
    };

    /// <summary>Content-Type MIME correspondente ao formato detectado.</summary>
    public static string ContentTypePara(Formato formato) => formato switch
    {
        Formato.Jpeg => "image/jpeg",
        Formato.Png => "image/png",
        Formato.WebP => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de imagem desconhecido."),
    };
}
