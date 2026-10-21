namespace SAPS.Web.Services;

/// <summary>
/// Fecha "de hoy" en Costa Rica (UTC-6, sin horario de verano). El servidor puede estar en otra zona horaria:
/// con DateTime.Today un precio guardado de noche quedaría con la fecha de vigencia de otro día.
/// </summary>
public static class FechaCostaRica
{
    private static readonly TimeSpan Desfase = TimeSpan.FromHours(-6);

    public static DateOnly Hoy(TimeProvider? reloj = null) =>
        DateOnly.FromDateTime((reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime.Add(Desfase));
}
