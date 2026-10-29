namespace Kura.Domain.Exceptions;

/// <summary>
/// S4-DB-06 — a rotina Oracle falhou por motivo que não é "indisponível" nem "clínica
/// inexistente" (ex.: ORA-20001, que com <c>IdClinica</c> <c>long</c> vindo do JWT é bug do
/// servidor). Vira <b>500</b>. A mensagem é <b>fixa</b>: o middleware põe <c>Message</c> no
/// <c>title</c> da resposta, então nada do Oracle (<c>ORA-</c>, <c>KURA.PKG_COBRANCA</c>) pode
/// entrar nela. O detalhe vive só em <see cref="Exception.InnerException"/>, que vai para o log
/// do servidor e nunca para o corpo.
/// </summary>
public sealed class RotinaOracleFalhouException : Exception
{
    public const string MensagemFixa = "Falha ao executar a rotina do banco de dados.";

    public RotinaOracleFalhouException(Exception? inner = null)
        : base(MensagemFixa, inner) { }
}
