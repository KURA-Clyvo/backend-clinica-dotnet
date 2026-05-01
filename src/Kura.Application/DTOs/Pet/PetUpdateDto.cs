namespace Kura.Application.DTOs.Pet;

public sealed class PetUpdateDto
{
    public long? IdVeterinarioResp { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public char SgSexo { get; init; }
    public char SgPorte { get; init; }
}
