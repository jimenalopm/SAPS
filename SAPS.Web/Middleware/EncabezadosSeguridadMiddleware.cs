using System.Security.Cryptography;

namespace SAPS.Web.Middleware;

/// <summary>
/// Encabezados de seguridad de toda respuesta. La política de contenido (CSP) solo deja correr scripts
/// propios (<c>'self'</c>) o los que lleven el nonce de la solicitud: un script que alguien lograra
/// inyectar en una página no se ejecuta. Los scripts en línea de las vistas usan
/// <c>nonce="@Context.Items[EncabezadosSeguridadMiddleware.ClaveNonce]"</c>.
/// Los estilos en línea se permiten porque las vistas usan atributos <c>style</c> (no ejecutan código).
/// </summary>
public sealed class EncabezadosSeguridadMiddleware(RequestDelegate siguiente)
{
    public const string ClaveNonce = "csp-nonce";

    public async Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[ClaveNonce] = nonce;

        var encabezados = context.Response.Headers;
        encabezados["Content-Security-Policy"] = ConstruirPolitica(nonce);
        encabezados["X-Content-Type-Options"] = "nosniff";
        encabezados["X-Frame-Options"] = "DENY";
        encabezados["Referrer-Policy"] = "strict-origin-when-cross-origin";
        encabezados["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        await siguiente(context);
    }

    public static string ConstruirPolitica(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
}
