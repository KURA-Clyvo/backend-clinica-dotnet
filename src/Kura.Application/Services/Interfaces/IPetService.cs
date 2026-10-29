namespace Kura.Application.Services.Interfaces;

using Kura.Domain.Paginacao;
using Kura.Application.DTOs.Pet;

public interface IPetService
{
    Task<IEnumerable<PetResponseDto>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte);
    Task<PetResponseDto> GetByIdAsync(long id);
    Task<PetResponseDto> CreateAsync(PetCreateDto dto);
    Task<PetResponseDto> UpdateAsync(long id, PetUpdateDto dto);
    Task SoftDeleteAsync(long id);
    Task AdicionarTutorAsync(long idPet, AdicionarTutorPetDto dto);

    /// <summary>
    /// S4-NET-04 (G2-m-1) — detalhe da v2, escopado por <c>IClinicaContext.IdClinica</c> à mão
    /// (a v1 <see cref="GetByIdAsync"/> não muda — D-NET-4). Inexistente, inativo ou de outra clínica ⇒ 404.
    /// </summary>
    Task<PetResponseDto> ObterEscopadoAsync(long id);

    /// <summary>S4-NET-04 — lista paginada da v2 (a v1 <see cref="GetByFiltersAsync"/> não muda).</summary>
    Task<PaginaDe<PetResponseDto>> ListarPaginadoAsync(ConsultaPets consulta);
}
