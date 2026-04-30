namespace Kura.Domain.Exceptions;

public class EntidadeNaoEncontradaException(string entidade, long id)
    : DomainException($"{entidade} com id {id} não encontrado.");
