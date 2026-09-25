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
/// <c>TestServer</c>.
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
    /// 🔴 Achado G2-c (Important) — MORDIDA: antes desta fix wave, esta mesma requisição (corpo
    /// acima de 2 MB, contra Kestrel real) devolvia <c>400</c>
    /// (<c>"Failed to read the request form. Request body too large..."</c>), porque o
    /// <c>BadHttpRequestException(413)</c> do Kestrel era lançado dentro de
    /// <c>Request.ReadFormAsync()</c> chamado pelo <c>FormValueProviderFactory</c> do MVC
    /// (acionado pelo model binding de <c>[FromForm] PetFotoUploadDto</c>), que capturava a
    /// exceção (ela herda de <see cref="IOException"/>) e a convertia em erro de ModelState —
    /// o case de 413 do <c>ExceptionHandlerMiddleware</c> nunca era alcançado para esta rota.
    /// Fix: o controller lê o form ele mesmo (<c>Request.ReadFormAsync()</c>), sem
    /// try/catch — a exceção sobe crua e o middleware devolve 413 de verdade.
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
