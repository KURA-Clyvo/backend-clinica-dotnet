namespace Kura.Domain.Interfaces;

using Kura.Domain.Laudos;

/// <summary>
/// Repositório de laudos (MongoDB). REGRA: <b>toda</b> operação recebe <c>long idClinica</c>
/// e o aplica no filtro; não existe método sem escopo (travado por
/// <c>LaudoRepositoryEscopoTests</c>, por reflexão). Não há exclusão física: exclusão é
/// <c>stAtivo=false</c> + <c>removidoEm</c>. Falha de infraestrutura vira
/// <see cref="Kura.Domain.Exceptions.ArmazenamentoDocumentosIndisponivelException"/>.
/// </summary>
public interface ILaudoRepository
{
    /// <summary>Detalhe de um laudo ativo da clínica; <c>null</c> se não existe, é de outra clínica ou foi removido.</summary>
    Task<Laudo?> ObterAsync(long idClinica, string id, CancellationToken ct = default);

    /// <summary>Lista paginada de laudos ativos do pet na clínica (filtro idClinica E idPet sempre).</summary>
    Task<PaginaLaudos> ListarPorPetAsync(long idClinica, long idPet, ConsultaLaudos consulta, CancellationToken ct = default);

    /// <summary>Insere o laudo; <c>laudo.IdClinica</c> deve ser igual a <paramref name="idClinica"/>. Devolve o laudo com Id e datas preenchidos.</summary>
    Task<Laudo> InserirAsync(long idClinica, Laudo laudo, CancellationToken ct = default);

    /// <summary>Exclusão lógica; <c>false</c> se não existe, é de outra clínica ou já estava removido.</summary>
    Task<bool> ExcluirLogicoAsync(long idClinica, string id, CancellationToken ct = default);
}
