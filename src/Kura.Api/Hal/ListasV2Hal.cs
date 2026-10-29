namespace Kura.Api.Hal;

using System.Text.Json.Serialization;
using Kura.Application.DTOs.Agenda;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services;

/// <summary>Nomes das rotas v2 (únicos no app; usados pelo <see cref="LinkGenerator"/>).</summary>
public static class RotasV2
{
    public const string ListarPets = "ListarPetsV2";
    public const string ObterPet = "ObterPetV2";
    public const string ListarTutores = "ListarTutoresV2";
    public const string ObterTutor = "ObterTutorV2";
    public const string ListarAgendamentos = "ListarAgendamentosV2";
}

/// <summary>
/// Pet da v2: os MESMOS campos de <see cref="PetResponseDto"/> (a v2 não expõe campo a mais que a
/// v1) mais <c>_links</c>. <see cref="PetResponseDto"/> é <c>sealed</c> e vive na camada Application
/// (sem link nenhum); por isso a cópia explícita em <see cref="De"/>.
/// </summary>
public sealed class PetHalDto : IRecursoHal
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
    public string? DsFotoUrl { get; init; }
    public string? DsFotoThumbUrl { get; init; }
    public IReadOnlyList<TutorVinculoDto> Tutores { get; init; } = [];

    [JsonPropertyName("_links")]
    public Dictionary<string, LinkHal> Links { get; set; } = new();

    public static PetHalDto De(PetResponseDto p, Dictionary<string, LinkHal> links) => new()
    {
        Id = p.Id, NmPet = p.NmPet, IdEspecie = p.IdEspecie, NmEspecie = p.NmEspecie,
        IdRaca = p.IdRaca, NmRaca = p.NmRaca, IdVeterinarioResp = p.IdVeterinarioResp,
        DtNascimento = p.DtNascimento, SgSexo = p.SgSexo, SgPorte = p.SgPorte, StAtiva = p.StAtiva,
        DsFotoUrl = p.DsFotoUrl, DsFotoThumbUrl = p.DsFotoThumbUrl, Tutores = p.Tutores, Links = links,
    };
}

/// <summary>Tutor da v2: mesmos campos de <see cref="TutorResponseDto"/> (nenhum a mais) mais <c>_links</c>.</summary>
public sealed class TutorHalDto : IRecursoHal
{
    public long Id { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public bool StAtiva { get; init; }

    [JsonPropertyName("_links")]
    public Dictionary<string, LinkHal> Links { get; set; } = new();

    public static TutorHalDto De(TutorResponseDto t, Dictionary<string, LinkHal> links) => new()
    {
        Id = t.Id, NmTutor = t.NmTutor, NrCpf = t.NrCpf, DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone, StAtiva = t.StAtiva, Links = links,
    };
}

/// <summary>Agendamento da v2: herda os campos de <see cref="AgendamentoItemDto"/> e acrescenta <c>_links</c>.</summary>
public sealed class AgendamentoHalDto : AgendamentoItemDto, IRecursoHal
{
    [JsonPropertyName("_links")]
    public Dictionary<string, LinkHal> Links { get; set; } = new();

    public static AgendamentoHalDto De(AgendamentoItemDto a, Dictionary<string, LinkHal> links) => new()
    {
        IdAgendamento = a.IdAgendamento, DtAgendamento = a.DtAgendamento, DuracaoMinutos = a.DuracaoMinutos,
        NmTutor = a.NmTutor, NmPet = a.NmPet, IdVeterinario = a.IdVeterinario, NmVeterinario = a.NmVeterinario,
        DsTipoConsulta = a.DsTipoConsulta, DsStatus = a.DsStatus, NrVersion = a.NrVersion,
        IdPet = a.IdPet, IdTutor = a.IdTutor, DtCheckin = a.DtCheckin,
        DtInicioAtendimento = a.DtInicioAtendimento, DsOrigem = a.DsOrigem,
        DsNivelUrgenciaOrigem = a.DsNivelUrgenciaOrigem, DsRespostaConfirmacao = a.DsRespostaConfirmacao,
        DsEtapaRecepcao = a.DsEtapaRecepcao, DsFotoThumbUrl = a.DsFotoThumbUrl, Links = links,
    };
}

/// <summary>
/// Monta os <c>_links</c> dos recursos da v2. <b>Papéis:</b> as rotas v1 de pets, tutores e agenda
/// só têm o <c>[Authorize]</c> da classe (GESTOR e VETERINARIO, os únicos papéis, passam), então as
/// ações aparecem para todo autenticado — não há política por verbo a espelhar. Se uma política
/// for posta num verbo v1, o mapper deve passar a consultar <see cref="IHalLinkFactory.PodeAsync"/>.
/// </summary>
public sealed class ListasV2HalMapper
{
    private readonly IHalLinkFactory _links;

    public ListasV2HalMapper(IHalLinkFactory links) => _links = links;

    /// <summary>
    /// <c>self</c>, <c>vacinas</c>, <c>laudos</c>, <c>timeline</c>, <c>editar</c> (PUT), <c>inativar</c> (DELETE),
    /// (sem <c>adicionarTutor</c>: <c>POST /api/v1/pets/{id}/tutores</c> responde 500 com corpo vazio — defeito
    /// pré-existente da v1, G2-m-3 —, então não é anunciado). Não há <c>GET /pets/{id}/tutores</c> na v1: os tutores vêm embutidos no
    /// próprio pet (<c>tutores</c>), então não existe link de leitura para eles.
    /// </summary>
    public PetHalDto Mapear(PetResponseDto p)
    {
        var id = new { id = p.Id };
        var links = new Dictionary<string, LinkHal>
        {
            ["self"] = _links.Criar(RotasV2.ObterPet, id),
            ["vacinas"] = _links.Criar("ProximasVacinasDoPet", id),
            ["laudos"] = _links.Criar(RotasLaudo.ListarDoPet, new { idPet = p.Id }),
            ["timeline"] = _links.Criar("TimelineDoPet", id),
            ["editar"] = _links.Criar("AtualizarPet", id, "PUT"),
            ["inativar"] = _links.Criar("InativarPet", id, "DELETE"),
        };
        return PetHalDto.De(p, links);
    }

    /// <summary><c>self</c>, <c>pets</c> (v1), <c>editar</c> (PUT), <c>inativar</c> (DELETE).</summary>
    public TutorHalDto Mapear(TutorResponseDto t)
    {
        var id = new { id = t.Id };
        var links = new Dictionary<string, LinkHal>
        {
            ["self"] = _links.Criar(RotasV2.ObterTutor, id),
            ["pets"] = _links.Criar("PetsDoTutor", id),
            ["editar"] = _links.Criar("AtualizarTutor", id, "PUT"),
            ["inativar"] = _links.Criar("InativarTutor", id, "DELETE"),
        };
        return TutorHalDto.De(t, links);
    }

    /// <summary>
    /// <c>pet</c> (quando há pet; G2-m-2: o item não informa se o pet segue ativo, então o link pode responder
    /// 404 para pet inativado — decisão de não gastar consulta extra por item) e as ações de recepção/status SÓ quando a regra do
    /// <see cref="AgendaService"/> as admite para o estado atual. Não há <c>GET</c> v1 de detalhe de
    /// agendamento, então não existe <c>self</c> por item (o <c>self</c> está no envelope da lista).
    /// O dia do check-in e o <c>nrVersion</c> só são avaliados na chamada (422/409).
    /// </summary>
    public AgendamentoHalDto Mapear(AgendamentoItemDto a)
    {
        var id = new { id = a.IdAgendamento };
        var links = new Dictionary<string, LinkHal>();
        if (a.IdPet is not null)
            links["pet"] = _links.Criar(RotasV2.ObterPet, new { id = a.IdPet.Value });
        if (AgendaService.AdmiteCheckin(a.DsStatus, a.DtCheckin, a.DtInicioAtendimento))
            links["checkin"] = _links.Criar("CheckinAgendamento", id, "POST");
        if (AgendaService.AdmiteInicioAtendimento(a.DsStatus, a.DtInicioAtendimento))
            links["inicio-atendimento"] = _links.Criar("InicioAtendimentoAgendamento", id, "POST");
        if (AgendaService.AdmiteTransicaoDeStatus(a.DsStatus))
            links["status"] = _links.Criar("AtualizarStatusAgendamento", id, "PATCH");
        return AgendamentoHalDto.De(a, links);
    }
}
