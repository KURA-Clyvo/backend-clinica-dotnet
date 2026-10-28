namespace Kura.Application.Validators;

using FluentValidation;
using Kura.Domain.Clinicas;
using Kura.Application.DTOs.Auth;

public sealed class RegisterClinicaValidator : AbstractValidator<RegisterClinicaDto>
{
    public RegisterClinicaValidator()
    {
        RuleFor(x => x.NmClinica)
            .NotEmpty().WithMessage("Nome da clínica é obrigatório.")
            .MaximumLength(120);

        RuleFor(x => x.NrCnpj)
            .NotEmpty().WithMessage("CNPJ é obrigatório.")
            .Must(c => NormalizadorCnpj.TentarNormalizar(c, out _))
            .WithMessage("CNPJ inválido. Informe 14 dígitos, com ou sem máscara (00.000.000/0000-00).");

        RuleFor(x => x.NmRazaoSocial)
            .MaximumLength(150);

        RuleFor(x => x.DsEndereco)
            .NotEmpty().WithMessage("Endereço é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.NmCidade)
            .NotEmpty().WithMessage("Cidade é obrigatória.")
            .MaximumLength(80);

        RuleFor(x => x.SgUf)
            .NotEmpty().WithMessage("UF é obrigatória.")
            .Length(2).WithMessage("UF deve ter exatamente 2 caracteres.");

        RuleFor(x => x.NrCep)
            .NotEmpty().WithMessage("CEP é obrigatório.")
            .MaximumLength(9);

        RuleFor(x => x.DsEmail)
            .NotEmpty().WithMessage("E-mail da clínica é obrigatório.")
            .EmailAddress().WithMessage("E-mail da clínica inválido.")
            .MaximumLength(120);

        RuleFor(x => x.DsEmailAcesso)
            .NotEmpty().WithMessage("Email de acesso é obrigatório.")
            .EmailAddress().WithMessage("Email de acesso inválido.")
            .MaximumLength(120);

        RuleFor(x => x.DsSenha)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");

        RuleFor(x => x.NmVeterinarioAdmin)
            .NotEmpty().WithMessage("Nome do veterinário administrador é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.NrCRMV)
            .NotEmpty().WithMessage("CRMV é obrigatório.")
            .MaximumLength(20);
    }
}
