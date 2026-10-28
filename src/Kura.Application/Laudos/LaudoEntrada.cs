namespace Kura.Application.Laudos;

using System.Globalization;
using System.Text.Json;
using Kura.Domain.Laudos;

/// <summary>
/// Interpretação dos campos de texto do multipart de laudo. Uma única regra, usada pelo
/// validador (para dizer 400) e pelo service (para gravar) — nada de duas leituras que podem divergir.
/// </summary>
public static class LaudoEntrada
{
    /// <summary>Teto do JSON de <c>dados</c> (decisão desta task; o contrato deixa o conteúdo livre).</summary>
    public const int MaxCaracteresDados = 32 * 1024;

    public static bool TentarTipo(string? texto, out TipoLaudo tipo)
    {
        // Enum.TryParse aceita número ("1") e lista ("A,B"): a lista branca é só o nome exato.
        tipo = default;
        return !string.IsNullOrWhiteSpace(texto)
            && Enum.GetNames<TipoLaudo>().Contains(texto.Trim(), StringComparer.OrdinalIgnoreCase)
            && Enum.TryParse(texto.Trim(), ignoreCase: true, out tipo);
    }

    /// <summary>
    /// Data/instante ISO-8601. Sem offset é tratado como UTC (nunca como o fuso do servidor); com
    /// offset é convertido para UTC. Devolve <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    public static bool TentarData(string? texto, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        if (!DateTimeOffset.TryParse(
                texto.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return false;
        utc = DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Utc);
        return true;
    }

    public static bool TentarIdEvento(string? texto, out long? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(texto)) return true; // opcional
        if (!long.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v <= 0)
            return false;
        id = v;
        return true;
    }

    /// <summary>
    /// <c>dados</c>: vazio vira <c>{}</c>; senão tem de ser um OBJETO JSON válido (array, número e
    /// texto soltos são recusados). Converte para tipos .NET que o repositório aceita (G2-m3 da
    /// S4-NET-02: <see cref="JsonElement"/> dentro de <c>dados</c> dava <c>ArgumentException</c>).
    /// </summary>
    public static bool TentarDados(string? json, out Dictionary<string, object?> dados)
    {
        dados = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(json)) return true;
        if (json.Length > MaxCaracteresDados) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            dados = ConverterObjeto(doc.RootElement);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or DadosInvalidosException)
        {
            return false;
        }
    }

    /// <summary>Valor ou chave que o JSON aceita mas o BSON/serializador de resposta não (G2-I1).</summary>
    private sealed class DadosInvalidosException(string mensagem) : Exception(mensagem);

    private static Dictionary<string, object?> ConverterObjeto(JsonElement objeto)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in objeto.EnumerateObject())
        {
            // G2-I1: o driver BSON recusa NUL em nome de elemento com ArgumentException crua (500).
            // '$', '.' e chave vazia o mongod 8 aceita e devolve (sondado pela G2), então ficam.
            if (p.Name.Contains((char)0)) throw new DadosInvalidosException("Chave com NUL.");
            d[p.Name] = Converter(p.Value); // chave repetida: vale a última, como no parser do JSON
        }
        return d;
    }

    private static object? Converter(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => ConverterObjeto(e),
        JsonValueKind.Array => e.EnumerateArray().Select(Converter).ToList(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : NumeroFinito(e.GetDouble()),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    // G2-I1: 1e400 vira ±Infinity no double; o Mongo guarda e o System.Text.Json não serializa de
    // volta, o que envenenava a lista de laudos do pet (500 em todo GET).
    private static double NumeroFinito(double v) =>
        double.IsFinite(v) ? v : throw new DadosInvalidosException("Número não finito.");
}
