namespace Kura.Domain.Entities;

public class Raca : EntidadeBase
{
    public long IdEspecie { get; set; }
    public string NmRaca { get; set; } = string.Empty;
    public Especie Especie { get; set; } = null!;
}
