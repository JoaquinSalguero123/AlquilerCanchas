namespace AlquilerCanchas.Infrastructure.Servicios;

/// <summary>
/// El complejo opera en hora argentina, pero Azure App Service corre en UTC: sin esto, un pago
/// cargado a las 22:00 quedaría con la fecha del día siguiente y "hoy" no sería hoy para el usuario.
/// </summary>
public class RelojArgentina : TimeProvider
{
    private static readonly TimeZoneInfo Zona = Resolver();

    public override TimeZoneInfo LocalTimeZone => Zona;

    private static TimeZoneInfo Resolver()
    {
        // El id cambia según el SO (Windows vs. IANA en Linux); si ninguno existe se usa UTC-3 fijo
        // (Argentina no tiene horario de verano desde 2009).
        foreach (var id in new[] { "America/Argentina/Buenos_Aires", "Argentina Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "Argentina", "Argentina");
    }
}
