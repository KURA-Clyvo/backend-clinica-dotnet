namespace Kura.Domain.Tutores;

/// <summary>
/// Fórmula ÚNICA de normalização de telefone de tutor — backlog
/// <c>KURA_BACKLOG_RECEPCAO.md</c>, REC-01, a partir da medição do G0 (<c>g0-recepcao.md</c>,
/// item 4): a Twilio entrega o remetente do WhatsApp como <c>55</c> + DDD + 9 dígitos (13
/// dígitos, sem <c>+</c>) e <see cref="Kura.Domain.Interfaces.ITutorRepository.GetByTelefoneAsync"/>
/// busca por IGUALDADE EXATA contra <c>TUTOR.DS_TELEFONE</c> — sem esta normalização no
/// cadastro/edição, nenhum tutor casa com o formato real.
///
/// <para><b>Sem dependência de ASP.NET</b> de propósito (mora em <c>Kura.Domain</c>): usado pelo
/// <c>TutorCreateValidator</c>/<c>TutorUpdateValidator</c> (Application, para produzir 400 antes
/// de qualquer escrita) E pelo <c>TutorService</c> (para decidir o valor persistido) — um só
/// lugar decide o que é um telefone válido e como ele fica armazenado.</para>
///
/// <para><b>Regra</b> (ordem importa — cada ramo abaixo só é avaliado se o anterior não bateu):
/// <list type="number">
/// <item><description>Entrada começa com <c>+</c> (DDI explícito, qualquer país) → armazena só os
/// dígitos, sem o <c>+</c>.</description></item>
/// <item><description>Dígitos têm 12 ou 13 posições E começam com <c>55</c> (já tem DDI Brasil,
/// sem <c>+</c>) → armazena como veio.</description></item>
/// <item><description>Dígitos têm 10 ou 11 posições (nacional, com ou sem o 9º dígito) →
/// armazena <c>"55" + dígitos</c>.</description></item>
/// <item><description>Nenhuma das anteriores → inválido. NUNCA grava lixo: quem chama decide o
/// efeito (400 no validator, defesa em profundidade no service).</description></item>
/// </list></para>
///
/// <para><b>Idempotência — obrigatória para o caso doméstico, NÃO garantida para estrangeiro
/// sem o <c>+</c> preservado</b> (ressalva já registrada no G0, item 4, linha 6 da tabela):
/// alimentar de novo o valor ARMAZENADO de um número brasileiro (12/13 dígitos iniciando em
/// <c>55</c>) sempre bate no ramo 2 e devolve o mesmo valor — é o caso que importa de verdade
/// (o <c>PUT</c> do <c>seed-demo-luna.sh</c> já manda <c>55…</c>, e o cadastro pode reenviar o
/// valor que ele mesmo gravou). Para um número estrangeiro (ramo 1), o valor armazenado NUNCA
/// carrega o <c>+</c> de volta — se ele tiver 10/11 dígitos, uma segunda passada o leria como
/// nacional brasileiro. Isto é uma limitação DECLARADA (não testada como idempotente), porque a
/// tela sempre tem o <c>+</c>/DDI explícito na entrada do usuário, nunca no valor já
/// armazenado.</para>
/// </summary>
public static class NormalizadorTelefone
{
    /// <summary>
    /// Tenta normalizar <paramref name="entrada"/> para o formato que
    /// <c>TUTOR.DS_TELEFONE</c>/<c>DS_WHATSAPP</c> armazenam (só dígitos, sempre com DDI).
    /// Devolve <c>false</c> — e <paramref name="digitosArmazenados"/> vazio — quando a entrada
    /// não se encaixa em nenhuma das formas conhecidas (nunca inventa/trunca dígitos).
    /// </summary>
    public static bool TentarNormalizar(string? entrada, out string digitosArmazenados)
    {
        digitosArmazenados = string.Empty;

        if (string.IsNullOrWhiteSpace(entrada))
            return false;

        var comDdiExplicito = entrada.TrimStart().StartsWith('+');

        var digitos = ExtrairDigitos(entrada);
        if (digitos.Length == 0)
            return false;

        // 1) DDI explícito (`+`) — qualquer país, armazena os dígitos sem o `+`.
        if (comDdiExplicito)
        {
            digitosArmazenados = digitos;
            return true;
        }

        // 2) Já tem DDI do Brasil embutido, sem `+` (ex.: o que a Twilio entrega).
        if ((digitos.Length == 12 || digitos.Length == 13) && digitos.StartsWith("55", StringComparison.Ordinal))
        {
            digitosArmazenados = digitos;
            return true;
        }

        // 3) Nacional (DDD + 8 ou 9 dígitos, sem DDI) — assume Brasil.
        if (digitos.Length == 10 || digitos.Length == 11)
        {
            digitosArmazenados = "55" + digitos;
            return true;
        }

        // 4) Nenhuma forma reconhecida — inválido.
        return false;
    }

    /// <summary>
    /// Converte o valor ARMAZENADO (só dígitos, sem <c>+</c>) para E.164 (<c>+</c> + dígitos),
    /// formato gravado em <c>TUTOR.DS_WHATSAPP</c>.
    /// </summary>
    public static string ParaE164(string digitosArmazenados) => "+" + digitosArmazenados;

    private static string ExtrairDigitos(string entrada)
    {
        var buffer = new char[entrada.Length];
        var tamanho = 0;
        foreach (var c in entrada)
        {
            if (c is >= '0' and <= '9')
                buffer[tamanho++] = c;
        }
        return new string(buffer, 0, tamanho);
    }
}
