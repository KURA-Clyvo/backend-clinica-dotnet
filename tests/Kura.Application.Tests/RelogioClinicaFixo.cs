namespace Kura.Application.Tests;

using Kura.Domain.Interfaces;

/// <summary>
/// REC-08 — dublê de <see cref="IRelogioClinica"/> com "agora" fixo, para testes que não
/// precisam exercitar a conversão de fuso de verdade (essa fica em
/// <c>RelogioClinicaTests</c>, contra a implementação real com <c>TimeProvider</c> fake).
/// Compartilhado por <c>DashboardServiceTests</c> e <c>TeleconsultaServiceTests</c>.
/// </summary>
internal sealed class RelogioClinicaFixo(DateTime agora) : IRelogioClinica
{
    public DateTime Agora() => agora;

    public DateTime Hoje() => agora.Date;
}
