using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SAPS.Web.Services.Seguridad;

/// <summary>
/// Límite de peticiones. El bloqueo por cuenta (5 intentos) no frena a quien prueba pocas contraseñas
/// en muchas cuentas, así que el inicio de sesión también se limita por IP. Pedidos se limita por usuario
/// para frenar un script que consulte o registre sin parar.
/// Detrás de un proxy inverso hay que configurar ForwardedHeaders; si no, todas las personas comparten la IP del proxy.
/// </summary>
public static class LimitesPeticiones
{
    public const string PoliticaPedidos = "pedidos";

    public const int IntentosLoginPorMinuto = 10;
    public const int PeticionesPedidosPorMinuto = 120;

    public const string MensajeLimite =
        "Demasiadas solicitudes en poco tiempo. Espere un minuto e inténtelo de nuevo.";

    public static void Configurar(RateLimiterOptions opciones)
    {
        opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Solo el POST del login cuenta: abrir la página no consume intentos.
        opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            EsEnvioDeLogin(context.Request)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "login:" + context.Connection.RemoteIpAddress,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = IntentosLoginPorMinuto,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                : RateLimitPartition.GetNoLimiter("sin-limite"));

        opciones.AddPolicy(PoliticaPedidos, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                "pedidos:" + (context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString()),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PeticionesPedidosPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        opciones.OnRejected = async (contexto, ct) =>
        {
            contexto.HttpContext.Response.Headers.RetryAfter = "60";
            // pedidos.js lee { mensaje }; el envío del login lo ve una persona en el navegador: texto plano.
            if (EsEnvioDeLogin(contexto.HttpContext.Request))
            {
                contexto.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await contexto.HttpContext.Response.WriteAsync(MensajeLimite, ct);
                return;
            }
            await contexto.HttpContext.Response.WriteAsJsonAsync(new { mensaje = MensajeLimite }, ct);
        };
    }

    public static bool EsEnvioDeLogin(HttpRequest solicitud) =>
        HttpMethods.IsPost(solicitud.Method)
        && solicitud.Path.Equals("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase);
}
