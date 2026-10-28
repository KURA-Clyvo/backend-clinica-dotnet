namespace Kura.IntegrationTests;

using System.Collections.Concurrent;
using Kura.Domain.Interfaces;
using Kura.Domain.Laudos;

/// <summary>
/// Repositório de laudos EM MEMÓRIA, escopado por <c>idClinica</c> como o de Mongo: toda leitura
/// e escrita filtra por clínica (e por <c>StAtivo</c>). Existe para provar rota, autorização,
/// paginação e <c>_links</c> pelo HTTP sem mongod; o comportamento do repositório REAL é coberto
/// por <c>LaudoRepositoryTests</c> (Mongo2Go). Id que não é ObjectId ⇒ "não achei", como o real.
/// </summary>
public sealed class LaudoRepositoryFake : ILaudoRepository
{
    private readonly ConcurrentDictionary<string, Laudo> _docs = new();
    private int _seq;

    /// <summary>Se preenchido, o próximo acesso lança esta exceção (e a limpa só quando o teste pedir).</summary>
    public volatile Exception? FalhaDeAcesso;

    private void Acessar()
    {
        if (FalhaDeAcesso is { } f) throw f;
    }

    private static bool IdValido(string id) =>
        id.Length == 24 && id.All(Uri.IsHexDigit);

    public Task<Laudo?> ObterAsync(long idClinica, string id, CancellationToken ct = default)
    {
        Acessar();
        if (!IdValido(id)) return Task.FromResult<Laudo?>(null);
        // A CHECAGEM DE CLÍNICA mora na linha abaixo (alvo da mutação de controle da S4-NET-03).
        return Task.FromResult(_docs.TryGetValue(id, out var l) && l.IdClinica == idClinica && l.StAtivo ? l : null);
    }

    public Task<PaginaLaudos> ListarPorPetAsync(long idClinica, long idPet, ConsultaLaudos c, CancellationToken ct = default)
    {
        Acessar();
        var q = _docs.Values.Where(l => l.IdClinica == idClinica && l.IdPet == idPet && l.StAtivo);
        if (c.Tipo is { } t) q = q.Where(l => l.Tipo == t);
        if (c.DataDe is { } de) q = q.Where(l => l.DataExame >= de);
        if (c.DataAte is { } ate) q = q.Where(l => l.DataExame <= ate);
        q = c.Ordem switch
        {
            OrdenacaoLaudo.DataExameAsc => q.OrderBy(l => l.DataExame).ThenBy(l => l.Id),
            OrdenacaoLaudo.CriadoEmAsc => q.OrderBy(l => l.CriadoEm).ThenBy(l => l.Id),
            OrdenacaoLaudo.CriadoEmDesc => q.OrderByDescending(l => l.CriadoEm).ThenByDescending(l => l.Id),
            _ => q.OrderByDescending(l => l.DataExame).ThenByDescending(l => l.Id),
        };
        var todos = q.ToList();
        var pagina = todos.Skip((c.Pagina - 1) * c.TamanhoPagina).Take(c.TamanhoPagina).ToList();
        return Task.FromResult(new PaginaLaudos(pagina, c.Pagina, c.TamanhoPagina, todos.Count));
    }

    public Task<Laudo> InserirAsync(long idClinica, Laudo laudo, CancellationToken ct = default)
    {
        Acessar();
        if (laudo.IdClinica != idClinica) throw new ArgumentException("clinica difere", nameof(laudo));
        laudo.Id = Interlocked.Increment(ref _seq).ToString("x24");
        laudo.StAtivo = true;
        laudo.CriadoEm = laudo.AtualizadoEm = DateTime.UtcNow.AddMilliseconds(_seq);
        _docs[laudo.Id] = laudo;
        return Task.FromResult(laudo);
    }

    public Task<bool> ExcluirLogicoAsync(long idClinica, string id, CancellationToken ct = default)
    {
        Acessar();
        if (!IdValido(id) || !_docs.TryGetValue(id, out var l) || l.IdClinica != idClinica || !l.StAtivo)
            return Task.FromResult(false);
        l.StAtivo = false;
        l.RemovidoEm = DateTime.UtcNow;
        return Task.FromResult(true);
    }
}

/// <summary>Armazenamento de arquivos em memória; <see cref="Quantidade"/> prova "nada gravado".</summary>
public sealed class ArmazenamentoEmMemoria : IArmazenamentoArquivos
{
    private readonly ConcurrentDictionary<string, byte[]> _arquivos = new();

    public int Quantidade => _arquivos.Count;
    public IReadOnlyCollection<string> Chaves => _arquivos.Keys.ToList();

    public async Task SalvarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await conteudo.CopyToAsync(ms, ct);
        _arquivos[chave] = ms.ToArray();
    }

    public Task<Stream?> AbrirAsync(string chave, CancellationToken ct) =>
        Task.FromResult<Stream?>(_arquivos.TryGetValue(chave, out var b) ? new MemoryStream(b) : null);

    public Task ExcluirAsync(string chave, CancellationToken ct)
    {
        _arquivos.TryRemove(chave, out _);
        return Task.CompletedTask;
    }
}
