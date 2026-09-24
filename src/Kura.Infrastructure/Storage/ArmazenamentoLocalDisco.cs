namespace Kura.Infrastructure.Storage;

using System.Text.RegularExpressions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Implementação de <see cref="IArmazenamentoArquivos"/> que grava no disco local, sob
/// <c>Storage:BasePath</c> (a mesma config que <c>ReceituarioPdfService</c> usa —
/// <c>src/Kura.Application/Services/ReceituarioPdfService.cs:40</c>, env
/// <c>Storage__BasePath</c>). FT-02/backlog <c>KURA_BACKLOG_FOTO_PET.md</c>, regra A3.
///
/// <para>Chave é sempre relativa (ex.: <c>clinica/7/pet/12/{uuid}_256.webp</c>) e é resolvida
/// para caminho absoluto só no momento de tocar o disco — nunca persistida como caminho
/// absoluto (diferente de <c>Documento.DsCaminho</c>, que guarda caminho absoluto — desvio
/// documentado no G0 item 8.3, não repetido aqui).</para>
///
/// <para>A defesa de path traversal (resolver caminho absoluto e conferir prefixo contra a
/// base) é extraída de <c>ReceituarioPdfService.cs:110-118</c> — mesma lógica, chave nova.
/// <c>ReceituarioPdfService</c> não foi tocado (migrar ele para esta interface é a FT-14,
/// fora do escopo desta task).</para>
/// </summary>
public sealed class ArmazenamentoLocalDisco : IArmazenamentoArquivos
{
    // Chave só pode conter estes caracteres. Barra é separador de subdiretório (permitido
    // de propósito — a chave carrega estrutura, ex. "clinica/7/pet/12/uuid_256.webp");
    // ".." é bloqueado separadamente abaixo (uma chave como "a..b" contém ".." como
    // substring mas não é traversal — por isso o regex sozinho não basta e a checagem de
    // segmento ".." vem depois, não combinada num único regex).
    private static readonly Regex CaracteresPermitidos = new(
        "^[A-Za-z0-9/_.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _storageBasePath;

    public ArmazenamentoLocalDisco(IConfiguration configuration)
    {
        _storageBasePath = configuration["Storage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage", "documentos");
    }

    public async Task SalvarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct)
    {
        var caminho = ResolverCaminhoSeguro(chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);

        await using var arquivo = new FileStream(
            caminho, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await conteudo.CopyToAsync(arquivo, ct);
    }

    public async Task<Stream?> AbrirAsync(string chave, CancellationToken ct)
    {
        var caminho = ResolverCaminhoSeguro(chave);

        if (!File.Exists(caminho))
            return null;

        var bytes = await File.ReadAllBytesAsync(caminho, ct);
        return new MemoryStream(bytes, writable: false);
    }

    public Task ExcluirAsync(string chave, CancellationToken ct)
    {
        var caminho = ResolverCaminhoSeguro(chave);

        // Idempotente por desenho (regra do backlog): excluir o que já não existe não lança.
        if (File.Exists(caminho))
            File.Delete(caminho);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Valida o FORMATO da chave (vazia, caractere fora da allowlist, ".." como segmento) e
    /// resolve o caminho absoluto, confirmando que ele cai dentro de
    /// <see cref="_storageBasePath"/> — mesma defesa de
    /// <c>ReceituarioPdfService.cs:110-118</c> (<c>Path.GetFullPath</c> + <c>StartsWith</c>
    /// da base com separador final, para não deixar <c>/data/kura/receituarios-outracoisa</c>
    /// passar por ter o mesmo prefixo textual de <c>/data/kura/receituarios</c>).
    /// </summary>
    private string ResolverCaminhoSeguro(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ArgumentException("Chave de armazenamento não pode ser vazia.", nameof(chave));

        if (Path.IsPathRooted(chave))
            throw new ArgumentException($"Chave de armazenamento não pode ser absoluta: '{chave}'.", nameof(chave));

        if (!CaracteresPermitidos.IsMatch(chave))
            throw new ArgumentException(
                $"Chave de armazenamento contém caractere não permitido: '{chave}'.", nameof(chave));

        if (chave.Split('/').Any(segmento => segmento == ".."))
            throw new ArgumentException(
                $"Chave de armazenamento não pode conter segmento '..': '{chave}'.", nameof(chave));

        var caminhoResolvido = Path.GetFullPath(Path.Combine(_storageBasePath, chave));
        var baseResolvida = Path.GetFullPath(_storageBasePath) + Path.DirectorySeparatorChar;

        if (!caminhoResolvido.StartsWith(baseResolvida, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Chave de armazenamento resolve para fora da base configurada: '{chave}'.", nameof(chave));

        return caminhoResolvido;
    }
}
