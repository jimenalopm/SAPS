using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Services.Auditoria;

// HU-004 (task 4): pruebas de la tabla de bitácora y de su bloqueo de UPDATE/DELETE contra SQL Server real.
// InMemory/SQLite no ejecutan triggers ni permisos, por eso esto corre contra el Docker local.
// Crea y elimina una base independiente (SAPS_HU004_Pruebas_<guid>); nunca utiliza SAPS_DB ni la BD de RecyPlast.
// Uso: dotnet run --project tests/SAPS.Bitacora.IntegrationTests -- SAPS.Web/appsettings.json
if (args.Length != 1) throw new ArgumentException("Indique la ruta local de SAPS.Web/appsettings.json.");
var archivo = Path.GetFullPath(args[0]);
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(archivo));
var conexion = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
if (conexion.DataSource is not ("localhost,1433" or "127.0.0.1,1433"))
    throw new InvalidOperationException("Solo se permite SQL Server local (localhost,1433). Ejecución cancelada.");
const string PrefijoBase = "SAPS_HU004_Pruebas_";
conexion.InitialCatalog = PrefijoBase + Guid.NewGuid().ToString("N");
conexion.ConnectTimeout = 15;

// Mismo registro que la aplicación (Identity incluido): sin él el modelo difiere de las migraciones.
var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(conexion.ConnectionString, sql => sql.UseCompatibilityLevel(110)));   // SQL Server 2012
servicios.AddDefaultIdentity<IdentityUser>(o => o.Password.RequireNonAlphanumeric = false).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var proveedor = servicios.BuildServiceProvider();
await using var alcance = proveedor.CreateAsyncScope();
var opciones = alcance.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
var verificaciones = 0;
void Verificar(bool valor, string caso)
{
    if (!valor) throw new Exception("FALLO: " + caso);
    Console.WriteLine("OK: " + caso);
    verificaciones++;
}

SqlConnection Abrir()
{
    var c = new SqlConnection(conexion.ConnectionString);
    c.Open();
    return c;
}
object? Escalar(string sql)
{
    using var c = Abrir();
    using var cmd = new SqlCommand(sql, c);
    return cmd.ExecuteScalar();
}
void Ejecutar(string sql)
{
    using var c = Abrir();
    using var cmd = new SqlCommand(sql, c);
    cmd.ExecuteNonQuery();
}
int FilasBitacora() => (int)Escalar("SELECT COUNT(*) FROM soda.tb_Bitacora")!;
// Devuelve el número de error de SQL Server de la primera excepción, o 0 si la sentencia se ejecutó sin error.
int NumeroError(Action accion)
{
    try { accion(); return 0; }
    catch (SqlException ex) { return ex.Number; }
    catch (DbUpdateException ex) when (ex.InnerException is SqlException sql) { return sql.Number; }
}

try
{
    await using var db = new ApplicationDbContext(opciones);
    await db.Database.MigrateAsync();
    Ejecutar($"ALTER DATABASE [{conexion.InitialCatalog}] SET COMPATIBILITY_LEVEL = 110");
    Console.WriteLine($"Base temporal creada: {conexion.InitialCatalog} (compatibilidad 110 = SQL Server 2012)");
    Verificar(!db.Database.HasPendingModelChanges(), "El modelo coincide con las migraciones");

    // ---------- 1. Estructura de la tabla (HU-004 criterio 4 y 8) ----------
    var esperadas = new Dictionary<string, (string Tipo, int Largo, bool Nulo)>
    {
        ["idBitacora"] = ("int", 0, false), ["FechaHoraUtc"] = ("datetime2", 0, false),
        ["Modulo"] = ("varchar", 30, false), ["TipoEvento"] = ("varchar", 50, false), ["Recurso"] = ("varchar", 100, false),
        ["IdRegistro"] = ("varchar", 50, true), ["EsExitoso"] = ("bit", 0, false),
        ["idUsuario"] = ("nvarchar", 450, true), ["NombreUsuario"] = ("varchar", 256, true), ["Rol"] = ("varchar", 50, true),
        ["UsuarioBd"] = ("varchar", 128, false), ["DireccionIp"] = ("varchar", 45, true), ["Dispositivo"] = ("varchar", 200, true),
        ["ValorAnterior"] = ("varchar", 500, true), ["ValorNuevo"] = ("varchar", 500, true), ["Descripcion"] = ("varchar", 300, true),
    };
    var reales = new Dictionary<string, (string Tipo, int Largo, bool Nulo)>();
    using (var c = Abrir())
    using (var cmd = new SqlCommand(@"SELECT COLUMN_NAME, DATA_TYPE, ISNULL(CHARACTER_MAXIMUM_LENGTH, 0), IS_NULLABLE
        FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'soda' AND TABLE_NAME = 'tb_Bitacora'", c))
    using (var lector = cmd.ExecuteReader())
        while (lector.Read())
            reales[lector.GetString(0)] = (lector.GetString(1), lector.GetInt32(2), lector.GetString(3) == "YES");
    Verificar(reales.Count == esperadas.Count && esperadas.All(e => reales.TryGetValue(e.Key, out var r) && r == e.Value),
        "La tabla soda.tb_Bitacora tiene las 16 columnas, tipos, largos y nulabilidad del diseño");
    var idBitacora = (int)Escalar(@"SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID('soda.tb_Bitacora') AND name = 'idBitacora'")!;
    Verificar(idBitacora == 1, "idBitacora es IDENTITY");
    Verificar((int)Escalar("SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('soda.tb_Bitacora')")! == 0,
        "Sin llave foránea hacia las cuentas: el log no depende de AspNetUsers");
    Verificar((int)Escalar("SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('soda.tb_Bitacora')")! == 0,
        "Sin CHECK sobre Modulo ni TipoEvento: un evento nuevo no requiere alterar la tabla");
    Verificar((int)Escalar("SELECT COUNT(*) FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('soda.tb_Bitacora')")! == 2,
        "Defaults de la base de datos: FechaHoraUtc y UsuarioBd");
    Verificar((int)Escalar(@"SELECT COUNT(*) FROM sys.triggers WHERE name = 'tr_BitacoraSoloLectura'
        AND parent_id = OBJECT_ID('soda.tb_Bitacora') AND is_instead_of_trigger = 1 AND is_disabled = 0")! == 1,
        "Existe el trigger INSTEAD OF tr_BitacoraSoloLectura y está habilitado");
    Verificar((int)Escalar(@"SELECT COUNT(*) FROM sys.database_permissions p
        WHERE p.major_id = OBJECT_ID('soda.tb_Bitacora') AND p.grantee_principal_id = DATABASE_PRINCIPAL_ID('public')
          AND p.state = 'D' AND p.permission_name IN ('UPDATE', 'DELETE')")! == 2,
        "DENY UPDATE y DENY DELETE al rol public");

    // ---------- 2. Escritura por el servicio real (task 5) ----------
    var accesor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
    var servicio = new ServicioBitacora(opciones, accesor, NullLogger<ServicioBitacora>.Instance);
    var columnasAntes = reales.Count;
    Verificar(await servicio.RegistrarAsync(new EventoBitacora("Seguridad", "InicioSesion", "Sesion", false)
    {
        NombreUsuario = "EMP001",
        Descripcion = "Credenciales incorrectas",
        DireccionIp = "10.0.0.5",
        Dispositivo = "Navegador de prueba",
    }), "El servicio escribe un inicio de sesión fallido");
    Verificar(await servicio.RegistrarAsync(new EventoBitacora("Pedidos", "OrdenAnulada", "tb_Pedido", true)
    {
        IdRegistro = "345",
        ValorAnterior = "Registrado",
        ValorNuevo = "Anulado",
    }), "El servicio escribe un tipo de evento nuevo (OrdenAnulada)");
    Verificar(FilasBitacora() == 2, "Quedan 2 filas en la bitácora");
    var columnasDespues = (int)Escalar("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'soda' AND TABLE_NAME = 'tb_Bitacora'")!;
    Verificar(columnasAntes == columnasDespues, "El evento nuevo no cambió la estructura de la tabla (criterio 8)");
    Verificar((int)Escalar(@"SELECT COUNT(*) FROM soda.tb_Bitacora WHERE TipoEvento = 'OrdenAnulada' AND EsExitoso = 1
        AND ValorAnterior = 'Registrado' AND ValorNuevo = 'Anulado'")! == 1, "El evento nuevo se guardó con sus valores anterior y nuevo");
    Verificar((int)Escalar(@"SELECT COUNT(*) FROM soda.tb_Bitacora WHERE FechaHoraUtc >= DATEADD(MINUTE, -2, SYSUTCDATETIME())
        AND FechaHoraUtc <= DATEADD(MINUTE, 1, SYSUTCDATETIME())")! == 2, "FechaHoraUtc la asigna la base de datos en UTC");
    Verificar((int)Escalar("SELECT COUNT(*) FROM soda.tb_Bitacora WHERE UsuarioBd = SUSER_SNAME()")! == 2,
        "UsuarioBd la asigna la base de datos (SUSER_SNAME())");

    // ---------- 3. UPDATE y DELETE rechazados, como dbo/sa (HU-004 criterios 6 y 7) ----------
    var antes = FilasBitacora();
    Verificar(NumeroError(() => Ejecutar("UPDATE soda.tb_Bitacora SET Descripcion = 'cambiado'")) == 50001,
        "UPDATE por T-SQL rechazado por el trigger (error 50001), incluso para sa");
    Verificar(NumeroError(() => Ejecutar("DELETE FROM soda.tb_Bitacora")) == 50001,
        "DELETE por T-SQL rechazado por el trigger (error 50001), incluso para sa");
    Verificar((int)Escalar("SELECT COUNT(*) FROM soda.tb_Bitacora WHERE Descripcion = 'cambiado'")! == 0 && FilasBitacora() == antes,
        "Tras los intentos, las filas siguen intactas");

    // ---------- 4. Mismo bloqueo por Entity Framework (el camino real de la aplicación) ----------
    await using (var ef = new ApplicationDbContext(opciones))
    {
        var fila = await ef.Bitacoras.FirstAsync();
        fila.Descripcion = "cambiado por EF";
        var error = NumeroError(() => ef.SaveChangesAsync().GetAwaiter().GetResult());
        Verificar(error == 50001, "SaveChanges con una fila modificada rechazado (50001)");
    }
    await using (var ef = new ApplicationDbContext(opciones))
    {
        var errorUpdate = NumeroError(() => ef.Bitacoras.ExecuteUpdateAsync(s => s.SetProperty(b => b.Descripcion, "x")).GetAwaiter().GetResult());
        var errorDelete = NumeroError(() => ef.Bitacoras.ExecuteDeleteAsync().GetAwaiter().GetResult());
        Verificar(errorUpdate == 50001, "ExecuteUpdate rechazado (50001)");
        Verificar(errorDelete == 50001, "ExecuteDelete rechazado (50001)");
    }
    await using (var ef = new ApplicationDbContext(opciones))
    {
        var fila = await ef.Bitacoras.FirstAsync();
        ef.Bitacoras.Remove(fila);
        Verificar(NumeroError(() => ef.SaveChangesAsync().GetAwaiter().GetResult()) == 50001, "SaveChanges con una fila eliminada rechazado (50001)");
    }
    Verificar(FilasBitacora() == antes, "Tras los intentos por EF, el conteo no cambió");

    // ---------- 5. Segunda capa: permisos (cuenta normal de la BD) ----------
    // Usuario sin login con GRANT explícito de UPDATE/DELETE: el DENY a public debe ganar (DENY prevalece sobre GRANT).
    Ejecutar("CREATE USER [usr_prueba_bitacora] WITHOUT LOGIN");
    Ejecutar("GRANT SELECT, INSERT, UPDATE, DELETE ON soda.tb_Bitacora TO [usr_prueba_bitacora]");
    int ComoUsuario(string sql)
    {
        using var c = Abrir();
        using (var cmd = new SqlCommand("EXECUTE AS USER = 'usr_prueba_bitacora'", c)) cmd.ExecuteNonQuery();
        try
        {
            using var cmd = new SqlCommand(sql, c);
            cmd.ExecuteNonQuery();
            return 0;
        }
        catch (SqlException ex) { return ex.Number; }
        finally { using var revert = new SqlCommand("REVERT", c); revert.ExecuteNonQuery(); }
    }
    Verificar(ComoUsuario("INSERT INTO soda.tb_Bitacora (Modulo, TipoEvento, Recurso, EsExitoso) VALUES ('Seguridad', 'PruebaPermisos', 'Prueba', 1)") == 0,
        "Cuenta normal de la BD: INSERT permitido");
    Verificar(ComoUsuario("SELECT COUNT(*) FROM soda.tb_Bitacora") == 0, "Cuenta normal de la BD: SELECT permitido");
    Verificar(ComoUsuario("UPDATE soda.tb_Bitacora SET Descripcion = 'x'") == 229,
        "Cuenta normal de la BD: UPDATE denegado por permisos (229) aunque tenga GRANT; DENY a public prevalece");
    Verificar(ComoUsuario("DELETE FROM soda.tb_Bitacora") == 229,
        "Cuenta normal de la BD: DELETE denegado por permisos (229) aunque tenga GRANT; DENY a public prevalece");
    Verificar(ComoUsuario("TRUNCATE TABLE soda.tb_Bitacora") is 229 or 1088,
        "Cuenta normal de la BD: TRUNCATE denegado (sin permiso ALTER sobre la tabla)");
    Verificar(FilasBitacora() == antes + 1, "Solo la fila insertada por la cuenta normal; nada se modificó ni borró");

    // ---------- 6. Límite conocido (docs/Bitacora-Diseno-HU004-HU015.md, sección 7) ----------
    // Se ejecuta al final porque vacía la tabla. No es una falla de la prueba: documenta lo que el trigger NO puede impedir.
    var errorTruncate = NumeroError(() => Ejecutar("TRUNCATE TABLE soda.tb_Bitacora"));
    Console.WriteLine(errorTruncate == 0
        ? "LÍMITE CONOCIDO: sa/dbo SÍ puede hacer TRUNCATE TABLE (no dispara triggers). Mitigación: la aplicación debe conectarse con una cuenta sin db_owner/sysadmin."
        : $"LÍMITE: TRUNCATE rechazado con el error {errorTruncate}.");

    Console.WriteLine($"RESULTADO: {verificaciones} verificaciones correctas.");
}
finally
{
    // Limpieza: solo se elimina la base temporal creada por esta ejecución.
    if (!conexion.InitialCatalog.StartsWith(PrefijoBase))
        throw new InvalidOperationException("Limpieza cancelada: la base no es la temporal creada por esta ejecución.");
    SqlConnection.ClearAllPools();
    await using var limpieza = new ApplicationDbContext(opciones);
    await limpieza.Database.EnsureDeletedAsync();
    Console.WriteLine("Base temporal de pruebas eliminada.");
}
