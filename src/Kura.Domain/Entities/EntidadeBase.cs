namespace Kura.Domain.Entities;

public abstract class EntidadeBase
{
    public long Id { get; set; }
    public char StAtiva { get; set; } = 'S';
    public DateTime DtCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DtAtualizacao { get; set; }
}
