namespace Kura.Application.DTOs.Pet;

/// <summary>
/// Resposta de <c>POST /api/v1/pets/{id}/foto</c>. Só a chave/data — as URLs assinadas
/// (<c>dsFotoUrl</c>/<c>dsFotoThumbUrl</c>) são responsabilidade da FT-04, que expõe o DTO
/// completo de Pet com URL derivada; esta resposta é só a confirmação da escrita.
/// </summary>
public sealed class PetFotoResponseDto
{
    public long IdPet { get; init; }
    public string DsFotoChave { get; init; } = string.Empty;
    public DateTime DtFotoAtualizacao { get; init; }
}
