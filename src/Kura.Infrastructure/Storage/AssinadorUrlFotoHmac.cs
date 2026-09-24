namespace Kura.Infrastructure.Storage;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kura.Domain.Interfaces;

/// <summary>
/// Implementação HMAC-SHA256 de <see cref="IAssinadorUrlFoto"/>. Algoritmo fixo — ver XML doc
/// da interface para a fórmula exata que o lado Java (FT-05) replica byte a byte.
///
/// Comparação em tempo constante via <see cref="CryptographicOperations.FixedTimeEquals"/>,
/// mesmo padrão de <c>Kura.Api.Filters.ApiKeyComparer</c> (TASK-86) — protege contra timing
/// attack sobre o CONTEÚDO da assinatura. Igual aquele helper, não é constant-time quanto ao
/// TAMANHO dos spans comparados (limitação aceitável pelo mesmo motivo: o tamanho da
/// assinatura HMAC-SHA256 é sempre fixo — 32 bytes antes de base64url — então essa dimensão
/// nunca varia por segredo/entrada, e não há nada ali para vazar por timing).
/// </summary>
public sealed class AssinadorUrlFotoHmac : IAssinadorUrlFoto
{
    private readonly byte[] _segredoBytes;

    public AssinadorUrlFotoHmac(string segredo)
    {
        if (string.IsNullOrEmpty(segredo))
            throw new ArgumentException("Segredo de assinatura de URL não pode ser vazio.", nameof(segredo));

        _segredoBytes = Encoding.UTF8.GetBytes(segredo);

        // Regra do backlog (FT-02): segredo curto degrada a segurança do HMAC-SHA256 —
        // validado aqui (não só na config), para que qualquer código que construa este
        // tipo diretamente (produção ou teste) não consiga furar a checagem passando por
        // fora do registro de DI.
        if (_segredoBytes.Length < 32)
            throw new ArgumentException(
                $"Segredo de assinatura de URL deve ter ao menos 32 bytes (UTF-8); recebeu {_segredoBytes.Length}.",
                nameof(segredo));
    }

    public string Assinar(string chave, DateTimeOffset expiraEm)
    {
        ArgumentException.ThrowIfNullOrEmpty(chave);

        var exp = expiraEm.ToUnixTimeSeconds();
        var mac = CalcularMac(chave, exp);
        return Base64UrlSemPadding(mac);
    }

    public bool Validar(string chave, long exp, string sig, DateTimeOffset agora)
    {
        if (string.IsNullOrEmpty(chave) || string.IsNullOrEmpty(sig))
            return false;

        if (exp < agora.ToUnixTimeSeconds())
            return false;

        var macEsperado = CalcularMac(chave, exp);

        byte[] sigFornecida;
        try
        {
            sigFornecida = Base64UrlDecode(sig);
        }
        catch (FormatException)
        {
            // sig malformada (não é base64url válido) — não é assinatura válida, mas
            // também não é um erro do chamador que mereça exceção: mesma classe de
            // "entrada não confiável", tratada como "false", não como exceção.
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(sigFornecida, macEsperado);
    }

    private byte[] CalcularMac(string chave, long exp)
    {
        var mensagem = chave + "\n" + exp.ToString(CultureInfo.InvariantCulture);
        var mensagemBytes = Encoding.UTF8.GetBytes(mensagem);

        using var hmac = new HMACSHA256(_segredoBytes);
        return hmac.ComputeHash(mensagemBytes);
    }

    private static string Base64UrlSemPadding(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string valor)
    {
        var base64 = valor.Replace('-', '+').Replace('_', '/');
        var resto = base64.Length % 4;
        if (resto == 2) base64 += "==";
        else if (resto == 3) base64 += "=";
        else if (resto == 1) throw new FormatException("Base64url inválido (tamanho incompatível).");

        return Convert.FromBase64String(base64);
    }
}
