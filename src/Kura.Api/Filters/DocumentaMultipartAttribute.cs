namespace Kura.Api.Filters;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// S4-NET-07 (G4-m-2) — descreve, SÓ NA DOCUMENTAÇÃO OpenAPI, o corpo <c>multipart/form-data</c> de
/// uma action que lê o formulário à mão (<see cref="DesabilitaFormValueProvidersAttribute"/>). Sem
/// parâmetro vinculado, o Swashbuckle não tem de onde inferir o corpo e publica
/// <c>requestBody: null</c>. Este atributo NÃO participa do model binding: o comportamento do
/// endpoint (413 cru até o middleware, leitura por <c>ReadFormAsync</c>) fica exatamente como era.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DocumentaMultipartAttribute : Attribute
{
    /// <param name="arquivos">Partes de arquivo (binárias), todas obrigatórias.</param>
    public DocumentaMultipartAttribute(params string[] arquivos) => Arquivos = arquivos;

    /// <summary>Nomes das partes de arquivo (<c>type: string, format: binary</c>), obrigatórias.</summary>
    public string[] Arquivos { get; }

    /// <summary>Campos de texto obrigatórios.</summary>
    public string[] CamposObrigatorios { get; init; } = [];

    /// <summary>Campos de texto opcionais.</summary>
    public string[] CamposOpcionais { get; init; } = [];
}

/// <summary>Aplica <see cref="DocumentaMultipartAttribute"/> ao documento OpenAPI.</summary>
public sealed class DocumentaMultipartOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor acao) return;
        var atributo = acao.MethodInfo
            .GetCustomAttributes(typeof(DocumentaMultipartAttribute), inherit: false)
            .Cast<DocumentaMultipartAttribute>()
            .FirstOrDefault();
        if (atributo is null) return;

        var propriedades = new Dictionary<string, IOpenApiSchema>();
        foreach (var arquivo in atributo.Arquivos)
            propriedades[arquivo] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        foreach (var campo in atributo.CamposObrigatorios.Concat(atributo.CamposOpcionais))
            propriedades[campo] = new OpenApiSchema { Type = JsonSchemaType.String };

        var esquema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = propriedades,
            Required = new HashSet<string>(atributo.Arquivos.Concat(atributo.CamposObrigatorios)),
        };

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType { Schema = esquema },
            },
        };
    }
}
