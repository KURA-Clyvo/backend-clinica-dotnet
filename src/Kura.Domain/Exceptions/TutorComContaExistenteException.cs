namespace Kura.Domain.Exceptions;

/// <summary>
/// REC-02 (KURA_BACKLOG_RECEPCAO.md): reemissão de convite (<c>POST /api/v1/tutores/{id}/convite</c>)
/// recusada porque o tutor já concluiu o onboarding — existe uma linha em <c>CONTA_TUTOR</c>
/// (tabela do Java, lida pelo .NET só como referência, nunca escrita — <c>ReadOnlyTablesInterceptor</c>)
/// para o <c>ID_TUTOR</c> informado. Mapeada para 409 pelo <c>ExceptionHandlerMiddleware</c>.
///
/// Não reusa <see cref="ConflitoConcorrenciaException"/> (também 409): aquela é sobre optimistic
/// locking/<c>NR_VERSION</c> e produziria mensagem semanticamente errada aqui — "foi modificado por
/// outro processo" não descreve "o tutor já tem conta".
/// </summary>
public class TutorComContaExistenteException : DomainException
{
    public TutorComContaExistenteException(long idTutor)
        : base($"Tutor id {idTutor} já possui conta. Não é possível reemitir convite.") { }
}
