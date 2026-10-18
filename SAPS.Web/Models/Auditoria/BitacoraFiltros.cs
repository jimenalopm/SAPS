using System.Globalization;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Web.Models.Auditoria;

/// <summary>
/// HU-004 task 12 / HU-015 criterio 6: filtros de la pantalla de bitácora. Llegan por la URL (formulario GET),
/// así que un listado filtrado se puede recargar o compartir y la paginación los conserva.
/// Todos son opcionales; sin filtros se muestra toda la bitácora.
/// </summary>
public class BitacoraFiltros
{
    public const string ResultadoExitoso = "exitoso";
    public const string ResultadoFallido = "fallido";
    private const int MaxTexto = 100;

    /// <summary>Código del evento (p. ej. CambioPrecio). Las opciones salen de la propia tabla.</summary>
    public string? TipoEvento { get; set; }

    public string? Modulo { get; set; }

    /// <summary>"exitoso" o "fallido"; cualquier otro valor se ignora.</summary>
    public string? Resultado { get; set; }

    /// <summary>Día inicial, en hora de Costa Rica (incluido).</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Día final, en hora de Costa Rica (incluido completo).</summary>
    public DateTime? Hasta { get; set; }

    /// <summary>Parte del nombre de usuario o correo (también el usuario de BD en cambios directos).</summary>
    public string? Usuario { get; set; }

    public bool TieneFiltros =>
        TipoEvento is not null || Modulo is not null || Resultado is not null
        || Desde is not null || Hasta is not null || Usuario is not null;

    /// <summary>Limpia lo que llega de la URL: textos vacíos a null, largo máximo, resultado válido y fechas en orden.</summary>
    public void Normalizar()
    {
        TipoEvento = Limpiar(TipoEvento);
        Modulo = Limpiar(Modulo);
        Usuario = Limpiar(Usuario);
        Resultado = Limpiar(Resultado)?.ToLowerInvariant();
        if (Resultado is not (ResultadoExitoso or ResultadoFallido)) Resultado = null;

        Desde = Desde?.Date;
        Hasta = Hasta?.Date;
        if (Desde is DateTime d && Hasta is DateTime h && d > h)
            (Desde, Hasta) = (h, d);   // si las invirtieron, se toman en el orden correcto
    }

    /// <summary>
    /// Aplica los filtros a la consulta. Las fechas se escriben en hora de Costa Rica y la tabla guarda UTC,
    /// por eso se convierten antes de comparar. Todo se traduce a SQL compatible con SQL Server 2012
    /// (igualdades, rangos y LIKE; sin listas con Contains).
    /// </summary>
    public IQueryable<Bitacora> Aplicar(IQueryable<Bitacora> consulta)
    {
        // Copias locales: EF Core las manda como parámetros de la consulta.
        if (TipoEvento is string tipo)
            consulta = consulta.Where(b => b.TipoEvento == tipo);

        if (Modulo is string modulo)
            consulta = consulta.Where(b => b.Modulo == modulo);

        if (Resultado is not null)
        {
            var exitoso = Resultado == ResultadoExitoso;
            consulta = consulta.Where(b => b.EsExitoso == exitoso);
        }

        if (Desde is DateTime desde)
        {
            var desdeUtc = PresentacionBitacora.AUtc(desde.Date);
            consulta = consulta.Where(b => b.FechaHoraUtc >= desdeUtc);
        }

        if (Hasta is DateTime hasta)
        {
            var hastaUtc = PresentacionBitacora.AUtc(hasta.Date.AddDays(1));   // fin del día, excluyente
            consulta = consulta.Where(b => b.FechaHoraUtc < hastaUtc);
        }

        if (Usuario is string texto)
        {
            consulta = consulta.Where(b => b.NombreUsuario != null && b.NombreUsuario.Contains(texto));
        }

        return consulta;
    }

    /// <summary>Valores de ruta para enlaces (paginación, ver detalle, volver) que conservan los filtros.</summary>
    public Dictionary<string, string> ComoRuta(int? pagina = null, string? sinClave = null)
    {
        var ruta = new Dictionary<string, string>();
        void Agregar(string clave, string? valor)
        {
            if (valor is not null && clave != sinClave) ruta[clave] = valor;
        }

        Agregar("tipoEvento", TipoEvento);
        Agregar("modulo", Modulo);
        Agregar("resultado", Resultado);
        Agregar("desde", Desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar("hasta", Hasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar("usuario", Usuario);
        if (pagina is > 1) ruta["pagina"] = pagina.Value.ToString(CultureInfo.InvariantCulture);
        return ruta;
    }

    /// <summary>Filtros aplicados, con texto legible, para mostrarlos como etiquetas que se pueden quitar.</summary>
    public List<FiltroActivo> Activos()
    {
        var lista = new List<FiltroActivo>();
        if (TipoEvento is not null) lista.Add(new("tipoEvento", "Evento", PresentacionBitacora.EtiquetaEvento(TipoEvento)));
        if (Modulo is not null) lista.Add(new("modulo", "Módulo", PresentacionBitacora.EtiquetaModulo(Modulo)));
        if (Resultado is not null) lista.Add(new("resultado", "Resultado", Resultado == ResultadoExitoso ? "Exitoso" : "Fallido"));
        if (Desde is DateTime d) lista.Add(new("desde", "Desde", PresentacionBitacora.Fecha(d)));
        if (Hasta is DateTime h) lista.Add(new("hasta", "Hasta", PresentacionBitacora.Fecha(h)));
        if (Usuario is not null) lista.Add(new("usuario", "Usuario", Usuario));
        return lista;
    }

    private static string? Limpiar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        return texto.Length <= MaxTexto ? texto : texto[..MaxTexto];
    }
}

/// <summary>Un filtro aplicado: clave en la URL, nombre del filtro y valor legible.</summary>
public sealed record FiltroActivo(string Clave, string Nombre, string Valor);

/// <summary>Opción de una lista desplegable: valor en la URL y texto visible.</summary>
public sealed record OpcionFiltro(string Valor, string Texto);

/// <summary>Tipos de evento agrupados por módulo (optgroup del filtro de tipo de evento).</summary>
public sealed record GrupoOpcionesFiltro(string Texto, List<OpcionFiltro> Opciones);
