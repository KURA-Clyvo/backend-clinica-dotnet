namespace Kura.Api.Hal;

using System.Security.Claims;
using System.Text.Json.Serialization;
using Kura.Api.Extensions;
using Kura.Application.DTOs.Laudo;

/// <summary>
/// Laudo com <c>_links</c>. Herda os campos de <see cref="LaudoResponseDto"/> (Application, sem
/// link nenhum) e acrescenta só o que é da camada Api.
/// </summary>
public sealed class LaudoHalDto : LaudoResponseDto, IRecursoHal
{
    [JsonPropertyName("_links")]
    public Dictionary<string, LinkHal> Links { get; set; } = new();

    public static LaudoHalDto De(LaudoResponseDto l, Dictionary<string, LinkHal> links) => new()
    {
        Id = l.Id,
        IdPet = l.IdPet,
        IdEventoClinico = l.IdEventoClinico,
        IdUsuarioAutor = l.IdUsuarioAutor,
        Tipo = l.Tipo,
        Titulo = l.Titulo,
        DataExame = l.DataExame,
        Laboratorio = l.Laboratorio,
        Observacao = l.Observacao,
        Dados = l.Dados,
        Arquivo = l.Arquivo,
        CriadoEm = l.CriadoEm,
        AtualizadoEm = l.AtualizadoEm,
        Links = links,
    };
}

/// <summary>Nomes das rotas de laudo (únicos no app; usados pelo <see cref="LinkGenerator"/>).</summary>
public static class RotasLaudo
{
    public const string Obter = "ObterLaudo";
    public const string Arquivo = "ObterArquivoLaudo";
    public const string Excluir = "ExcluirLaudo";
    public const string ListarDoPet = "ListarLaudosDoPet";
    public const string Pet = "ObterPet";
}

/// <summary>Monta os <c>_links</c> de um laudo, com ações dependentes do papel de quem pede.</summary>
public sealed class LaudoHalMapper
{
    private readonly IHalLinkFactory _links;

    public LaudoHalMapper(IHalLinkFactory links) => _links = links;

    /// <summary>
    /// <c>self</c>, <c>pet</c>, <c>laudosDoPet</c>, <c>arquivo</c> para todo autenticado;
    /// <c>excluir</c> (DELETE) só quando o usuário passa na política <c>SomenteGestor</c> — a mesma
    /// que protege o DELETE.
    /// </summary>
    public async Task<LaudoHalDto> MapearAsync(LaudoResponseDto laudo, ClaimsPrincipal usuario)
    {
        var links = new Dictionary<string, LinkHal>
        {
            ["self"] = _links.Criar(RotasLaudo.Obter, new { id = laudo.Id }),
            ["pet"] = _links.Criar(RotasLaudo.Pet, new { id = laudo.IdPet }),
            ["laudosDoPet"] = _links.Criar(RotasLaudo.ListarDoPet, new { idPet = laudo.IdPet }),
            ["arquivo"] = _links.Criar(RotasLaudo.Arquivo, new { id = laudo.Id }),
        };
        if (await _links.PodeAsync(usuario, PoliticasAutorizacao.SomenteGestor))
            links["excluir"] = _links.Criar(RotasLaudo.Excluir, new { id = laudo.Id }, "DELETE");
        return LaudoHalDto.De(laudo, links);
    }
}
