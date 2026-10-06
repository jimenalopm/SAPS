using OpenQA.Selenium;
using OpenQA.Selenium.Internal;

namespace SAPS.FunctionalTests.Infraestructura;

/// <summary>
/// Clic robusto. El encabezado de SAPS es "sticky" (queda fijo arriba) y hay ventanas con animación (Bootstrap):
/// si Selenium hace clic con un elemento tapado, lanza ElementClickInterceptedException.
/// </summary>
public static class Interaccion
{
    /// <summary>
    /// 1) Centra el elemento en pantalla (así no queda bajo el encabezado fijo ni bajo el borde inferior);
    /// 2) espera a que NADA lo tape (comprueba qué elemento está realmente en el punto del clic);
    /// 3) hace el clic normal de Selenium (el que haría una persona);
    /// 4) solo si tras unos segundos sigue tapado, hace el clic por JavaScript como último recurso.
    /// </summary>
    public static void ClicSeguro(this IWebElement elemento, int segundosDeEspera = 5)
    {
        var js = (IJavaScriptExecutor)((IWrapsDriver)elemento).WrappedDriver;
        var limite = DateTime.UtcNow.AddSeconds(segundosDeEspera);

        while (DateTime.UtcNow < limite)
        {
            js.ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", elemento);
            if (elemento.Displayed && elemento.Enabled && EstaLibre(js, elemento))
            {
                try
                {
                    elemento.Click();
                    return;
                }
                catch (ElementClickInterceptedException)
                {
                    // Algo lo tapó justo ahora (animación, aviso): se reintenta tras una pausa corta.
                }
            }
            Thread.Sleep(200);
        }

        // Último recurso: el elemento sigue tapado tras esperar. Se hace clic por JavaScript.
        js.ExecuteScript("arguments[0].click();", elemento);
    }

    /// <summary>¿El elemento que está en el centro del botón es el propio botón (o algo dentro de él)?</summary>
    private static bool EstaLibre(IJavaScriptExecutor js, IWebElement elemento) =>
        (bool)js.ExecuteScript(@"
            var a = arguments[0], r = a.getBoundingClientRect();
            var t = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
            return !!t && (t === a || a.contains(t));", elemento)!;
}
