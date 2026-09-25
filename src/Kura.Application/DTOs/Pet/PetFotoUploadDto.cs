namespace Kura.Application.DTOs.Pet;

/// <summary>
/// DTO interno para validar as 2 partes multipart de <c>POST /api/v1/pets/{id}/foto</c> —
/// FT-03/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>.
///
/// <para>🔴 <b>Fix wave G2 (g2-ft03.md, achado G2-e):</b> antes tinha <c>IFormFile</c> +
/// <c>[FromForm(Name=...)]</c> (tipos do framework web ASP.NET) — isso obrigava
/// <c>Kura.Application</c> a carregar um <c>FrameworkReference</c> do framework web,
/// quebrando a separação de camadas que o `README.md` público declara ("Clean Architecture
/// ... separação estrita"). Agora este DTO só carrega <see cref="Stream"/> puro (BCL, sem
/// dependência de framework web) — quem monta a instância é <c>PetsController</c>
/// (Kura.Api), que lê o multipart ele mesmo (não via model binding automático) e monta este
/// DTO a partir dos streams abertos.</para>
/// </summary>
public sealed class PetFotoUploadDto
{
    public Stream? Thumb { get; init; }

    public Stream? Media { get; init; }
}
