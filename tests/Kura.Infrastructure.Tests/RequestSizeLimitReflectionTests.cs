namespace Kura.Infrastructure.Tests;

using System.Reflection;
using FluentAssertions;
using Kura.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// FT-03 (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>) — aceite 4 (corpo acima do limite → 413).
///
/// <para><b>Por que reflection, e não só o teste HTTP.</b> Medido em
/// <c>PetFotoHttpTests.UploadFoto_CorpoAcimaDoLimite_SobTestServerNaoEhRejeitado_MedidoNaoPresumido</c>:
/// sob <c>WebApplicationFactory</c>/<c>TestServer</c>, um corpo de 3 MB (limite declarado é
/// 2 MB) é ACEITO com <c>200</c> — <c>TestServer</c> não implementa
/// <c>IHttpMaxRequestBodySizeFeature</c> como o Kestrel real. Este teste prova a METADE que
/// não depende do transporte: o atributo está de fato presente no método, com o valor
/// certo — independente de o harness de teste conseguir ou não reproduzir a rejeição.</para>
/// </summary>
public class RequestSizeLimitReflectionTests
{
    [Fact]
    public void UploadFoto_DeclaraRequestSizeLimitDe2Megabytes()
    {
        var metodo = typeof(PetsController).GetMethod(nameof(PetsController.UploadFoto));

        metodo.Should().NotBeNull();

        var atributo = metodo!.GetCustomAttribute<RequestSizeLimitAttribute>();

        atributo.Should().NotBeNull(
            "sem [RequestSizeLimit] o Kestrel real aceitaria corpo de qualquer tamanho " +
            "(G0 mediu: hoje não existe limite explícito em NENHUM endpoint do projeto)");

        ((IRequestSizeLimitMetadata)atributo!).MaxRequestBodySize.Should().Be(
            2 * 1024 * 1024,
            "o backlog pede exatamente 2 MB — um valor maior tornaria o limite inócuo, um " +
            "valor menor rejeitaria fotos legítimas processadas pelo cliente (FT-07)");
    }

    [Fact]
    public void UploadFoto_ContinuaExigindoAutenticacao()
    {
        // Controle: o limite de tamanho não substitui a autenticação — o endpoint herda
        // [Authorize] do controller (decisão F2, ver doc-comment de PetsController).
        typeof(PetsController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();
    }
}
