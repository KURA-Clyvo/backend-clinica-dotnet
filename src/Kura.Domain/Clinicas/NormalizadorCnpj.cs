namespace Kura.Domain.Clinicas;

/// <summary>
/// S4-FIX-02 (E67): única fonte do formato canônico de CNPJ. A coluna
/// <c>CLINICA.NR_CNPJ</c> (VARCHAR2(18), UNIQUE) guarda sempre a forma mascarada
/// <c>00.000.000/0000-00</c>, qualquer que seja a forma digitada (com ou sem máscara).
/// Não valida dígitos verificadores (decisão deliberada: fixtures e dados existentes usam CNPJs fictícios).
/// </summary>
public static class NormalizadorCnpj
{
    public const int TamanhoDigitos = 14;

    /// <summary>Normaliza para a forma mascarada. Falso se a entrada tem letras/símbolos estranhos ou != 14 dígitos.</summary>
    public static bool TentarNormalizar(string? entrada, out string canonico)
    {
        canonico = string.Empty;
        if (string.IsNullOrWhiteSpace(entrada)) return false;

        var digitos = new char[TamanhoDigitos];
        var n = 0;
        foreach (var c in entrada.Trim())
        {
            if (c is >= '0' and <= '9')
            {
                if (n == TamanhoDigitos) return false;
                digitos[n++] = c;
            }
            else if (c is not ('.' or '/' or '-')) return false; // letras e outros símbolos
        }
        if (n != TamanhoDigitos) return false;

        var d = new string(digitos);
        canonico = $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}";
        return true;
    }

    /// <summary>Apenas os 14 dígitos de um CNPJ já canônico (formato legado gravado pelo antigo ClinicaService).</summary>
    public static string SomenteDigitos(string canonico) =>
        new(canonico.Where(char.IsDigit).ToArray());
}
