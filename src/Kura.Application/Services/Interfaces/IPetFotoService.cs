namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Pet;
using Microsoft.AspNetCore.Http;

public interface IPetFotoService
{
    /// <summary>
    /// Recebe as 2 variantes (thumb/media) já validadas por magic bytes
    /// (<see cref="Validators.PetFotoUploadValidator"/>, roda antes via FluentValidation
    /// auto-validation) e grava a foto do pet. Lança
    /// <see cref="Kura.Domain.Exceptions.EntidadeNaoEncontradaException"/> (404) se o pet não
    /// existe OU pertence a outra clínica (herdado do query filter de tenant de
    /// <c>Pet</c>).
    /// </summary>
    Task<PetFotoResponseDto> UploadFotoAsync(
        long idPet, IFormFile thumb, IFormFile media, CancellationToken ct);
}
