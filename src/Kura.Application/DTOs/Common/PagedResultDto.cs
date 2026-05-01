namespace Kura.Application.DTOs.Common;

public sealed class PagedResultDto<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
