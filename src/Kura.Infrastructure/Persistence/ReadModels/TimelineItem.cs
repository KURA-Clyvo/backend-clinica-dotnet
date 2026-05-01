namespace Kura.Infrastructure.Persistence.ReadModels;

public class TimelineItem
{
    public long IdEventoClinico { get; set; }
    public long IdPet { get; set; }
    public string NmPet { get; set; } = string.Empty;
    public string NmTipo { get; set; } = string.Empty;
    public DateTime DtEvento { get; set; }
    public string DsObservacao { get; set; } = string.Empty;
    public string NmVeterinario { get; set; } = string.Empty;
    public char StAtiva { get; set; }
}
