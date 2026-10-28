namespace Kura.Domain.Interfaces;

using Kura.Domain.Laudos;

/// <summary>
/// Repositório de laudos (MongoDB). REGRA: <b>toda</b> operação recebe <c>long idClinica</c>
/// e o aplica no filtro; não existe método sem escopo. A <b>assinatura</b> é travada por
/// <c>LaudoRepositoryEscopoTests</c> (reflexão, inclui interfaces herdadas); o <b>uso</b> do
/// <c>idClinica</c> no filtro é travado pelos testes de duas clínicas em <c>LaudoRepositoryTests</c>.
/// Não há exclusão física: exclusão é <c>stAtivo=false</c> + <c>removidoEm</c>. Falha de
/// conectividade vira <see cref="Kura.Domain.Exceptions.ArmazenamentoDocumentosIndisponivelException"/>;
/// recusa do schema vira <see cref="Kura.Domain.Exceptions.LaudoRecusadoPeloSchemaException"/>.
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
