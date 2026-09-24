namespace Kura.Domain.Interfaces;

/// <summary>
/// Assina e valida URL de acesso a foto de pet com validade — responsabilidade separada de
/// <see cref="IArmazenamentoArquivos"/> de propósito (SOLID: armazenar bytes e provar posse
/// de uma URL temporária são preocupações diferentes). FT-02/backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>, regra A6.
///
/// <para><b>Algoritmo FIXO e documentado — não mudar sem coordenar com o lado Java (FT-05),
/// que replica byte a byte:</b></para>
/// <code>
/// sig = base64url_sem_padding( HMAC-SHA256( key = UTF8(segredo), msg = UTF8(chave + "\n" + exp) ) )
/// </code>
/// <para>onde <c>exp</c> é o unix timestamp em segundos (<c>DateTimeOffset.ToUnixTimeSeconds()</c>),
/// formatado em decimal invariante, sem separador de milhar.</para>
///
/// <para>O segredo vem de <c>Foto:UrlSecret</c> (env <c>Foto__UrlSecret</c>), validado no
/// registro de DI (<c>ServiceCollectionExtensions.AddInfrastructure</c>): ausente ou menor que
/// 32 bytes UTF-8 ⇒ falha na partida do processo, nunca no primeiro uso.</para>
/// </summary>
public interface IAssinadorUrlFoto
{
    /// <summary>
    /// Calcula a assinatura HMAC-SHA256 (base64url sem padding) de <paramref name="chave"/>
    /// com validade até <paramref name="expiraEm"/>. Não devolve o <c>exp</c> — quem monta a
    /// URL já tem <paramref name="expiraEm"/> e deriva o mesmo `exp` com
    /// <c>expiraEm.ToUnixTimeSeconds()</c>; devolver os dois duplicaria a conversão em dois
    /// lugares que precisariam ficar em sincronia.
    /// </summary>
    string Assinar(string chave, DateTimeOffset expiraEm);

    /// <summary>
    /// Valida <paramref name="sig"/> contra <paramref name="chave"/>/<paramref name="exp"/> e
    /// contra o relógio (<paramref name="agora"/>). <see langword="false"/> se a assinatura
    /// não bate (comparação em tempo constante), se <paramref name="exp"/> já passou de
    /// <paramref name="agora"/>, ou se qualquer parâmetro estiver malformado.
    /// </summary>
    bool Validar(string chave, long exp, string sig, DateTimeOffset agora);
}
