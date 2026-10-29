namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;

public class UnitOfWork : IUnitOfWork
{
    private readonly KuraDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(KuraDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflitoConcorrenciaException();
        }
        catch (DbUpdateException ex) when (EhRotinaOracleIndisponivel(ex))
        {
            // S4-DB-06: o INSERT de COBRANCA dispara a trigger de auditoria (S4-DB-03); trigger ou
            // package inválido falha aqui com ORA-04063/04098 e viraria 500. Só estes códigos
            // mudam; qualquer outro DbUpdateException segue como estava.
            throw new RotinaOracleIndisponivelException(ex);
        }
    }

    private static bool EhRotinaOracleIndisponivel(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is OracleException oracle)
                return RotinasOracle.ErrosRotinaOracle.IndisponivelNoInsert(oracle.Number);
        }
        return false;
    }

    public async Task BeginTransactionAsync()
    {
        // Nota (TASK-30): o provider EF Core InMemory (usado nos testes deste
        // projeto) não implementa transações relacionais — Database.BeginTransactionAsync
        // lança InvalidOperationException nele. Este método só é exercitado de fato
        // contra um provider relacional (Oracle em produção). Os testes de
        // orquestração do AuthService usam um IUnitOfWork fake, não este.
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>Database.IsRelational()</c> é a pergunta certa <b>para o que este método promete</b>
    /// (e não <c>is InMemoryDatabase</c>): ela é sobre a CAPACIDADE de abrir <b>transação</b>,
    /// não sobre o nome de um provider. Provider não relacional novo degrada sozinho.
    ///
    /// <para>🔴 <b>O que ela NÃO garante</b> — corrigido pela revisão G2 da FD-13, que pegou
    /// esta documentação prometendo o que o código não entrega: um provider relacional novo
    /// <b>não</b> passa a valer automaticamente para o invariante do último gestor. Aquele
    /// caminho precisa de <b>duas</b> capacidades, e este método só responde por uma. O lock de
    /// <c>UsuarioClinicaRepository.BloquearGestoresAtivosAsync</c> é <c>SELECT … FOR UPDATE</c>,
    /// sintaxe que o <b>SQLite não implementa</b> (ele não tem lock de linha). Um teste futuro
    /// em SQLite receberia <c>true</c> daqui, <b>pareceria</b> exercitar a serialização e não
    /// exercitaria — falso verde, que é pior que a degradação honesta do InMemory. <b>Trocar o
    /// provider de teste exige reescrever o SQL do lock, não só trocar o provider.</b></para>
    /// </remarks>
    public async Task<bool> TryBeginTransactionAsync()
    {
        if (!_context.Database.IsRelational())
            return false;

        _transaction = await _context.Database.BeginTransactionAsync();
        return true;
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction is null)
            return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        // DbContext lifetime is otherwise managed by DI (scoped) — no-op beyond the transaction.
    }
}
