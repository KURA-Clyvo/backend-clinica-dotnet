namespace Kura.IntegrationTests;

using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// FT-03 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — fix wave G2 (<c>g2-ft03.md</c>, achado
/// G2-c) — prova o aceite 4 (corpo acima do limite → 413) com <b>KESTREL REAL</b>, não
/// <c>TestServer</c>. 🔴 Fix wave 2 (re-G2, <c>g2b-ft03.md</c>, achados re-G2-1/re-G2-2): o
/// mecanismo do 413 mudou de um factory GLOBAL (<c>Program.cs</c>, casava por substring) para
/// <see cref="Kura.Api.Filters.DesabilitaFormValueProvidersAttribute"/>, local à action — esta
/// classe ganhou o caso <c>chunked</c> (que antes só existia como sonda da re-G2) e o teste do
/// spoof (a mesma frase que disparava 413 numa rota qualquer, achado re-G2-1) virou permanente.
///
/// <para><b>Por que uma classe separada, com Kestrel de verdade.</b>
/// <c>PetFotoHttpTests.UploadFoto_CorpoAcimaDoLimite_SobTestServerNaoEhRejeitado_MedidoNaoPresumido</c>
/// já documenta que <c>TestServer</c> NÃO implementa <c>IHttpMaxRequestBodySizeFeature</c> como
/// o Kestrel real — sob <c>TestServer</c> um corpo de 3 MB é aceito com 200. O .NET 10 tem
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.UseKestrel"/>,
/// que sobe a MESMA <see cref="KuraApiFactory"/> (InMemory, sem Oracle, sem container) num
/// socket TCP real — dá para medir o comportamento de verdade sem subir o compose.</para>
///
/// <para><b>Por que socket cru, não <see cref="HttpClient"/>, para o caso &gt; 2 MB.</b>
/// Medido nesta fix wave (e na sonda da G2): mandar 3 MB inteiros via
/// <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> faz o Kestrel fechar a conexão NO
/// MEIO do upload assim que detecta que o <c>Content-Length</c> declarado estoura o limite —
/// o cliente HTTP recebe uma exceção de conexão resetada, não uma resposta HTTP limpa. Um
/// socket cru que declara <c>Content-Length: 3145728</c> e escreve só o INÍCIO do corpo
/// consegue ler a resposta 413 antes de a conexão cair.</para>
/// </summary>
[Trait(ConvencaoDeTestes.Categoria, ConvencaoDeTestes.Integracao)]
public class PetFotoKestrelHttpTests
{
    private static readonly string CRLF = new([(char)13, (char)10]);
    private static readonly byte[] WebpValido =
        [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray()];

    private static async Task<(KuraApiFactory Factory, long IdPet, string Token)> SubirComKestrelAsync()
    {
        var factory = new KuraApiFactory();
        factory.UseKestrel(0); // porta 0 = o SO escolhe uma porta livre
        factory.StartServer();

        long idPet;
        using (var escopo = factory.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<KuraDbContext>();
            var pet = new Pet
            {
                IdClinica = KuraApiFactory.IdClinicaSemeada,
                IdEspecie = 1,
                IdRaca = 1,
                NmPet = "Rex Kestrel G2-c",
                DtNascimento = new DateTime(2022, 1, 1),
                SgSexo = 'M',
                SgPorte = 'M',
            };
            db.Pets.Add(pet);
            db.SaveChanges();
            idPet = pet.Id;
        }

        var clienteLogin = factory.CreateClient();
        var token = await AutenticacaoHelper.ObterTokenAsync(clienteLogin);

        return (factory, idPet, token);
    }

    /// <summary>
    /// Controle positivo do MESMO instrumento (Kestrel real): corpo válido e pequeno continua
    /// devolvendo 200 depois do fix do G2-c — o problema era o limite de tamanho não convertido
    /// em 413, não a leitura manual do multipart em si (que passou a ser feita à mão pelo
    /// controller, ver <c>PetsController.UploadFoto</c>).
    /// </summary>
    [Fact]
    public async Task UploadFoto_CorpoDentroDoLimite_ComKestrelReal_Devolve200()
    {
        var (factory, idPet, token) = await SubirComKestrelAsync();
        await using var _ = factory;

        var client = factory.CreateClient();
        client.UsarToken(token);

        var conteudo = new MultipartFormDataContent();
        conteudo.Add(new ByteArrayContent(WebpValido), "thumb", "t.webp");
        conteudo.Add(new ByteArrayContent(WebpValido), "media", "m.webp");

        var resposta = await client.PostAsync($"/api/v1/pets/{idPet}/foto", conteudo);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK,
            "corpo pequeno e válido, sob Kestrel real, tem de continuar funcionando depois do " +
            "fix do G2-c");
    }

    /// <summary>
    /// 🔴 Achado G2-c (Important) — MORDIDA original: antes da fix wave 1, esta mesma
    /// requisição (corpo acima de 2 MB, contra Kestrel real) devolvia <c>400</c>
    /// (<c>"Failed to read the request form. Request body too large..."</c>), porque o
    /// <c>BadHttpRequestException(413)</c> do Kestrel era lançado dentro de
    /// <c>Request.ReadFormAsync()</c> chamado pelo <c>FormValueProviderFactory</c> do MVC
    /// (acionado pelo model binding de <c>[FromForm] PetFotoUploadDto</c>), que capturava a
    /// exceção (ela herda de <see cref="IOException"/>) e a convertia em erro de ModelState —
    /// o case de 413 do <c>ExceptionHandlerMiddleware</c> nunca era alcançado para esta rota.
    ///
    /// <para>🔴 <b>Correção de histórico (fix wave 2, re-G2 <c>g2b-ft03.md</c>, achado
    /// re-G2-3).</b> Este XML doc dizia, sobre o fix da wave 1: "o controller lê o form ele
    /// mesmo, sem try/catch — a exceção sobe crua e o middleware devolve 413 de verdade". Isso
    /// era FALSO — MEDIDO pela própria fix wave 1 (o <c>title</c> da resposta trazia o prefixo
    /// <c>"Failed to read the request form."</c>, exclusivo do MVC, provando que o 413 vinha do
    /// factory GLOBAL de <c>Program.cs</c>, não do middleware) e confirmado de novo pela re-G2
    /// (M2/M4 de <c>g2b-ft03.md</c>). O que a frase descrevia só passou a ser VERDADE na fix
    /// wave 2: <c>Kura.Api.Filters.DesabilitaFormValueProvidersAttribute</c> impede o
    /// <c>FormValueProviderFactory</c> de rodar nesta action, então agora É o controller quem
    /// lê o form primeiro, sem try/catch, e É o case por tipo do
    /// <c>ExceptionHandlerMiddleware</c> quem devolve o 413 — o formato da resposta mudou de
    /// <c>application/json</c> (ad-hoc) para <c>application/problem+json</c> (padrão do
    /// projeto) como consequência direta disso.</para>
    /// </summary>
    [Fact]
    public async Task UploadFoto_CorpoAcimaDoLimite_ComKestrelReal_Devolve413()
    {
        var (factory, idPet, token) = await SubirComKestrelAsync();
        await using var _ = factory;

        var client = factory.CreateClient();

        const string boundary = "g2c413bnd";
        var head = $"--{boundary}" + CRLF
            + "Content-Disposition: form-data; name=\"thumb\"; filename=\"t.bin\"" + CRLF
            + "Content-Type: image/webp" + CRLF + CRLF;
        var inicioDoCorpo = Encoding.ASCII.GetBytes(head)
            .Concat(WebpValido)
            .Concat(new byte[60_000]) // recheio — só precisa ser MENOR que o Content-Length declarado
            .ToArray();

        const long tamanhoDeclarado = 3 * 1024 * 1024; // > 2 MB, o limite de [RequestSizeLimit]

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(client.BaseAddress!.Host, client.BaseAddress.Port);
        await using var ns = tcp.GetStream();

        var cabecalhoRequisicao =
            $"POST /api/v1/pets/{idPet}/foto HTTP/1.1" + CRLF
            + $"Host: {client.BaseAddress.Host}:{client.BaseAddress.Port}" + CRLF
            + $"Authorization: Bearer {token}" + CRLF
            + $"Content-Type: multipart/form-data; boundary={boundary}" + CRLF
            + $"Content-Length: {tamanhoDeclarado}" + CRLF + CRLF;

        await ns.WriteAsync(Encoding.ASCII.GetBytes(cabecalhoRequisicao));
        await ns.WriteAsync(inicioDoCorpo);
        await ns.FlushAsync();

        var resposta = LerRespostaHttp(ns);

        resposta.Should().Contain("413 ",
            "corpo acima do limite declarado em [RequestSizeLimit] tem de ser rejeitado com " +
            "413 pelo Kestrel real — sem o fix do G2-c, esta mesma requisição devolvia 400 " +
            "('Request body too large...' como erro de ModelState)");
        resposta.Should().NotContain("HTTP/1.1 400",
            "controle negativo: o 413 não pode ter sido acidentalmente convertido em 400 de " +
            "novo (era exatamente esse o defeito que a G2 reprovou)");
    }

    /// <summary>
    /// 🔴 Fix wave 2 (re-G2, g2b-ft03.md/M2) — corpo acima do limite SEM <c>Content-Length</c>
    /// (<c>Transfer-Encoding: chunked</c>). O caminho chunked estoura o limite DURANTE a
    /// leitura (não antes de começar, como no caso com <c>Content-Length</c> declarado) —
    /// medido pela re-G2 que produz a MESMA exceção/mensagem, então tinha de ser provado
    /// separadamente do caso acima em vez de presumido idêntico.
    /// </summary>
    [Fact]
    public async Task UploadFoto_CorpoAcimaDoLimiteChunked_ComKestrelReal_Devolve413()
    {
        var (factory, idPet, token) = await SubirComKestrelAsync();
        await using var _ = factory;

        var client = factory.CreateClient();

        const string boundary = "g2bchunkedbnd";
        var head = $"--{boundary}" + CRLF
            + "Content-Disposition: form-data; name=\"thumb\"; filename=\"t.bin\"" + CRLF
            + "Content-Type: image/webp" + CRLF + CRLF;
        var primeiroPedaco = Encoding.ASCII.GetBytes(head).Concat(WebpValido).ToArray();

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(client.BaseAddress!.Host, client.BaseAddress.Port);
        await using var ns = tcp.GetStream();

        var cabecalhoRequisicao =
            $"POST /api/v1/pets/{idPet}/foto HTTP/1.1" + CRLF
            + $"Host: {client.BaseAddress.Host}:{client.BaseAddress.Port}" + CRLF
            + $"Authorization: Bearer {token}" + CRLF
            + $"Content-Type: multipart/form-data; boundary={boundary}" + CRLF
            + "Transfer-Encoding: chunked" + CRLF + CRLF;
        await ns.WriteAsync(Encoding.ASCII.GetBytes(cabecalhoRequisicao));

        async Task EscreverChunkAsync(byte[] dados)
        {
            await ns.WriteAsync(Encoding.ASCII.GetBytes(dados.Length.ToString("X") + CRLF));
            await ns.WriteAsync(dados);
            await ns.WriteAsync(Encoding.ASCII.GetBytes(CRLF));
        }

        await EscreverChunkAsync(primeiroPedaco);

        const long limite = 2 * 1024 * 1024;
        var bloco = new byte[64 * 1024];
        long enviados = primeiroPedaco.Length;
        try
        {
            // Escreve até passar do limite de [RequestSizeLimit] — o Kestrel encerra a
            // leitura assim que detecta o estouro, então este laço pode nunca chegar ao chunk
            // final "0\r\n\r\n" (esperado: IOException ao escrever num socket já fechado pelo
            // servidor, tratado como "a resposta já chegou").
            while (enviados < 3L * 1024 * 1024)
            {
                await EscreverChunkAsync(bloco);
                enviados += bloco.Length;
            }
            await ns.WriteAsync(Encoding.ASCII.GetBytes("0" + CRLF + CRLF));
        }
        catch (IOException)
        {
            // Esperado: o Kestrel fecha a conexão no meio do envio ao detectar o estouro.
        }
        await ns.FlushAsync();

        var resposta = LerRespostaHttp(ns);

        resposta.Should().Contain("413 ",
            "corpo chunked acima do limite declarado em [RequestSizeLimit] tem de ser " +
            "rejeitado com 413 pelo Kestrel real, do mesmo jeito que o caso com " +
            $"Content-Length (enviados={enviados}, limite={limite})");
        resposta.Should().NotContain("HTTP/1.1 400",
            "controle negativo: o caminho chunked não pode ter sido convertido em 400");
    }

    /// <summary>
    /// 🔴 Achado re-G2-1 (Important, g2b-ft03.md) — MORDIDA/spoof virado teste permanente. O
    /// fix wave 1 detectava o 413 casando por SUBSTRING <c>"Request body too large"</c> numa
    /// mensagem de <c>ModelState</c>, num factory GLOBAL (<c>Program.cs</c>) que valia para
    /// QUALQUER rota do projeto. A re-G2 mediu que o <c>ModelBindingMessageProvider</c> padrão
    /// ECOA o valor tentado de um parâmetro de query inválido — então um cliente que
    /// escrevesse literalmente esse texto numa query string de uma rota SEM NENHUMA RELAÇÃO
    /// com upload de arquivo recebia <c>413</c> em vez do <c>400</c> de validação normal, só
    /// por ter digitado a frase certa. A fix wave 2 removeu o factory global — o 413 agora só
    /// existe localmente em <c>PetsController.UploadFoto</c>
    /// (<see cref="Kura.Api.Filters.DesabilitaFormValueProvidersAttribute"/>), então este
    /// spoof não pode mais alcançar nenhum mecanismo de 413.
    /// </summary>
    [Fact]
    public async Task GetAgenda_DataInicioComTextoDoSpoofDe413_Devolve400NaoMais413()
    {
        var factory = new KuraApiFactory();
        await using var _ = factory;

        var client = factory.CreateClient();
        client.UsarToken(await AutenticacaoHelper.ObterTokenAsync(client));

        var resposta = await client.GetAsync(
            "/api/v1/agenda?dataInicio=Request%20body%20too%20large&dataFim=2026-01-01");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "um parâmetro de data inválido é sempre 400 de validação — mesmo quando o valor " +
            "digitado é literalmente o texto que o Kestrel usa para sinalizar 413, essa rota " +
            "não tem nenhuma relação com upload de arquivo nem com RequestSizeLimit");
    }

    /// <summary>
    /// Lê a resposta HTTP crua até achar um <c>}</c> (fim do corpo JSON, pequeno) ou estourar
    /// o timeout de leitura — o Kestrel pode fechar a conexão logo depois de escrever a
    /// resposta, então um <see cref="IOException"/> de timeout/reset é esperado e tratado
    /// como "acabou de chegar", não como falha do teste.
    /// </summary>
    private static string LerRespostaHttp(NetworkStream stream, int timeoutMs = 10_000)
    {
        stream.ReadTimeout = timeoutMs;
        var buffer = new byte[16 * 1024];
        var sb = new StringBuilder();
        try
        {
            int lidos;
            while ((lidos = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, lidos));
                if (sb.ToString().Contains('}'))
                    break;
            }
        }
        catch (IOException)
        {
            // Timeout de leitura ou conexão encerrada pelo servidor — devolve o que já chegou.
        }
        return sb.ToString();
    }
}
