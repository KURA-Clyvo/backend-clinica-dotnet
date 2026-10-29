namespace Kura.Api.Filters;

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// S4-NET-07 (G4-m-1) — rede central: todo parâmetro de QUERY sai no OpenAPI com a inicial minúscula
/// (<c>De</c> ⇒ <c>de</c>), igual aos <c>_links</c> e ao README. Só a DOCUMENTAÇÃO muda: o binding de
/// query do ASP.NET Core não distingue caixa, então <c>?De=</c> e <c>?de=</c> continuam valendo.
/// DTOs que ficam em <c>Kura.Application</c> (sem referência ao ASP.NET, p.ex. o do resumo financeiro)
/// não podem usar <c>[FromQuery(Name=...)]</c>; este filtro cobre o caso e qualquer DTO futuro.
/// </summary>
public sealed class QueryEmCamelCaseParameterFilter : IParameterFilter
{
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        if (parameter is OpenApiParameter p && p.In == ParameterLocation.Query
            && !string.IsNullOrEmpty(p.Name) && char.IsUpper(p.Name[0]))
        {
            p.Name = char.ToLowerInvariant(p.Name[0]) + p.Name[1..];
        }
    }
}
