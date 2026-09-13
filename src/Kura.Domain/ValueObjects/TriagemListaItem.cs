namespace Kura.Domain.ValueObjects;

/// <summary>
/// LU-08 — item de GET /api/v1/luna/triagens (JWT de clínica). Devolvido pelo
/// repositório (não pelo DbContext direto) para manter o Application layer sem
/// dependência de EF — mesmo padrão de <see cref="TimelineEntry"/>.
///
/// <c>TrechoMensagem</c> é <c>null</c> quando a triagem não tem <c>IdInteracao</c>
/// (campo nullable desde V9) ou quando o join com INTERACAO_CANAL não encontra a
/// linha correspondente NA MESMA CLÍNICA — ver
/// <c>TriagemLunaRepository.ListarPorClinicaAsync</c> para o predicado explícito de
/// clínica que torna esse "não encontra" seguro em vez de um vazamento cross-tenant.
/// </summary>
public sealed record TriagemListaItem(
    long IdTriagem,
    DateTime DtTriagem,
    string Urgencia,
    IReadOnlyList<string> Sintomas,
    int? Score,
    string? RegrasVersao,
    bool EncaminhadoVet,
    long? IdTutor,
    string? NomeTutor,
    IReadOnlyList<TriagemPetResumo> Pets,
    string? TrechoMensagem);

/// <summary>Pet do tutor da triagem — nome da espécie em texto, não id (mesma
/// convenção de <c>PetResumoLunaDto</c>).</summary>
public sealed record TriagemPetResumo(long IdPet, string NmPet, string NmEspecie);
