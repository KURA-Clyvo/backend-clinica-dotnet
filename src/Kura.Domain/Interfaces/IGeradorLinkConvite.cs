namespace Kura.Domain.Interfaces;

/// <summary>
/// Monta o link do convite de onboarding do tutor — REC-01/backlog
/// <c>KURA_BACKLOG_RECEPCAO.md</c>, A-8.
///
/// <para>Formato: <c>{Convite:UrlBaseAppTutor}/register?token={token}&amp;clinicaId={idClinica}</c>
/// — a mesma rota que <c>mobile-tutor-rn</c> já resolve por deep link
/// (<c>_layout.tsx</c>/<c>utils/invite.ts</c>, confirmado no G0 item 5). Config ausente/vazia ⇒
/// <see cref="GerarLink"/> devolve <see langword="null"/> e o processo sobe normalmente (mesmo
/// padrão do <c>GeradorUrlFotoPet.java</c> do lado tutor, FT-05: um único <c>WARN</c> na
/// partida, nunca fail-fast) — o app cliente decide como tratar <c>dsLinkConvite: null</c>
/// (REC-03).</para>
/// </summary>
public interface IGeradorLinkConvite
{
    /// <summary>
    /// Gera o link do convite para o <paramref name="token"/>/<paramref name="idClinica"/>
    /// informados. <paramref name="idClinica"/> deve ser SEMPRE a clínica do JWT de quem está
    /// criando o tutor — nunca um valor vindo do corpo da requisição (mordida REC-01: duas
    /// clínicas, o tutor nasce na clínica do token).
    /// </summary>
    /// <returns>
    /// O link completo, ou <see langword="null"/> quando <c>Convite:UrlBaseAppTutor</c> não
    /// está configurado.
    /// </returns>
    string? GerarLink(Guid token, long idClinica);
}
