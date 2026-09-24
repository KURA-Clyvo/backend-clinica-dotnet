namespace Kura.Application.DTOs.Pet;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Corpo multipart de <c>POST /api/v1/pets/{id}/foto</c> — 2 partes nomeadas, FT-03/backlog
/// <c>KURA_BACKLOG_FOTO_PET.md</c>. <c>[FromForm(Name=...)]</c> explícito porque o nome da
/// parte multipart ("thumb"/"media") é contrato de API — não presumir que o binder casa por
/// nome de propriedade C# só porque hoje bate por acaso.
/// </summary>
public sealed class PetFotoUploadDto
{
    [FromForm(Name = "thumb")]
    public IFormFile? Thumb { get; init; }

    [FromForm(Name = "media")]
    public IFormFile? Media { get; init; }
}
