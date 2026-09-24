namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// FT-03 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — aceite 4 (corpo acima do limite → 413).
///
/// <para><b>Por que este teste existe, isolado do endpoint real.</b> O brief da task
/// sinalizou 2 armadilhas a MEDIR, não presumir: (a) <c>TestServer</c> pode não aplicar
/// <c>[RequestSizeLimit]</c> como o Kestrel real aplicaria; (b) o
/// <see cref="Microsoft.AspNetCore.Http.BadHttpRequestException"/> que o Kestrel lança ao
/// estourar o limite podia ser convertida em 500 pelo <see cref="ExceptionHandlerMiddleware"/>
/// — MEDIDO nesta task: antes do case novo no switch, o resultado abaixo seria 500 (cai no
/// <c>default</c>), não 413. Este teste isola exatamente essa conversão, sem depender de
/// <c>TestServer</c> de fato truncar um corpo de requisição (ver <c>PetFotoHttpTests</c> para
/// o que foi medido — e o que NÃO foi possível medir — sobre o Kestrel real via HTTP).</para>
/// </summary>
public class ExceptionHandlerMiddlewareTamanhoTests
{
    private sealed class LoggerNulo : ILogger<ExceptionHandlerMiddleware>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task BadHttpRequestException_ComStatusCode413_MiddlewareDevolve413NaoDevolve500()
    {
        // Arrange — simula exatamente o que o Kestrel lança ao estourar
        // [RequestSizeLimit]: BadHttpRequestException com StatusCode já preenchido.
        Task Next(HttpContext _) =>
            throw new Microsoft.AspNetCore.Http.BadHttpRequestException(
                "Request body too large.", StatusCodes.Status413PayloadTooLarge);

        var middleware = new ExceptionHandlerMiddleware(Next, new LoggerNulo());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/pets/1/foto";
        httpContext.Request.Method = "POST";
        httpContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge,
            "o switch do middleware precisa ler BadHttpRequestException.StatusCode em vez de " +
            "cair no default (500) — MEDIDO: sem o case novo, este teste falhava com 500");
    }

    [Fact]
    public async Task BadHttpRequestException_ComOutroStatusCode_MiddlewareRespeitaOStatusDaExcecao()
    {
        // Controle: a exceção também é usada pelo Kestrel para outros erros de parsing HTTP
        // (ex.: 400 de request malformada) — o case não deve fixar 413 sempre, e sim ler o
        // StatusCode real da exceção.
        Task Next(HttpContext _) =>
            throw new Microsoft.AspNetCore.Http.BadHttpRequestException(
                "Malformed request.", StatusCodes.Status400BadRequest);

        var middleware = new ExceptionHandlerMiddleware(Next, new LoggerNulo());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/pets/1/foto";
        httpContext.Request.Method = "POST";
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
