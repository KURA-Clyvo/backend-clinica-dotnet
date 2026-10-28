namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Laudo;

/// <summary>
/// Laudos de pets (metadados no MongoDB, binário no storage). Toda operação é escopada pela
/// clínica do JWT (<c>IClinicaContext</c>): laudo ou pet de outra clínica é 404, sem oráculo.
/// </summary>
public interface ILaudoService
{
    /// <param name="emailAutor">Claim <c>email</c> do JWT; resolve o <c>idUsuarioAutor</c> (o token não carrega o id).</param>
    Task<LaudoResponseDto> CriarAsync(long idPet, LaudoCriarDto dto, string? emailAutor, CancellationToken ct);

    Task<PagedResultDto<LaudoResponseDto>> ListarPorPetAsync(long idPet, LaudoListarDto consulta, CancellationToken ct);

    Task<LaudoResponseDto> ObterAsync(string id, CancellationToken ct);

    /// <summary>Abre o binário. Quem recebe é dono do stream (descarta).</summary>
    Task<LaudoArquivoDownload> ObterArquivoAsync(string id, CancellationToken ct);

    /// <summary>Exclusão lógica (o arquivo permanece no storage para trilha de auditoria).</summary>
    Task ExcluirAsync(string id, CancellationToken ct);
}
