namespace Kura.Domain.ValueObjects;

public sealed record TimelineEntry(
    long IdEventoClinico,
    long IdPet,
    string NmPet,
    string NmTipo,
    DateTime DtEvento,
    string DsObservacao,
    string NmVeterinario);
