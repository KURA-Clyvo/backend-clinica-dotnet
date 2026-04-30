namespace Kura.Domain.Entities;

public class Pet : EntidadeBase
{
    public long IdEspecie { get; set; }
    public long IdRaca { get; set; }
    public long? IdVeterinarioResp { get; set; }
    public string NmPet { get; set; } = string.Empty;
    public DateTime DtNascimento { get; set; }
    public char SgSexo { get; set; }
    public char SgPorte { get; set; }
    public Especie Especie { get; set; } = null!;
    public Raca Raca { get; set; } = null!;
    public ICollection<TutorPet> TutorPets { get; set; } = [];
}
