using SAPS.Web.Services.Auditoria;

namespace SAPS.Web.Models.Auditoria;

// ---------- Listado (HU-004 tasks 11 y 12) ----------
public class BitacoraListadoViewModel
{
    public List<BitacoraFilaViewModel> Filas { get; set; } = [];

    // Filtros (task 12): los aplicados y las opciones de las listas desplegables.
    public BitacoraFiltros Filtros { get; set; } = new();
    public List<GrupoOpcionesFiltro> TiposPorModulo { get; set; } = [];
    public List<OpcionFiltro> Modulos { get; set; } = [];

    public int Pagina { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public int TotalRegistros { get; set; }
    public int TamanoPagina { get; set; }

    /// <summary>Posición (1-based) de la primera fila de la página, para "Mostrando X–Y de Z".</summary>
    public int PrimeraFila => TotalRegistros == 0 ? 0 : (Pagina - 1) * TamanoPagina + 1;
    public int UltimaFila => TotalRegistros == 0 ? 0 : PrimeraFila + Filas.Count - 1;
}

/// <summary>
/// Una fila de soda.tb_Bitacora lista para mostrar: textos legibles, hora de Costa Rica y un resumen
/// del cambio según el tipo de evento. Solo lectura: no hay forma de devolverla a la BD.
/// </summary>
public class BitacoraFilaViewModel
{
    public int IdBitacora { get; init; }

    public DateTime FechaHoraCostaRica { get; init; }
    public string Fecha => PresentacionBitacora.Fecha(FechaHoraCostaRica);
    public string Hora => PresentacionBitacora.Hora(FechaHoraCostaRica);

    public string TipoEvento { get; init; } = "";
    public string TipoEventoTexto { get; init; } = "";
    public string Modulo { get; init; } = "";
    public string ModuloTexto { get; init; } = "";
    public string Recurso { get; init; } = "";
    public string RecursoTexto { get; init; } = "";
    public bool EsExitoso { get; init; }

    public string? NombreUsuario { get; init; }
    public string? RolTexto { get; init; }

    /// <summary>Cambio de precio hecho directo en la BD (sin usuario de SAPS): el trigger guarda el usuario de SQL Server.</summary>
    public bool EsCambioDirectoBd { get; init; }

    /// <summary>Motivo de un inicio de sesión fallido ("Credenciales incorrectas", "Cuenta desactivada..."), tal como lo guardó el login.</summary>
    public string? MotivoFallo { get; init; }

    // Columna "Detalle": una línea principal y, debajo, el cambio (anterior → nuevo) o un texto secundario.
    public string? DetallePrincipal { get; init; }
    public string? DetalleSecundario { get; init; }
    public string? ValorAnteriorTexto { get; init; }
    public string? ValorNuevoTexto { get; init; }
    public bool MuestraCambio => ValorAnteriorTexto is not null && ValorNuevoTexto is not null;

    public static BitacoraFilaViewModel DesdeEntidad(Bitacora b)
    {
        string? principal, secundario = null, anterior = null, nuevo = null;
        var esDirectoBd = false;

        switch (b.TipoEvento)
        {
            case TiposEventoBitacora.CambioPrecio:
                principal = PresentacionBitacora.ProductoDelPrecio(b.Descripcion);
                anterior = PresentacionBitacora.Colones(b.ValorAnterior);
                nuevo = PresentacionBitacora.Colones(b.ValorNuevo);
                // Sin contexto de la aplicación, fn_UsuarioContexto() deja SUSER_SNAME() como NombreUsuario.
                esDirectoBd = b.IdUsuario is null && b.NombreUsuario == b.UsuarioBd;
                break;

            case TiposEventoBitacora.RolCambiado:
                principal = PresentacionBitacora.CuentaAfectada(b.Descripcion) ?? b.Descripcion;
                anterior = PresentacionBitacora.EtiquetaRol(b.ValorAnterior);
                nuevo = PresentacionBitacora.EtiquetaRol(b.ValorNuevo);
                break;

            case TiposEventoBitacora.CuentaCreada:
                principal = PresentacionBitacora.CuentaAfectada(b.Descripcion) ?? b.Descripcion;
                var rolAsignado = PresentacionBitacora.EtiquetaRol(b.ValorNuevo);
                secundario = rolAsignado is null ? null : $"Rol asignado: {rolAsignado}";
                break;

            case TiposEventoBitacora.InicioSesion:
            case TiposEventoBitacora.CierreSesion:
                principal = b.DireccionIp;
                secundario = PresentacionBitacora.ResumirDispositivo(b.Dispositivo);
                break;

            default:
                // Desactivar / reactivar cuenta y cualquier evento futuro: descripción y, si hay, el cambio.
                principal = PresentacionBitacora.CuentaAfectada(b.Descripcion) ?? b.Descripcion;
                anterior = b.ValorAnterior;
                nuevo = b.ValorNuevo;
                break;
        }

        var motivo = !b.EsExitoso && b.TipoEvento == TiposEventoBitacora.InicioSesion ? b.Descripcion : null;

        return new BitacoraFilaViewModel
        {
            IdBitacora = b.IdBitacora,
            FechaHoraCostaRica = PresentacionBitacora.AHoraCostaRica(b.FechaHoraUtc),
            TipoEvento = b.TipoEvento,
            TipoEventoTexto = PresentacionBitacora.EtiquetaEvento(b.TipoEvento),
            Modulo = b.Modulo,
            ModuloTexto = PresentacionBitacora.EtiquetaModulo(b.Modulo),
            Recurso = b.Recurso,
            RecursoTexto = PresentacionBitacora.EtiquetaRecurso(b.Recurso),
            EsExitoso = b.EsExitoso,
            NombreUsuario = b.NombreUsuario,
            RolTexto = PresentacionBitacora.EtiquetaRol(b.Rol),
            EsCambioDirectoBd = esDirectoBd,
            MotivoFallo = motivo,
            DetallePrincipal = principal,
            DetalleSecundario = secundario,
            ValorAnteriorTexto = anterior,
            ValorNuevoTexto = nuevo,
        };
    }
}

// ---------- Detalle (HU-004 task 12, HU-015 criterio 6) ----------
/// <summary>
/// Todos los campos de un registro de la bitácora. Lleva los filtros y la página del listado para que
/// "Volver a la bitácora" regrese al mismo punto.
/// </summary>
public class BitacoraDetalleViewModel
{
    public BitacoraFilaViewModel Fila { get; init; } = new();

    public DateTime FechaHoraUtc { get; init; }
    public string FechaHoraTexto => $"{Fila.Fecha}, {PresentacionBitacora.HoraConSegundos(Fila.FechaHoraCostaRica)}";

    public string? IdRegistro { get; init; }
    public string? IdUsuario { get; init; }
    public string UsuarioBd { get; init; } = "";
    public string? DireccionIp { get; init; }
    public string? Dispositivo { get; init; }
    public string? DispositivoResumen { get; init; }
    public string? Descripcion { get; init; }

    /// <summary>Cuenta afectada (altas, bajas y cambios de rol) o producto (cambios de precio), si aplica.</summary>
    public string? CuentaAfectada { get; init; }
    public string? Producto { get; init; }

    /// <summary>Nombre del producto o bebida sin tamaño, para el enlace al historial de precios (HU-015).</summary>
    public string? NombreArticulo { get; init; }

    // Comparación "valor anterior → valor nuevo". Se muestra si al menos uno existe.
    public string TituloCambio { get; init; } = "Valor";
    public string? ValorAnterior { get; init; }
    public string? ValorNuevo { get; init; }
    public string? Diferencia { get; init; }
    public bool TieneCambio => ValorAnterior is not null || ValorNuevo is not null;

    public bool EsCambioPrecio => Fila.TipoEvento == TiposEventoBitacora.CambioPrecio;

    // Para volver al listado con los mismos filtros y página.
    public BitacoraFiltros Filtros { get; init; } = new();
    public int Pagina { get; init; } = 1;

    public static BitacoraDetalleViewModel DesdeEntidad(Bitacora b, BitacoraFiltros filtros, int pagina)
    {
        var esPrecio = b.TipoEvento == TiposEventoBitacora.CambioPrecio;
        var esRol = b.TipoEvento is TiposEventoBitacora.RolCambiado or TiposEventoBitacora.CuentaCreada;

        string? Formatear(string? valor) =>
            esPrecio ? PresentacionBitacora.Colones(valor)
            : esRol ? PresentacionBitacora.EtiquetaRol(valor)
            : string.IsNullOrEmpty(valor) ? null : valor;

        var titulo = b.TipoEvento switch
        {
            TiposEventoBitacora.CambioPrecio => "Precio",
            TiposEventoBitacora.RolCambiado => "Rol de la cuenta",
            TiposEventoBitacora.CuentaCreada => "Rol asignado",
            TiposEventoBitacora.CuentaDesactivada or TiposEventoBitacora.CuentaReactivada => "Estado de la cuenta",
            _ => "Valor",
        };

        return new BitacoraDetalleViewModel
        {
            Fila = BitacoraFilaViewModel.DesdeEntidad(b),
            FechaHoraUtc = b.FechaHoraUtc,
            IdRegistro = b.IdRegistro,
            IdUsuario = b.IdUsuario,
            UsuarioBd = b.UsuarioBd,
            DireccionIp = b.DireccionIp,
            Dispositivo = b.Dispositivo,
            DispositivoResumen = PresentacionBitacora.ResumirDispositivo(b.Dispositivo),
            Descripcion = b.Descripcion,
            CuentaAfectada = PresentacionBitacora.CuentaAfectada(b.Descripcion),
            Producto = esPrecio ? PresentacionBitacora.ProductoDelPrecio(b.Descripcion) : null,
            NombreArticulo = esPrecio ? PresentacionBitacora.NombreArticuloDelPrecio(b.Descripcion) : null,
            TituloCambio = titulo,
            ValorAnterior = Formatear(b.ValorAnterior),
            ValorNuevo = Formatear(b.ValorNuevo),
            Diferencia = esPrecio ? PresentacionBitacora.DiferenciaPrecio(b.ValorAnterior, b.ValorNuevo) : null,
            Filtros = filtros,
            Pagina = pagina,
        };
    }
}
