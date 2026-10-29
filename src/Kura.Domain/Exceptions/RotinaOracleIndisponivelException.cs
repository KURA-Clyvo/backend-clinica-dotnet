namespace Kura.Domain.Exceptions;

/// <summary>
/// S4-DB-06 — a rotina Oracle (package <c>PKG_COBRANCA</c>, trigger de auditoria) não pôde ser
/// executada porque está ausente ou inválida (ORA-04063/04067/04068/06508/06550, ou 04098 no
/// <c>INSERT</c> de cobrança). A camada Api converte em <b>503</b>. Deliberadamente NÃO herda de
/// <see cref="DomainException"/>: não é regra de negócio violada, é infraestrutura fora.
///
/// <para>🔴 A mensagem é <b>fixa</b> e nunca carrega texto do Oracle: ele traz <c>ORA-</c> e o
/// nome do schema/package, que não são informação para o cliente.</para>
/// </summary>
public sealed class RotinaOracleIndisponivelException : Exception
{
    public const string MensagemFixa = "Rotina Oracle indisponível.";

    public RotinaOracleIndisponivelException(Exception? inner = null)
        : base(MensagemFixa, inner) { }
}
