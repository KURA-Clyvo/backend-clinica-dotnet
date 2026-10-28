namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class UsuarioClinicaRepository : Repository<UsuarioClinica>, IUsuarioClinicaRepository
{
    public UsuarioClinicaRepository(KuraDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>Por que <c>IgnoreQueryFilters()</c> aqui, e por que o predicado repete
    /// <c>StAtiva</c> à mão.</b> O filtro de tenant de <c>USUARIO_CLINICA</c>
    /// (<c>KuraDbContext.ApplyTenantFilters</c>) é
    /// <c>StAtiva &amp;&amp; (IdClinicaFiltro == null || IdClinica == IdClinicaFiltro)</c>:
    /// ele <b>DESLIGA INTEIRO</b> quando não há clínica no contexto — não nega. Depender
    /// dele aqui deixaria o resultado desta consulta dependente de estado ambiente que o
    /// login <b>não controla</b>.</para>
    ///
    /// <para>E esse estado ambiente existe de verdade: <c>POST /api/v1/auth/login</c> é
    /// <c>[AllowAnonymous]</c>, mas <c>UseAuthentication()</c> valida e popula
    /// <c>HttpContext.User</c> mesmo assim quando o cliente manda um <c>Authorization</c>
    /// válido. Um app que ainda tenha o token da sessão anterior no header (trocar de conta
    /// sem limpar o header é o caso trivial) faria <c>IdClinicaFiltro</c> ser NÃO nulo
    /// durante o próprio login, e a busca por e-mail seria silenciosamente escopada na
    /// clínica ERRADA — o usuário certo, com a senha certa, receberia "Email ou senha
    /// inválidos.". <c>IgnoreQueryFilters()</c> torna esta consulta determinística: mesmo
    /// resultado com ou sem JWT no header.</para>
    ///
    /// <para>A contrapartida é que o <c>StAtiva</c> que vinha embutido no filtro some junto,
    /// então ele é reescrito no <c>Where</c> — soft delete é regra do projeto (DELETE físico
    /// nunca acontece), e um usuário desativado não pode autenticar.</para>
    ///
    /// <para><c>OrderBy(IdClinica)</c> não escolhe nada — a decisão sobre N&gt;1 é do
    /// <c>AuthService</c>, que falha explicitamente. A ordenação existe só para a lista ser
    /// determinística em mensagens de diagnóstico e em teste.</para>
    /// </remarks>
    public async Task<IReadOnlyList<UsuarioClinica>> BuscarAtivosPorEmailAsync(string email) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(u => u.DsEmail == email && u.StAtiva)
            .OrderBy(u => u.IdClinica)
            .ToListAsync();

    // ── FD-04 ────────────────────────────────────────────────────────────────────────────
    // As 4 consultas abaixo seguem a MESMA disciplina de BuscarAtivosPorEmailAsync, e pelo
    // mesmo motivo, não por simetria estética: IgnoreQueryFilters() + predicado de tenant
    // escrito à mão. O query filter desta entidade desliga inteiro quando IdClinicaFiltro é
    // null, então uma consulta que dependesse dele daria resultado DIFERENTE conforme houvesse
    // ou não JWT no contexto — e o CRUD de usuários é justamente onde essa variação vira
    // vazamento cross-tenant ou 404 falso. Aqui a clínica é ARGUMENTO: o chamador (o service,
    // lendo IClinicaContext.IdClinica) é quem responde "de quem é esta requisição", e a
    // resposta fica visível no call site em vez de embutida no modelo.
    //
    // Efeito colateral desejado: o isolamento passa a ser MUTÁVEL por teste. Trocar
    // `u.IdClinica == idClinica` por `true` quebra UsuarioClinicaServiceTests; um isolamento
    // que só existisse via query filter continuaria verde com o filtro removido sempre que o
    // contexto estivesse vazio.

    /// <inheritdoc />
    /// <remarks>
    /// FD-16 — <c>(incluirInativos || u.StAtiva)</c> vai DENTRO do <c>Where</c> traduzido
    /// para SQL, não filtrado depois em memória.
    /// </remarks>
    public async Task<IReadOnlyList<UsuarioClinica>> ListarDaClinicaAsync(
        long idClinica, bool incluirInativos = false) =>
        await _dbSet
            .IgnoreQueryFilters()
            .Where(u => u.IdClinica == idClinica && (incluirInativos || u.StAtiva))
            .OrderBy(u => u.DsEmail)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<UsuarioClinica?> BuscarPorIdNaClinicaAsync(long id, long idClinica) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.IdClinica == idClinica);

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <b>Sem <c>u.StAtiva</c> no predicado, e isso é a diferença entre <c>422</c> tratado e
    /// <c>ORA-00001</c>.</b> O soft delete do projeto não apaga a linha, e a linha desativada
    /// continua ocupando <c>UK_USUARIO_CLINICA_EMAIL (ID_CLINICA, DS_EMAIL)</c> no Oracle.
    /// Filtrar por ativo aqui aprovaria o e-mail reutilizado e explodiria no <c>INSERT</c> —
    /// invisível para esta suíte, porque o provider InMemory não valida índice único.
    /// </remarks>
    public async Task<UsuarioClinica?> BuscarPorEmailNaClinicaAsync(
        long idClinica, string email, long? excetoId = null) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IdClinica == idClinica
                                   && u.DsEmail == email
                                   && (excetoId == null || u.Id != excetoId));

    /// <inheritdoc />
    public async Task<UsuarioClinica?> BuscarAtivoPorEmailNaClinicaAsync(long idClinica, string email) =>
        await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.IdClinica == idClinica && u.DsEmail == email && u.StAtiva);

    /// <inheritdoc />
    public async Task<int> ContarGestoresAtivosAsync(long idClinica, long? excetoId = null) =>
        await _dbSet
            .IgnoreQueryFilters()
            .CountAsync(u => u.IdClinica == idClinica
                          && u.StAtiva
                          && u.TpPerfil == PerfisUsuarioClinica.Gestor
                          && (excetoId == null || u.Id != excetoId));

    /// <inheritdoc />
    /// <remarks>
    /// <para><b>SQL cru, e não LINQ, porque o EF Core não sabe emitir <c>FOR UPDATE</c>.</b>
    /// Não há tradução de lock pessimista no provider Oracle — nem
    /// <c>IsolationLevel.Serializable</c> resolveria, porque Oracle implementa
    /// <c>SERIALIZABLE</c> com snapshot: a segunda transação não bloquearia, ela morreria com
    /// <c>ORA-08177</c> depois, virando <c>500</c> em vez do <c>422</c> correto.</para>
    ///
    /// <para><b><c>ExecuteSqlRawAsync</c>, e não <c>SqlQueryRaw</c>/<c>FromSqlRaw</c>:</b> as
    /// duas últimas podem envolver o SQL num <c>SELECT ... FROM (&lt;sql&gt;)</c>, e Oracle
    /// não aceita <c>FOR UPDATE</c> dentro de inline view. <c>ExecuteSqlRawAsync</c> manda o
    /// texto literal. O <c>SELECT ... FOR UPDATE</c> trava TODAS as linhas do resultado no
    /// momento da EXECUÇÃO do statement (antes de qualquer fetch) — por isso executar já
    /// basta, e o retorno <c>-1</c> de <c>ExecuteNonQuery</c> não significa "nada aconteceu".
    /// Isto foi <b>medido</b>, não deduzido: ver a seção de prova do relatório da FD-13.</para>
    ///
    /// <para><c>'S'</c> aparece literal porque é o que a coluna guarda
    /// (<c>BoolToSimNaoConverter</c>) e o conversor do EF não participa de SQL cru; o perfil
    /// vai por parâmetro, ancorado em <see cref="PerfisUsuarioClinica.Gestor"/>, para não
    /// duplicar a constante como string solta.</para>
    ///
    /// <para>⚠️ <b>Se a clínica já não tem NENHUM gestor ativo, isto trava zero linha</b> e não
    /// serializa nada — o que é inofensivo, porque nesse caso a contagem seguinte devolve 0 e
    /// o invariante recusa de qualquer jeito.</para>
    /// </remarks>
    public async Task BloquearGestoresAtivosAsync(long idClinica)
    {
        // Ver o <remarks> da interface: sem provider relacional não há SQL cru nem transação,
        // então aqui não há lock — e o service continua com a checagem não serializada, que é
        // exatamente o comportamento pré-FD-13. Silencioso de propósito: a alternativa
        // (lançar) quebraria as 650 suítes InMemory sem tornar nenhuma delas mais verdadeira.
        if (!_context.Database.IsRelational())
            return;

        await _context.Database.ExecuteSqlRawAsync(
            """
            SELECT ID_USUARIO_CLINICA
              FROM USUARIO_CLINICA
             WHERE ID_CLINICA = {0}
               AND TP_PERFIL = {1}
               AND ST_ATIVA = 'S'
               FOR UPDATE
            """,
            idClinica,
            PerfisUsuarioClinica.Gestor);
    }
}
