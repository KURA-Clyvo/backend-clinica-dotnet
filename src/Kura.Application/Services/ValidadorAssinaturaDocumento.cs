namespace Kura.Application.Services;

/// <summary>
/// Detecta o formato REAL de um documento de laudo pelos primeiros bytes (magic bytes): PDF
/// (<c>%PDF-</c>) ou uma das imagens já reconhecidas por <see cref="ValidadorAssinaturaImagem"/>.
/// Nunca olha extensão nem <c>Content-Type</c> do cliente (S4-NET-03, D-NET-1 do G0).
///
/// <para>A regra de imagem NÃO é duplicada: este tipo delega a <see cref="ValidadorAssinaturaImagem.Detectar"/>
/// e só acrescenta o PDF. O <c>Content-Type</c> gravado vem sempre daqui.</para>
/// </summary>
public static class ValidadorAssinaturaDocumento
{
    public enum Formato
    {
        Pdf,
        Jpeg,
        Png,
        WebP,
    }

    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();

    /// <summary>Content-Types que um laudo pode ter (mapa fechado do contrato v1, G0 §5).</summary>
    public static readonly IReadOnlySet<string> ContentTypesPermitidos = new HashSet<string>
    {
        "application/pdf", "image/jpeg", "image/png", "image/webp",
    };

    /// <summary>Formato detectado, ou <see langword="null"/>. Deixa o stream em 0.</summary>
    public static Formato? Detectar(Stream? stream)
    {
        if (stream is null || stream.Length == 0)
            return null;

        if (ComecaComPdf(stream))
            return Formato.Pdf;

        return ValidadorAssinaturaImagem.Detectar(stream) switch
        {
            ValidadorAssinaturaImagem.Formato.Jpeg => Formato.Jpeg,
            ValidadorAssinaturaImagem.Formato.Png => Formato.Png,
            ValidadorAssinaturaImagem.Formato.WebP => Formato.WebP,
            _ => null,
        };
    }

    private static bool ComecaComPdf(Stream stream)
    {
        if (stream.Length < AssinaturaPdf.Length)
            return false;

        var cabecalho = new byte[AssinaturaPdf.Length];
        stream.Position = 0;
        var lidos = 0;
        while (lidos < cabecalho.Length)
        {
            var n = stream.Read(cabecalho, lidos, cabecalho.Length - lidos);
            if (n == 0) break;
            lidos += n;
        }
        stream.Position = 0;
        return lidos == cabecalho.Length && cabecalho.AsSpan().SequenceEqual(AssinaturaPdf);
    }

    public static string ExtensaoPara(Formato formato) => formato switch
    {
        Formato.Pdf => "pdf",
        Formato.Jpeg => "jpg",
        Formato.Png => "png",
        Formato.WebP => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de documento desconhecido."),
    };

    public static string ContentTypePara(Formato formato) => formato switch
    {
        Formato.Pdf => "application/pdf",
        Formato.Jpeg => "image/jpeg",
        Formato.Png => "image/png",
        Formato.WebP => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de documento desconhecido."),
    };

    /// <summary>Extensão do arquivo a partir do Content-Type gravado (mapa fechado).</summary>
    public static string ExtensaoPorContentType(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "bin",
    };
}
