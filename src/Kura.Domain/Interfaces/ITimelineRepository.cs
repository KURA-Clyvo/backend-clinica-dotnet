namespace Kura.Domain.Interfaces;

using Kura.Domain.ValueObjects;

public interface ITimelineRepository
{
    Task<IEnumerable<TimelineEntry>> GetByPetIdAsync(long idPet);
}
