namespace Kura.Application.Services;

using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementação de <see cref="IRelogioClinica"/> — REC-08 (A-5/F-4).
///
/// <para><b>Fuso por config, default São Paulo</b> — lido UMA VEZ no construtor (singleton, ver
/// <c>ServiceCollectionExtensions</c>), mesmo padrão de leitura de config do
/// <see cref="GeradorLinkConvite"/>: se <c>RelogioClinica:FusoHorario</c> estiver ausente ou
/// vazio, usa <c>America/Sao_Paulo</c>; se o id IANA configurado não existir no host, loga um
/// único <c>WARN</c> e cai no mesmo default em vez de derrubar o processo — errar o fuso não
/// pode ser motivo de a clínica ficar sem agenda.</para>
///
/// <para><b><see cref="TimeProvider"/> injetável</b> (não <c>DateTime.UtcNow</c> direto) para
/// que testes fixem "agora" com um <c>FakeTimeProvider</c>/relógio fixo, sem depender do
/// horário real da máquina que roda a suíte.</para>
/// </summary>
public sealed class RelogioClinica : IRelogioClinica
{
    public const string FusoDefault = "America/Sao_Paulo";

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _fuso;

    public RelogioClinica(TimeProvider timeProvider, IConfiguration configuration, ILogger<RelogioClinica> logger)
    {
        _timeProvider = timeProvider;

        var fusoConfigurado = configuration["RelogioClinica:FusoHorario"];
        var fusoId = string.IsNullOrWhiteSpace(fusoConfigurado) ? FusoDefault : fusoConfigurado;

        try
        {
            _fuso = TimeZoneInfo.FindSystemTimeZoneById(fusoId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(
                ex,
                "RelogioClinica: fuso '{FusoId}' inválido ou não encontrado neste host — usando o default {FusoDefault}.",
                fusoId,
                FusoDefault);
            _fuso = TimeZoneInfo.FindSystemTimeZoneById(FusoDefault);
        }
    }

    public DateTime Agora() =>
        TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _fuso);

    public DateTime Hoje() => Agora().Date;
}
