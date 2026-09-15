namespace Kura.Infrastructure.Persistence.Repositories;

using System.Text;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

public class TriagemLunaRepository(KuraDbContext context) : ITriagemLunaRepository
{
    public async Task<List<TriagemLuna>> GetByIntervaloAsync(DateTime dataInicio, DateTime dataFim)
    {
        return await context.TriagensLuna
            .Where(t => t.DtTriagem >= dataInicio && t.DtTriagem <= dataFim)
            .ToListAsync();
    }

    public async Task AddAsync(TriagemLuna entidade)
    {
        await context.TriagensLuna.AddAsync(entidade);
    }

    /// <summary>
    /// LU-08 — GET /api/v1/luna/triagens. O join com INTERACAO_CANAL é feito com uma
    /// chave composta (IdInteracao, IdClinica) — a metade IdClinica é o predicado
    /// 🔴 EXIGIDO pelo brief ("interacao.IdClinica == triagem.IdClinica explícito no
    /// LINQ, não delegado ao query filter"): sem ela, o LEFT JOIN casaria qualquer
    /// INTERACAO_CANAL pelo Id sozinho, e uma linha corrompida/cross-tenant (FK
    /// gravada errado antes da checagem de LunaService.RegistrarTriagemAsync existir,
    /// ou por um caminho de escrita futuro que não passe por ali) vazaria
    /// DS_CONTEUDO de outra clínica. Ver TriagemLunaListaTenantIsolationTests para a
    /// mutação que prova isso — ela roda com o HasQueryFilter global DESLIGADO
    /// (IdClinicaFiltro null), de propósito: com o filtro ligado, esta linha é
    /// redundante e a mutação não morderia nominalmente (armadilha da regra 13).
    /// </summary>
    public async Task<(IReadOnlyList<TriagemListaItem> Itens, int Total)> ListarPorClinicaAsync(
        long idClinica,
        string? urgencia,
        DateTime? dataInicio,
        DateTime? dataFim,
        int page,
        int pageSize)
    {
        var joined =
            from t in context.TriagensLuna.AsNoTracking()
            where t.IdClinica == idClinica // explícito — independe do HasQueryFilter global
            join i in context.InteracoesCanal.AsNoTracking()
                on new { IdInteracao = t.IdInteracao, IdClinica = (long?)t.IdClinica }
                equals new { IdInteracao = (long?)i.Id, IdClinica = i.IdClinica }
                into interacaoJoin
            from i in interacaoJoin.DefaultIfEmpty()
            select new { Triagem = t, Conteudo = (string?)i.DsConteudo };

        if (!string.IsNullOrWhiteSpace(urgencia))
            joined = joined.Where(x => x.Triagem.DsNivelUrgencia == urgencia);

        if (dataInicio.HasValue)
            joined = joined.Where(x => x.Triagem.DtTriagem >= dataInicio.Value);

        if (dataFim.HasValue)
            joined = joined.Where(x => x.Triagem.DtTriagem <= dataFim.Value);

        var total = await joined.CountAsync();

        // Ordenação fixa do brief: ALTA -> MEDIA -> BAIXA, depois mais recente.
        // DsNivelUrgencia não tem CHECK constraint (ver TriagemLuna.cs) — um valor
        // fora dos 3 esperados cai no ramo "3" (final da lista), nunca quebra o sort.
        var ordenada = joined
            .OrderBy(x => x.Triagem.DsNivelUrgencia == "ALTA" ? 0
                : x.Triagem.DsNivelUrgencia == "MEDIA" ? 1
                : x.Triagem.DsNivelUrgencia == "BAIXA" ? 2
                : 3)
            .ThenByDescending(x => x.Triagem.DtTriagem);

        var pagina = await ordenada
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var idsTutor = pagina
            .Where(x => x.Triagem.IdTutor != null)
            .Select(x => x.Triagem.IdTutor!.Value)
            .Distinct()
            .ToList();

        // Mesma defesa em profundidade do join acima: filtra por idClinica explícito,
        // não só pelo HasQueryFilter global de Tutor. idsTutor vazio faz o Contains()
        // traduzir para uma cláusula sempre-falsa — sem round trip especial, mas sem
        // exceção nem resultado espúrio.
        var nomesTutor = await context.Tutores.AsNoTracking()
            .Where(tu => idsTutor.Contains(tu.Id) && tu.IdClinica == idClinica)
            .ToDictionaryAsync(tu => tu.Id, tu => tu.NmTutor);

        var petsPorTutorBruto = await context.TutorPets.AsNoTracking()
            .Where(tp => idsTutor.Contains(tp.IdTutor) && tp.Pet.IdClinica == idClinica)
            .Select(tp => new { tp.IdTutor, tp.Pet.Id, tp.Pet.NmPet, tp.Pet.Especie.NmEspecie })
            .ToListAsync();

        var petsPorTutor = petsPorTutorBruto
            .GroupBy(x => x.IdTutor)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TriagemPetResumo>)g
                    .Select(x => new TriagemPetResumo(x.Id, x.NmPet, x.NmEspecie))
                    .ToList());

        var itens = pagina
            .Select(x =>
            {
                var idTutor = x.Triagem.IdTutor;
                var nomeTutor = idTutor.HasValue && nomesTutor.TryGetValue(idTutor.Value, out var nm)
                    ? nm
                    : null;
                var pets = idTutor.HasValue && petsPorTutor.TryGetValue(idTutor.Value, out var p)
                    ? p
                    : (IReadOnlyList<TriagemPetResumo>)[];

                return new TriagemListaItem(
                    x.Triagem.Id,
                    x.Triagem.DtTriagem,
                    x.Triagem.DsNivelUrgencia,
                    SplitSintomas(x.Triagem.DsSintomas),
                    x.Triagem.NrScore,
                    x.Triagem.DsRegrasVersao,
                    x.Triagem.StEncaminhadoVet,
                    idTutor,
                    nomeTutor,
                    pets,
                    ObterTrechoMensagem(x.Conteudo));
            })
            .ToList();

        return (itens, total);
    }

    // Leitura simétrica à escrita em LunaService.ComporSintomas — mesmo delimitador
    // (';'), documentado em TriagemLuna.DsSintomas.
    private static IReadOnlyList<string> SplitSintomas(string? sintomas) =>
        string.IsNullOrWhiteSpace(sintomas)
            ? []
            : sintomas.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Primeiros 280 CARACTERES (não bytes) de DS_CONTEUDO — aqui é exibição na tela da
    // clínica, não coluna Oracle, então o raciocínio de bytes-vs-caracteres das
    // constantes de truncamento de LunaService não se aplica (brief LU-08, item 3).
    private const int TamanhoTrechoMensagem = 280;

    // Fix wave 1 (MENOR-1/MENOR-6, lu-08-revisao.md frente 3/4): conteudo[..280]
    // cortava por UNIDADE UTF-16 (System.String.Length), sem olhar limite de
    // caractere — quando o corte caía no meio de um par substituto (emoji fora do BMP,
    // 2 unidades UTF-16), sobrava um high surrogate solto. A G2 mediu o sintoma direto
    // em Oracle real: item com "aaa…(279 'a')…�" no JSON (o serializador troca o
    // surrogate solto por U+FFFD ao codificar em UTF-8). Itera por Rune (unidade
    // Unicode completa, nunca quebra um par substituto) e para ANTES de estourar 280
    // unidades UTF-16 — o resultado pode ter menos de 280 unidades quando o próximo
    // Rune não caberia inteiro, nunca um surrogate partido.
    private static string? ObterTrechoMensagem(string? conteudo)
    {
        if (conteudo is null)
            return null;

        if (conteudo.Length <= TamanhoTrechoMensagem)
            return conteudo;

        var builder = new StringBuilder();
        var unidadesUsadas = 0;
        foreach (var rune in conteudo.EnumerateRunes())
        {
            var unidadesRune = rune.Utf16SequenceLength;
            if (unidadesUsadas + unidadesRune > TamanhoTrechoMensagem)
                break;

            builder.Append(rune.ToString());
            unidadesUsadas += unidadesRune;
        }

        return builder.ToString();
    }
}
