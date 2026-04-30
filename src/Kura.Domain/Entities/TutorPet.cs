namespace Kura.Domain.Entities;

public class TutorPet
{
    public long IdTutor { get; set; }
    public long IdPet { get; set; }
    public string DsVinculo { get; set; } = "PROPRIETARIO";
    public char StPrincipal { get; set; } = 'S';
    public DateTime DtVinculo { get; set; } = DateTime.UtcNow;
    public Tutor Tutor { get; set; } = null!;
    public Pet Pet { get; set; } = null!;
}
