namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class TutorRepository : Repository<Tutor>, ITutorRepository
{
    private readonly ILogger<TutorRepository> _logger;

    public TutorRepository(KuraDbContext context, ILogger<TutorRepository> logger) : base(context)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<Tutor>> SearchAsync(string? busca, long idClinica)
    {
        var query = _dbSet.Where(t => t.IdClinica == idClinica);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var lower = busca.ToLower();
            query = query.Where(t => t.NmTutor.ToLower().Contains(lower) || t.NrCpf.Contains(busca));
        }

        return await query.ToListAsync();
    }

    public Task<Tutor?> GetByIdAsync(long id, long idClinica)
        => _dbSet.FirstOrDefaultAsync(t => t.Id == id && t.IdClinica == idClinica);

    public async Task<Tutor?> GetByTelefoneAsync(string numero)
    {
        // TASK-79: antes, FirstOrDefaultAsync sem ORDER BY podia devolver QUALQUER UM dos
        // tutores colidentes (não-determinístico contra Oracle — plano/ordem física pode
        // mudar entre execuções). Determinismo sozinho não fecha o problema real: o
        // vazamento é que TUTOR.DS_TELEFONE não tem UNIQUE (V1__initial_schema.sql:91) e
        // mais de um tutor ATIVO pode compartilhar o mesmo número — inclusive dois
        // tutores da MESMA clínica (ex.: casal com o telefone da casa), não só entre
        // clínicas diferentes. Sem este fix, a Luna (autenticada por API Key, sem JWT de
        // clínica — logo sem qualquer escopo de tenant) podia receber nome/id_clinica/
        // pets do tutor ERRADO e responder no WhatsApp com dado de outra clínica.
        //
        // Fix: telefone AMBÍGUO (mais de um tutor ATIVO com o mesmo número, qualquer
        // clínica) é tratado como "não encontrado" — a MESMA forma que este método já usa
        // para telefone inexistente (null → controller responde 404). Verificado nesta
        // task, na fonte do consumidor (kura-luna-ai/luna/src/services/
        // inbound_message_service.py e src/integration/kura_client.py): 404 já é o
        // caminho gracioso — buscar_tutor_por_telefone devolve None, a interação é
        // registrada com id_tutor=None (TASK-77 fechou esse caminho: grava com
        // ID_CLINICA/ID_TUTOR nulos em vez de 422), a triagem é pulada, e o fallback
        // genérico é enviado ao tutor. Nenhum caminho novo de resposta foi criado —
        // degradar para "não encontrado" é estritamente melhor do que devolver a
        // clínica errada.
        //
        // Rodada de fix 1 (revisão G2): colisão INTRA-clínica (mesma clínica, 2+
        // tutores ativos com o mesmo telefone) também cai neste caminho e devolve
        // null — mantido de propósito, não corrigido para "só cross-clínica". Um
        // tutor devolvido erroneamente aqui faria a Luna gravar triagem (sintomas,
        // urgência, score) no id_tutor errado, o que é pior do que triagem ausente.
        // Consequência real: aquele domicílio recebe o fallback genérico da Luna e a
        // interação é gravada com ID_CLINICA/ID_TUTOR nulos (invisível a qualquer
        // consulta escopada por clínica) em vez de a clínica ver a mensagem.
        // DECIDIDO pelo Felipe em 2026-08-11 — não é mais pergunta em aberto: manter
        // este comportamento E abrir task no Bloco 0 do FIX_8 para desambiguar de
        // verdade (a clínica NÃO é ambígua no caso intra-clínica; só o tutor é).
        // Ou seja: o caso do casal não está resolvido, está aceito com prazo — não
        // tratar como encerrado.
        //
        // A Luna nunca envia o sentinela "Não informado" (TASK-60, coalesce de
        // telefone vazio em TutorService.cs:97-99/:129-131) — ela sempre chega aqui
        // com o número real do remetente Twilio. Mas a ROTA aceita string arbitrária:
        // {numero} não tem route constraint, regex nem normalização em nenhum degrau
        // (TutoresController.GetPorTelefone → BuscarContextoPorTelefoneAsync → aqui),
        // e qualquer portador da API key da Luna pode chamar
        // GET /api/v1/tutores/telefone/N%C3%A3o%20informado — daí o .Take(2): a
        // lógica só precisa distinguir 0/1/>1, e sem limite um sentinela com centenas
        // de tutores sem telefone materializava e rastreava TODAS as entidades no
        // ChangeTracker do request, mais uma linha de log que cresce sem limite
        // (medido nesta rodada: 300 tutores colidentes → 300 entidades rastreadas e
        // log de 1601 caracteres SEM Take(2); 2 entidades e 111 caracteres COM
        // Take(2) — ver task-79-fixround1-report.md).
        var candidatos = await _dbSet
            .Where(t => t.NrTelefone == numero)
            .OrderBy(t => t.Id) // determinismo: nunca depender de plano/ordem física do Oracle
            .Take(2) // só precisamos distinguir 0/1/>1; o ORDER BY garante que os 2
                     // primeiros sejam determinísticos
            .ToListAsync();

        if (candidatos.Count > 1)
        {
            // LGPD: NUNCA logar `numero` (mesma regra de RedigirPathSensivel/TASK-67 e
            // ITutorService.BuscarContextoPorTelefoneAsync). Com Take(2), candidatos
            // nunca tem mais de 2 elementos aqui — "≥2" declara isso explicitamente em
            // vez de sugerir que o total de colidentes é conhecido.
            _logger.LogWarning(
                "GetByTelefoneAsync: colisão de telefone entre ≥2 tutores ativos " +
                "(ids: {Ids}) — tratado como não encontrado.",
                string.Join(",", candidatos.Select(t => t.Id)));
            return null;
        }

        return candidatos.SingleOrDefault();
    }

    /// <summary>
    /// R3a (G2b fix wave 2): mesmo filtro/Take(2) de <see cref="GetByTelefoneAsync"/>, mas
    /// devolve a CONTAGEM (0, 1 ou 2) em vez do tutor — permite ao chamador distinguir "não
    /// encontrado" (0) de "ambíguo" (2) antes de decidir se tenta outra chave de busca.
    /// </summary>
    public Task<int> ContarAtivosPorTelefoneAsync(string numero) =>
        _dbSet.Where(t => t.NrTelefone == numero).Take(2).CountAsync();
}
