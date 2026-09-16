namespace Kura.Api.Extensions;

using Kura.Api.Middlewares;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// LU-16 G4 (achado A4, BLOQUEANTE): enricher global do Serilog que redige qualquer
/// propriedade estruturada <c>RequestPath</c> ou <c>Path</c> de QUALQUER
/// <see cref="LogEvent"/>, reaproveitando <see cref="ExceptionHandlerMiddleware.RedigirPathSensivel(string?)"/>
/// — a mesma regra que já protegia o caminho de exceção desde a TASK-67, sem duplicá-la.
///
/// Por que um enricher GLOBAL, e não só customizar o <c>MessageTemplate</c> de
/// <c>UseSerilogRequestLogging</c> (a primeira tentativa desta task): o vazamento medido
/// pelo G4 tinha 4 gravações por UMA chamada, e só uma delas é a linha de conclusão do
/// Serilog.AspNetCore. As outras vinham de <c>Microsoft.AspNetCore.Hosting.Diagnostics</c>
/// ("Request starting"/"Request finished", resolvidas suprimindo a categoria em
/// <c>Program.cs</c>) e de uma quinta fonte achada ao ESCREVER o teste desta task, não
/// prevista no brief: o ASP.NET Core hosting empurra uma logging SCOPE ambiente
/// (<c>HostingLogScope</c>, via <c>ILogger.BeginScope</c>) contendo <c>RequestPath</c> que
/// envolve TODA a requisição — ou seja, QUALQUER log emitido durante o processamento
/// (inclusive "Executing endpoint '{EndpointName}'", nome de rota, sem o telefone no
/// texto — mas com o telefone na propriedade estruturada) carrega essa propriedade. Um
/// sink de texto simples (Console/File com o template padrão, o que este projeto usa)
/// não a renderiza hoje — mas ela existe no <see cref="LogEvent"/>, e um sink estruturado
/// futuro (JSON/CLEF/Seq/Elasticsearch) a exportaria com o telefone cru. Um enricher roda
/// depois que toda propriedade — de template, de diagnostic context E de scope — já foi
/// anexada ao evento, então é o ÚNICO ponto que cobre as três origens de uma vez.
/// </summary>
public sealed class RedigirRequestPathEnricher : ILogEventEnricher
{
    private static readonly string[] NomesDePropriedade = ["RequestPath", "Path"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var nome in NomesDePropriedade)
        {
            if (!logEvent.Properties.TryGetValue(nome, out var valorAtual))
                continue;

            if (valorAtual is not ScalarValue { Value: string original })
                continue;

            var redigido = ExceptionHandlerMiddleware.RedigirPathSensivel(original);
            if (!string.Equals(redigido, original, StringComparison.Ordinal))
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(nome, redigido));
        }
    }
}
