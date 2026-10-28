namespace Kura.Api.Hal;

using System.Text.Json.Serialization;

/// <summary>
/// Um link HAL: <c>{ "href": "...", "method": "DELETE" }</c>. <c>method</c> é extensão documentada
/// (opcional no HAL) e só aparece em AÇÕES (ausente = GET).
/// </summary>
public sealed record LinkHal(
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("method"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Method = null);

/// <summary>Recurso que carrega <c>_links</c>. Implementado só por DTOs da camada Api.</summary>
public interface IRecursoHal
{
    [JsonPropertyName("_links")]
    Dictionary<string, LinkHal> Links { get; set; }
}

/// <summary>
/// Envelope paginado HAL: <c>{ items, page, pageSize, total, totalPages, _links }</c>. Base
/// comum reaproveitada por toda lista paginada (S4-NET-03 em diante).
/// </summary>
public sealed class PaginaHalDto<T> where T : IRecursoHal
{
    [JsonPropertyName("items")] public IReadOnlyList<T> Items { get; init; } = [];
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("pageSize")] public int PageSize { get; init; }
    [JsonPropertyName("total")] public long Total { get; init; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; init; }
    [JsonPropertyName("_links")] public Dictionary<string, LinkHal> Links { get; init; } = new();
}
