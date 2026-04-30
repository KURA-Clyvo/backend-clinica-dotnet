namespace Kura.Domain.Entities;

public class Veterinario : EntidadeBase
{
    public long IdClinica { get; set; }
    public string NmVeterinario { get; set; } = string.Empty;
    public string NrCrmv { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;
    public string NrTelefone { get; set; } = string.Empty;
    public Clinica Clinica { get; set; } = null!;
}
