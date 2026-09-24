namespace Kura.Domain.Interfaces;

/// <summary>
/// Abstração de armazenamento de arquivo binário (foto de pet, hoje; receituário PDF
/// migra depois — <c>FT-14</c>, fora do escopo desta task). FT-02/backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>, regra A3: "todo acesso a arquivo passa por
/// <see cref="IArmazenamentoArquivos"/>" — a implementação de hoje
/// (<c>ArmazenamentoLocalDisco</c>, em <c>Kura.Infrastructure</c>) grava no disco local via
/// o mesmo volume que já serve <c>Storage:BasePath</c> para o receituário; produção troca
/// para <c>ArmazenamentoAzureBlob</c> (FT-12) sem mudar quem consome esta interface.
///
/// <para><b>Chave é sempre RELATIVA</b> (ex.: <c>clinica/7/pet/12/{uuid}_256.webp</c>), nunca
/// caminho absoluto — é o que permite trocar de provedor/base sem migration de dado (regra
/// A2 do backlog). Implementações devem rejeitar chave vazia, com <c>..</c>, absoluta, ou que
/// resolva para fora do armazenamento configurado.</para>
///
/// <para>Vive em <c>Kura.Domain.Interfaces</c>, não em <c>Kura.Application</c>, porque
/// <c>Kura.Infrastructure</c> (onde a implementação de disco mora) só referencia
/// <c>Kura.Domain</c> e <c>Kura.CrossCutting</c> — o mesmo motivo que já colocou
/// <see cref="IUnitOfWork"/> aqui (implementado em <c>Kura.Infrastructure/Persistence</c>)
/// em vez de em <c>Kura.Application.Services.Interfaces</c> (onde vivem os serviços de
/// negócio, implementados dentro do próprio <c>Kura.Application</c>).</para>
/// </summary>
public interface IArmazenamentoArquivos
{
    /// <summary>Grava (ou sobrescreve) o conteúdo sob a chave informada.</summary>
    Task SalvarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct);

    /// <summary>
    /// Abre o conteúdo da chave informada, ou <see langword="null"/> se a chave não existe
    /// no armazenamento. Não lança para "não encontrado" — lançar é reservado para chave
    /// inválida (path traversal, formato inválido) e falha real de I/O.
    /// </summary>
    Task<Stream?> AbrirAsync(string chave, CancellationToken ct);

    /// <summary>
    /// Remove o conteúdo sob a chave informada. Idempotente: excluir uma chave que já não
    /// existe (ou nunca existiu) não lança.
    /// </summary>
    Task ExcluirAsync(string chave, CancellationToken ct);
}
