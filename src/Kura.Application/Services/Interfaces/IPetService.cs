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

    /// <summary>S4-NET-04 — lista paginada da v2 (a v1 <see cref="GetByFiltersAsync"/> não muda).</summary>
    Task<PaginaDe<PetResponseDto>> ListarPaginadoAsync(ConsultaPets consulta);
}
