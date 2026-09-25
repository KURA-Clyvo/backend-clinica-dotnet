namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Pet;

public interface IPetFotoService
{
    /// <summary>
    /// Recebe as 2 variantes (thumb/media) como <see cref="Stream"/> já validadas por magic
    /// bytes (<see cref="Validators.PetFotoUploadValidator"/>, roda antes, no controller) e
    /// grava a foto do pet. Lança
    /// <see cref="Kura.Domain.Exceptions.EntidadeNaoEncontradaException"/> (404) se o pet não
    /// existe OU pertence a outra clínica (herdado do query filter de tenant de
    /// <c>Pet</c>).
    ///
    /// <para>🔴 <b>Fix wave G2 (g2-ft03.md, achado G2-e):</b> assinatura trocou de
    /// <c>IFormFile</c> (tipo do framework web ASP.NET) para <see cref="Stream"/> puro —
    /// <c>Kura.Application</c> não pode depender do framework web. Precedente do próprio
    /// repo: <c>EventosClinicosController.cs</c> (transcrição de áudio) já recebe
    /// <c>IFormFile</c> só no controller e passa <see cref="Stream"/> para o service. Quem
    /// abre e fecha os streams é o chamador (Kura.Api).</para>
    /// </summary>
    Task<PetFotoResponseDto> UploadFotoAsync(
        long idPet, Stream thumb, Stream media, CancellationToken ct);
}
