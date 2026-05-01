namespace Kura.Application.DTOs.Pet;

public sealed class PetResponseDto
{
    public long Id { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public long IdEspecie { get; init; }
    public string NmEspecie { get; init; } = string.Empty;
    public long IdRaca { get; init; }
    public string NmRaca { get; init; } = string.Empty;
    public long? IdVeterinarioResp { get; init; }
    public DateTime DtNascimento { get; init; }
    public char SgSexo { get; init; }
    public char SgPorte { get; init; }
    public char StAtiva { get; init; }
}
