namespace Kura.Domain.Interfaces;

/// <summary>
/// REC-02 (KURA_BACKLOG_RECEPCAO.md): leitura de <c>CONTA_TUTOR</c> — tabela do Java
/// (<c>ReadOnlyTablesInterceptor</c> bloqueia qualquer escrita do .NET nela). <c>ContaTutor</c>
/// não herda <c>EntidadeBase</c> (sem <c>StAtiva</c>/soft delete — a conta do tutor não é
/// inativada pelo .NET), então não cabe no <see cref="IRepository{T}"/> genérico. Mesmo molde de
/// <see cref="IConsentimentoRepository"/>, a outra tabela read-only do Java.
/// </summary>
public interface IContaTutorRepository
{
    /// <summary>
    /// Verifica se o tutor já concluiu o onboarding (existe conta associada). Usado pela
    /// reemissão de convite (REC-02): tutor com conta ⇒ 409, reemitir não faz sentido.
    /// </summary>
    Task<bool> ExisteContaAsync(long idTutor);
}
