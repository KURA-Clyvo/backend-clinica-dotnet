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
///
/// <para><b>REC-08 fix wave (G2, m-5) — fallback do fallback.</b> Se NEM o fuso configurado NEM
/// <see cref="FusoDefault"/> existirem no host (ex.: imagem sem <c>tzdata</c>), o construtor
/// original teria deixado o <see cref="TimeZoneNotFoundException"/> do segundo
/// <c>FindSystemTimeZoneById</c> escapar sem tratamento — como a resolução do singleton é
/// preguiçosa (primeira injeção), isso vira <c>500</c> no primeiro request que tocar dashboard/
/// teleconsulta/agenda, não uma falha na partida. Agora cai num fuso de OFFSET FIXO −03:00
/// (<see cref="TimeZoneInfo.CreateCustomTimeZone(string, TimeSpan, string, string)"/>) com um
/// segundo <c>WARN</c>, em vez de lançar. Não é logicamente idêntico ao fuso IANA (não segue
/// mudança de lei de horário de verão), mas América/São Paulo não observa DST desde 2019 — hoje
/// é equivalente na prática, e é estritamente melhor que derrubar a resolução.</para>
/// </summary>
public sealed class RelogioClinica : IRelogioClinica
{
    public const string FusoDefault = "America/Sao_Paulo";

    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _fuso;

    public RelogioClinica(TimeProvider timeProvider, IConfiguration configuration, ILogger<RelogioClinica> logger)
        : this(timeProvider, configuration, logger, TimeZoneInfo.FindSystemTimeZoneById)
    {
    }

    /// <summary>
    /// REC-08 fix wave (G2, m-5) — overload que recebe o RESOLVEDOR de fuso (função
    /// <c>id → TimeZoneInfo</c>), para que o teste do fallback "nem o configurado nem o
    /// default existem" não dependa de o host que roda a suíte ter ou não <c>tzdata</c>. A
    /// injeção de dependência de produção sempre resolve o construtor de 3 parâmetros acima
    /// (nenhum <c>Func&lt;string, TimeZoneInfo&gt;</c> está registrado no container, então o
    /// .NET DI descarta este overload e usa o outro — sem registro extra necessário).
    /// </summary>
    public RelogioClinica(
        TimeProvider timeProvider,
        IConfiguration configuration,
        ILogger<RelogioClinica> logger,
        Func<string, TimeZoneInfo> resolverFuso)
    {
        _timeProvider = timeProvider;

        var fusoConfigurado = configuration["RelogioClinica:FusoHorario"];
        var fusoId = string.IsNullOrWhiteSpace(fusoConfigurado) ? FusoDefault : fusoConfigurado;

        try
        {
            _fuso = resolverFuso(fusoId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(
                ex,
                "RelogioClinica: fuso '{FusoId}' inválido ou não encontrado neste host — usando o default {FusoDefault}.",
                fusoId,
                FusoDefault);

            try
            {
                _fuso = resolverFuso(FusoDefault);
            }
            catch (Exception ex2) when (ex2 is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                logger.LogWarning(
                    ex2,
                    "RelogioClinica: fuso default '{FusoDefault}' também não encontrado neste host — usando offset fixo -03:00 (sem horário de verão).",
                    FusoDefault);
                _fuso = TimeZoneInfo.CreateCustomTimeZone(
                    "America/Sao_Paulo (offset fixo)",
                    TimeSpan.FromHours(-3),
                    "Horário de Brasília (fixo)",
                    "Horário de Brasília (fixo)");
            }
        }
    }

    public DateTime Agora() =>
        TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _fuso);

    public DateTime Hoje() => Agora().Date;
}
