namespace Kura.Infrastructure.Mongo;

/// <summary>Seção <c>Mongo</c> da configuração (env <c>Mongo__ConnectionString</c> etc.).</summary>
public sealed class MongoOptions
{
    public const string Secao = "Mongo";
    /// <summary>Teto do timeout de seleção de servidor: o health e as rotas não podem pendurar.</summary>
    public const int TimeoutMaximoSegundos = 3;

    /// <summary>Vazia/ausente = Mongo não configurado (a API sobe; laudos ficam indisponíveis).</summary>
    public string? ConnectionString { get; set; }
    public string Database { get; set; } = "kura_docs";
    /// <summary>Se false, o <see cref="MongoSchemaRunner"/> não toca no banco.</summary>
    public bool InitEnabled { get; set; } = true;
    public int ServerSelectionTimeoutSeconds { get; set; } = TimeoutMaximoSegundos;

    public bool Configurado => !string.IsNullOrWhiteSpace(ConnectionString);

    public TimeSpan TimeoutSelecao => TimeSpan.FromSeconds(
        Math.Clamp(ServerSelectionTimeoutSeconds, 1, TimeoutMaximoSegundos));
}
