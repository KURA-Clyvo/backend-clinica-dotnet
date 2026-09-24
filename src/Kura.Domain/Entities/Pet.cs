namespace Kura.Domain.Entities;

public class Pet : EntidadeBase
{
    public long IdClinica { get; set; }
    public long IdEspecie { get; set; }
    public long IdRaca { get; set; }
    public long? IdVeterinarioResp { get; set; }
    public string NmPet { get; set; } = string.Empty;
    public DateTime DtNascimento { get; set; }
    public char SgSexo { get; set; }
    public char SgPorte { get; set; }

    /// <summary>
    /// Chave BASE (com extensão) do armazenamento de foto — ex.
    /// <c>clinica/7/pet/12/{uuid}.webp</c>. <see langword="null"/> quando o pet nunca teve
    /// foto enviada. FT-03/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>, regra A2.
    ///
    /// <para><b>Por que a extensão mora AQUI, dentro da "base"</b>: só existe esta coluna
    /// (mais <see cref="DtFotoAtualizacao"/>) para representar a foto — sem uma coluna
    /// separada de extensão, a FT-04 (que serve o arquivo e monta as URLs derivadas) não
    /// teria como saber se o par de variantes é <c>.webp</c>, <c>.jpg</c> ou <c>.png</c> a
    /// partir de uma base "pura". As chaves das duas variantes são derivadas inserindo o
    /// sufixo de tamanho ANTES da extensão desta base (ex.:
    /// <c>clinica/7/pet/12/{uuid}_256.webp</c>, <c>..._1080.webp</c>) — fórmula única em
    /// <see cref="Kura.Domain.Storage.ChaveFotoPet"/> (ruling F7-a do maestro, G2
    /// <c>g2-ft01-ft02.md</c>), compartilhada com a FT-04 (lê) e replicada com âncora pela
    /// FT-05/Java.</para>
    /// </summary>
    public string? DsFotoChave { get; set; }

    /// <summary>Data/hora (UTC) da última troca de foto. <see langword="null"/> sem foto.</summary>
    public DateTime? DtFotoAtualizacao { get; set; }

    public Especie Especie { get; set; } = null!;
    public Raca Raca { get; set; } = null!;
    public ICollection<TutorPet> TutorPets { get; set; } = [];
}
