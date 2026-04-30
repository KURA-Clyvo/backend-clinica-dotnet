namespace Kura.Domain.Entities;

public class Tutor : EntidadeBase
{
    public string NmTutor { get; set; } = string.Empty;
    public string NrCpf { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;
    public string NrTelefone { get; set; } = string.Empty;
    public ICollection<TutorPet> TutorPets { get; set; } = [];
}
