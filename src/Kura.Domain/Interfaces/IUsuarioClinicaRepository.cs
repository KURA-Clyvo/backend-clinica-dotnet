namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

/// <summary>
/// Acesso a <see cref="UsuarioClinica"/> (FD-03, ciclo FIN; ampliado pela FD-04).
///
/// <para>
/// 🔴 <b>Todo método desta interface escreve o predicado de tenant À MÃO e ignora os query
/// filters.</b> O filtro de <c>USUARIO_CLINICA</c> em <c>KuraDbContext.ApplyTenantFilters</c>
/// <b>DESLIGA INTEIRO</b> quando <c>IdClinicaFiltro</c> é null (não nega), então depender
/// dele aqui faria o resultado destas consultas variar conforme estado ambiente que o
/// chamador não controla. Com a clínica recebida por parâmetro, o mesmo argumento produz o
/// mesmo resultado com ou sem JWT no header — e o isolamento vira algo que um teste pode
/// mutar e ver quebrar, em vez de um efeito colateral de configuração.
/// </para>
/// </summary>
public interface IUsuarioClinicaRepository : IRepository<UsuarioClinica>
{
    /// <summary>
    /// Todos os usuários ATIVOS com este e-mail, em TODAS as clínicas.
    ///
    /// <para>⚠️ <b>Devolve coleção, e não um único usuário, de propósito.</b> A UK da V17 é
    /// <c>(ID_CLINICA, DS_EMAIL)</c> — e-mail é único POR CLÍNICA, não globalmente —, então
    /// "o usuário deste e-mail" não é uma pergunta com resposta única. Quem chama tem que
    /// decidir explicitamente o que fazer quando vier mais de um; devolver
    /// <c>UsuarioClinica?</c> aqui esconderia essa decisão dentro de um
    /// <c>FirstOrDefault()</c>, que é exatamente a forma de escolha arbitrária e silenciosa
    /// de tenant que a FD-03 existe para eliminar.</para>
    ///
    /// <para><b>A busca ignora os query filters e escreve o predicado inteiro à mão</b> —
    /// ver a implementação para o argumento medido.</para>
    /// </summary>
    Task<IReadOnlyList<UsuarioClinica>> BuscarAtivosPorEmailAsync(string email);

    /// <summary>
    /// FD-04 — usuários de uma clínica, ordenados por e-mail (ordem estável para listagem e
    /// para asserção de teste).
    ///
    /// <para>
    /// FD-16 — <c>incluirInativos=false</c> (default) preserva o comportamento anterior: só
    /// os ATIVOS. <c>true</c> inclui também os desativados, sem abrir mão do escopo de
    /// clínica — o flag liga a inclusão de inativos, não desliga o isolamento de tenant.
    /// Mesmo achado e mesmo motivo de <c>IServicoPrecoRepository.ListarDaClinicaAsync</c>:
    /// <c>POST /{id}/reativacao</c> sempre existiu, mas a listagem nunca devolvia o que
    /// precisaria ser reativado.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UsuarioClinica>> ListarDaClinicaAsync(
        long idClinica, bool incluirInativos = false);

    /// <summary>
    /// FD-04 — um usuário pelo par (id, clínica), <b>incluindo desativados</b>.
    ///
    /// <para>A clínica entra no predicado, e não como conferência posterior: um id de outro
    /// tenant devolve <c>null</c>, que o service traduz em <c>404</c> — a resposta é
    /// indistinguível de "não existe", então o endpoint não vira oráculo de existência de id
    /// alheio.</para>
    ///
    /// <para>Inclui desativados porque um GESTOR administrando usuários precisa enxergar
    /// quem ele desativou; o estado vai no <c>ST_ATIVA</c> da resposta.</para>
    /// </summary>
    Task<UsuarioClinica?> BuscarPorIdNaClinicaAsync(long id, long idClinica);

    /// <summary>
    /// FD-04 — usuário pelo par (clínica, e-mail), <b>incluindo desativados</b>. É a checagem
    /// explícita de <c>UK_USUARIO_CLINICA_EMAIL</c>.
    ///
    /// <para>🔴 <b>Incluir desativados não é zelo, é correção.</b> Soft delete é a regra do
    /// projeto: <c>ST_ATIVA='N'</c> mantém a LINHA no Oracle, e a linha continua ocupando a
    /// unique key. Uma checagem que só olhasse ativos aprovaria o e-mail e o
    /// <c>INSERT</c> morreria com <c>ORA-00001</c> — <c>500</c> de constraint, que é
    /// exatamente o que esta task tem de evitar. O provider InMemory da suíte <b>não valida
    /// índice único</b>, então nenhum teste desta suíte pegaria o erro no INSERT: a única
    /// defesa é este predicado.</para>
    /// </summary>
    /// <param name="excetoId">
    /// Quando informado, ignora essa linha na busca. Existe para a REATIVAÇÃO (fix wave
    /// pós-G2 da FD-04): ao reativar, o próprio usuário desativado é o dono do e-mail, e um
    /// <c>FirstOrDefault</c> poderia devolvê-lo — fazendo o usuário colidir CONSIGO MESMO e
    /// tornando a reativação impossível. A pergunta que a reativação precisa fazer é "existe
    /// OUTRO com este e-mail", e ela tem de estar escrita no predicado, não resolvida depois
    /// por comparação de id sobre um resultado de ordem indefinida.
    /// </param>
    Task<UsuarioClinica?> BuscarPorEmailNaClinicaAsync(
        long idClinica, string email, long? excetoId = null);

    /// <summary>
    /// Como <see cref="BuscarPorEmailNaClinicaAsync"/>, mas só devolve usuário <b>ATIVO</b>
    /// (S4-NET-03, G2-m2: token ainda válido de usuário desativado não pode criar laudo).
    /// Método próprio de propósito: o outro precisa enxergar o desativado (UK do e-mail).
    /// </summary>
    Task<UsuarioClinica?> BuscarAtivoPorEmailNaClinicaAsync(long idClinica, string email);

    /// <summary>
    /// FD-04 — quantos <c>GESTOR</c> ATIVOS a clínica tem, <b>excluindo</b> opcionalmente um
    /// id (o usuário que a operação em curso está prestes a rebaixar ou desativar).
    ///
    /// <para>É o instrumento do invariante "nenhuma operação pode deixar a clínica com zero
    /// GESTOR ativo" — ver <c>UsuarioClinicaService</c> para o argumento da decisão.</para>
    /// </summary>
    Task<int> ContarGestoresAtivosAsync(long idClinica, long? excetoId = null);

    /// <summary>
    /// 🔴 <b>FD-13 — trava (lock pessimista) TODAS as linhas de GESTOR ativo da clínica até o
    /// fim da transação em curso.</b> Tem de ser chamado <b>antes</b> de
    /// <see cref="ContarGestoresAtivosAsync"/> quando a contagem for usada para DECIDIR uma
    /// escrita, e <b>dentro</b> de uma transação — sem transação, o lock é liberado no fim do
    /// próprio statement e não protege nada.
    ///
    /// <para><b>O defeito que ele fecha, medido:</b> duas desativações concorrentes de
    /// gestores <b>diferentes</b> da mesma clínica passavam as duas (<c>204</c> + <c>204</c>)
    /// e deixavam a clínica com <b>ZERO gestor ativo</b> — 3/3 reproduções contra Oracle real.
    /// Serialmente a segunda dá <c>422</c>, ou seja, a regra existe: o buraco é a janela entre
    /// CONTAR e GRAVAR.</para>
    ///
    /// <para><b>Por que lock pessimista, e não as alternativas mais baratas</b> (as 4 já foram
    /// avaliadas e recusadas na FD-04): <c>NR_VERSION</c>/optimistic locking não se aplica
    /// porque as duas escritas concorrentes tocam <b>linhas diferentes</b> — não há versão em
    /// disputa; transação sozinha não fecha nada sob <c>READ COMMITTED</c>, que é o default do
    /// Oracle e dá snapshot por STATEMENT (as duas contagens veem 2 gestores e as duas
    /// passam); e revalidar no <c>SaveChanges</c> repete a mesma leitura na mesma janela cega.
    /// A serialização precisa de um recurso COMUM às duas transações — e o único recurso comum
    /// aqui é o próprio conjunto de gestores.</para>
    ///
    /// <para><b>Por que o conjunto INTEIRO, sem excluir o alvo.</b> É o que faz as duas
    /// transações colidirem: A (alvo 100) e B (alvo 107) travam o MESMO conjunto
    /// <c>{100, 107}</c>, então uma bloqueia a outra. Se cada uma travasse só "os outros", os
    /// conjuntos seriam disjuntos e ninguém bloquearia ninguém — o lock existiria e não
    /// serializaria nada.</para>
    ///
    /// <para><b>Deadlock:</b> as duas sessões executam o statement IDÊNTICO, com o mesmo plano
    /// e o mesmo predicado, logo travam as linhas na mesma ordem de varredura. Deadlock exige
    /// ordens diferentes.</para>
    ///
    /// <para>⚠️ <b>Degrada para no-op quando o provider não é relacional</b> (InMemory da
    /// suíte), porque SQL cru não existe lá — e um <c>if</c> no service para desviar disso
    /// faria o teste exercitar um caminho que produção não usa. A consequência é declarada e
    /// não tem contorno: <b>nenhum teste desta suíte prova a serialização</b>; a prova é
    /// medição contra Oracle real, registrada no relatório da FD-13.</para>
    /// </summary>
    Task BloquearGestoresAtivosAsync(long idClinica);

}
