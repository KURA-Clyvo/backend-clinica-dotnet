namespace Kura.IntegrationTests;

/// <summary>
/// S4-NET-FIX-MEIANOITE: <see cref="KuraApiFactory"/> com o <see cref="TimeProvider"/> do host
/// FIXO. O <c>AgendaService</c> recusa check-in/início fora do dia do agendamento
/// (<c>IRelogioClinica.Hoje()</c>), então um teste que semeia "agora − 30 min" pelo relógio de
/// parede quebra entre 00h00 e 00h30 de São Paulo. Aqui o "agora" do host E o do teste são o
/// mesmo instante, controlado.
///
/// <para>Padrão: <b>10h00</b> de São Paulo, na data de hoje (longe da meia-noite). A variável de
/// ambiente <c>KURA_TESTES_HORA_SP</c> (<c>HH:mm</c>) força outro horário — é como se roda a suíte
/// "com o relógio em 00h15" sem esperar a virada do dia.</para>
/// </summary>
public class KuraApiFactoryRelogioFixo : KuraApiFactory
{
    public const string VariavelHora = "KURA_TESTES_HORA_SP";

    private static readonly TimeZoneInfo Sp = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly RelogioDeTestes _relogio;

    public KuraApiFactoryRelogioFixo()
    {
        var hora = TimeSpan.Parse(Environment.GetEnvironmentVariable(VariavelHora) is { Length: > 0 } v ? v : "10:00");
        var hojeSp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Sp).Date;
        _relogio = new RelogioDeTestes(TimeZoneInfo.ConvertTimeToUtc(hojeSp + hora, Sp));
    }

    protected override TimeProvider? ProvedorDeTempo => _relogio;

    /// <summary>Muda o instante do host (em hora de São Paulo).</summary>
    public void DefinirAgoraSp(DateTime agoraSp)
        => _relogio.Utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(agoraSp, DateTimeKind.Unspecified), Sp);

    /// <summary>"Agora" em hora de SP, pelo MESMO relógio do host.</summary>
    public DateTime AgoraSp() => TimeZoneInfo.ConvertTimeFromUtc(_relogio.Utc, Sp);

    private sealed class RelogioDeTestes(DateTime utcInicial) : TimeProvider
    {
        public DateTime Utc { get; set; } = DateTime.SpecifyKind(utcInicial, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Utc, TimeSpan.Zero);
    }
}
