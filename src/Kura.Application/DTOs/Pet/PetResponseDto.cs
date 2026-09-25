namespace Kura.Application.DTOs.Pet;

using Kura.Application.DTOs.Tutor;

public sealed class PetResponseDto
{
    public long Id { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public long IdEspecie { get; init; }
    public string NmEspecie { get; init; } = string.Empty;
    public long IdRaca { get; init; }
    public string NmRaca { get; init; } = string.Empty;
    public long? IdVeterinarioResp { get; init; }
    public DateTime DtNascimento { get; init; }
    public char SgSexo { get; init; }
    public char SgPorte { get; init; }
    public bool StAtiva { get; init; }

    // FT-04 (backlog KURA_BACKLOG_FOTO_PET.md, regra A5): URL assinada da variante 1080
    // (detalhe) e da variante 256 (lista/avatar). null quando o pet não tem foto
    // (Pet.DsFotoChave null) — nunca lança, nunca string vazia. Serializam camelCase
    // (dsFotoUrl/dsFotoThumbUrl) pela convenção JSON padrão da API. Os apps ainda NÃO
    // declaram esses campos no tipo de pet — o consumo entra na FT-08 (medido no G2 da FT-04).
    public string? DsFotoUrl { get; init; }
    public string? DsFotoThumbUrl { get; init; }

    public IReadOnlyList<TutorVinculoDto> Tutores { get; init; } = [];
}

public sealed class TutorVinculoDto
{
    public long IdTutor { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string DsVinculo { get; init; } = string.Empty;
    public bool StPrincipal { get; init; }
}
