namespace Kura.Domain.Entities;

public class Clinica : EntidadeBase
{
    public string NmClinica { get; set; } = string.Empty;
    public string NrCnpj { get; set; } = string.Empty;
    public string DsEndereco { get; set; } = string.Empty;
    public string NrTelefone { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;
    public string DsSenha { get; set; } = string.Empty;
    public ICollection<Veterinario> Veterinarios { get; set; } = [];
}
