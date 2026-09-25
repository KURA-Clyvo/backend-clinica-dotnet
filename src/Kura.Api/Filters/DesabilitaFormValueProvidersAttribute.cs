namespace Kura.Api.Filters;

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

/// <summary>
/// Fix wave 2 da FT-03 (re-G2 <c>g2b-ft03.md</c>, achados re-G2-1/re-G2-2) — substitui o
/// <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> GLOBAL que existia antes (casava
/// por SUBSTRING <c>"Request body too large"</c>, disparável por qualquer cliente que
/// escrevesse esse texto numa query string de QUALQUER rota do projeto — medido pela re-G2:
/// <c>GET /api/v1/agenda?dataInicio=Request%20body%20too%20large&amp;dataFim=...</c> devolvia
/// 413 antes desta fix, quando deveria devolver 400 de validação normal).
///
/// <para><b>Mecanismo (padrão documentado pela Microsoft para upload por streaming,
/// "DisableFormValueModelBinding"):</b> remove os 3 <see cref="IValueProviderFactory"/> de
/// FORM (<see cref="FormValueProviderFactory"/>, <see cref="FormFileValueProviderFactory"/>,
/// <see cref="JQueryFormValueProviderFactory"/>) do pipeline de model binding SÓ para a action
/// decorada com este atributo. Sem esses factories, nada chama
/// <c>HttpRequest.ReadFormAsync()</c> durante o model binding — então o
/// <c>Microsoft.AspNetCore.Http.BadHttpRequestException</c>(413) que o Kestrel lança quando o
/// corpo estoura <see cref="Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute"/> nunca é
/// capturado e embrulhado em erro de ModelState — ele sobe CRU até o
/// <c>Kura.Api.Middlewares.ExceptionHandlerMiddleware</c>, que já tem um case POR TIPO
/// (<c>BadHttpRequestException badRequestEx =&gt; badRequestEx.StatusCode</c>) para exatamente
/// esse cenário. A action decorada (<c>PetsController.UploadFoto</c>) é quem chama
/// <c>Request.ReadFormAsync()</c> manualmente, DEPOIS que o model binding (agora sem estes
/// factories) já terminou sem erro — por isso o parâmetro de rota (<c>{id:long}</c>) continua
/// sendo vinculado normalmente: ele vem do <c>RouteValueProvider</c>, que este filtro não
/// remove.</para>
///
/// <para><b>Medido na re-G2 (M5 de <c>g2b-ft03.md</c>):</b> com este filtro, corpo acima do
/// limite via <c>Content-Length</c> E via <c>Transfer-Encoding: chunked</c> → 413
/// <c>application/problem+json</c> (formato PADRÃO do projeto, não mais o JSON ad-hoc que o
/// factory global antigo produzia); o spoof pela query string da agenda volta a 400; os
/// <c>PetFotoHttpTests</c> (400/404/200) continuam verdes.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DesabilitaFormValueProvidersAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var fabricas = context.ValueProviderFactories;
        fabricas.RemoveType<FormValueProviderFactory>();
        fabricas.RemoveType<FormFileValueProviderFactory>();
        fabricas.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nada a fazer depois da execução — este filtro só altera o binding, não a resposta.
    }
}
