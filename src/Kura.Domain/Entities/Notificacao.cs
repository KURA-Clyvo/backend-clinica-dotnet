namespace Kura.Domain.Entities;

public class Notificacao : EntidadeBase
{
    public long IdClinica { get; set; }
    public long? IdTutor { get; set; }
    public long? IdVeterinario { get; set; }
    public string DsTitulo { get; set; } = string.Empty;
    public string DsMensagem { get; set; } = string.Empty;
    public char StLida { get; set; } = 'N';
    public DateTime? DtLeitura { get; set; }
    public Tutor? Tutor { get; set; }
    public Veterinario? Veterinario { get; set; }
}
