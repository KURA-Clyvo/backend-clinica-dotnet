namespace Kura.Domain.Exceptions;

/// <summary>
/// O armazenamento de documentos (MongoDB) não respondeu ou não está configurado. A camada
/// Api converte em 503 <c>LAUDOS_INDISPONIVEL</c> (S4-NET-03). Deliberadamente NÃO herda de
/// <see cref="DomainException"/>: não é regra de negócio violada, é infraestrutura fora.
/// A mensagem nunca carrega connection string.
/// </summary>
public sealed class ArmazenamentoDocumentosIndisponivelException : Exception
{
    public ArmazenamentoDocumentosIndisponivelException(string message, Exception? inner = null)
        : base(message, inner) { }
}
