namespace Kura.Domain.Laudos;

/// <summary>Tipos de laudo aceitos (contrato <c>laudos</c> v1, G0 §5). Persistido como string.</summary>
public enum TipoLaudo
{
    HEMOGRAMA,
    BIOQUIMICO,
    IMAGEM,
    RECEITA,
    OUTRO,
}
