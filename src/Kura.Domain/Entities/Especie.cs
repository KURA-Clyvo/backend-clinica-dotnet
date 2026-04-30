namespace Kura.Domain.Entities;

public class Especie : EntidadeBase
{
    public string NmEspecie { get; set; } = string.Empty;
    public ICollection<Raca> Racas { get; set; } = [];
}
