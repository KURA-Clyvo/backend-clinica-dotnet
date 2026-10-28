namespace Kura.Domain.Exceptions;

/// <summary>
/// O armazenamento recusou o laudo por violar o <c>$jsonSchema</c> (dado inválido, erro permanente:
/// repetir não adianta). A S4-NET-03 mapeia para 422. A mensagem é fixa e a exceção NÃO carrega a
/// exceção do driver: ela contém <c>consideredValue</c>, ou seja, conteúdo clínico (LGPD).
/// </summary>
public sealed class LaudoRecusadoPeloSchemaException : DomainException
{
    public LaudoRecusadoPeloSchemaException()
        : base("O laudo foi recusado pelo schema do armazenamento de documentos.") { }
}
