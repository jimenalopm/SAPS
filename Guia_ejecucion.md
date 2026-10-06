# Guía de Instalación y Ejecución del Proyecto SAPS

## 1. Instalar .NET

Para ejecutar el proyecto es necesario tener instalado .NET.

```powershell
winget install Microsoft.DotNet.SDK.8
```

Verifica la instalación:

```powershell
dotnet --version
```

Esto instala el SDK de .NET 8 (LTS). Si necesitas otra versión, cambia el número (por ejemplo, `Microsoft.DotNet.SDK.9`).

---

## 2. Crear el contenedor de la base de datos

Antes de correr el proyecto, es necesario crear el contenedor de Docker con la base de datos. Para eso se ha creado el script `crear_db.bat`, el cual crea el contenedor con SQL Server y genera la base de datos.

Ejecuta el script:

```powershell
crear_db.bat
```

> **Importante:** este paso es vital, ya que sin el contenedor en ejecución la aplicación no podrá conectarse a SQL Server.

---

## 3. Instalar las herramientas necesarias

```powershell
dotnet tool install --global dotnet-ef
```

Si el sistema indica que la herramienta ya está instalada, puedes continuar sin problemas.

---

## 4. Configurar la cadena de conexión

La cadena de conexión con la contraseña no se guarda en `appsettings.json`, sino en los *user secrets* de .NET (son individuales por máquina y no se suben al repo). Desde la carpeta `SAPS.Web`, ejecuta:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=SAPS_DB;User Id=sa;Password=TU_CLAVE;Encrypt=False;TrustServerCertificate=True;"
```

Reemplaza `TU_CLAVE` por la contraseña de `sa` que elegiste al crear el contenedor. El script `crear_db.bat` la pide al ejecutarse.

> **Importante:** la contraseña nunca se escribe en archivos del repo. El valor `CONFIGURAR_EN_USER_SECRETS` que aparece en `appsettings.json` es solo un marcador; el valor real lo toma la aplicación de los *user secrets*.

---

## 5. Aplicar las migraciones de la base de datos

Ejecuta el siguiente comando para construir el esquema de tablas dentro de la base de datos `SAPS_DB` (esquemas `soda` y `rrhh`, ver `docs/BaseDatos-Estandares-SQL2012.md`):

```powershell
dotnet ef database update
```

---

## 6. Ejecutar la aplicación

Para iniciar el servidor web local:

```powershell
dotnet run
```

O, si prefieres que la aplicación detecte cambios en tus vistas Razor en tiempo real sin necesidad de reiniciar manualmente:

```powershell
dotnet watch
```

---

## 7. Abrir en el navegador

En la salida de la consola verás las URLs locales asignadas, por ejemplo:

```
Building...
info: Microsoft.Hosting.Lifetime[14]
  Now listening on: https://localhost:5120
```

**Nota:** normalmente el proyecto se expone en el puerto **5120**:

```
http://localhost:5120
```

---

## 8. Protección: datos de prueba solo en base local

Al iniciar en Development, la aplicación siembra datos de prueba (usuarios `EMP001`, `SODA001` y `ADM001` con clave de desarrollo, y los colaboradores ficticios `DEMO001` a `DEMO003`, que se insertan en `rrhh.tb_Colaborador`). Para no contaminar una base real, **solo lo hace si el entorno es Development y el servidor de `DefaultConnection` es local** (`localhost`, `127.0.0.1`, `.`, `(local)`, `(localdb)` o el contenedor Docker de la sección 2, que usa `localhost,1433`). Cualquier otro servidor, como el de RecyPlast (`10.195.13.2`), se considera base real.

Si la base no es local, la aplicación no crea ni modifica usuarios, no inserta los colaboradores DEMO (en la base real solo existen los colaboradores reales) y escribe en el log: `Sembrado de datos de prueba omitido: la base no es local.` Los roles (`Administrador`, `RecursosHumanos`, `Soda`, `Usuario`) se siguen creando solo si faltan, porque la aplicación los necesita para funcionar; esto no toca usuarios ni borra datos existentes.

La lógica está en `SAPS.Web/Data/SembradoPrueba.cs` y sus pruebas en `SAPS.Tests/SembradoPruebaTests.cs`. El script `seed_catalogo.sql` es manual y no lo ejecuta la aplicación: no lo corra contra una base real.

---

## 9. Colaboradores (RNF-006)

Los colaboradores salen de la tabla `rrhh.tb_Colaborador` de `SAPS_DB`, que administra RH desde SAPS (la pantalla de administración aún no existe). El pedido busca por código; si digita `842` se busca `0000000842`. Un código que no existe muestra «Colaborador no encontrado» y uno inactivo muestra «Colaborador inactivo» y no deja registrar el pedido.

La carga inicial se hace una sola vez con `tools/cargar_colaboradores.py` (lee `C:\SAPS-datos\Codigo.xlsm` y genera `datos-locales/colaboradores_inserts.sql`). Esa carpeta está en `.gitignore`: el repo es público y el archivo trae nombres reales, **no se sube**. Se ejecuta con `sqlcmd -I -f 65001 -d SAPS_DB -i colaboradores_inserts.sql` y se puede repetir sin duplicar.

Mientras no exista la pantalla, RH da de baja o de alta con estas consultas:

```sql
UPDATE rrhh.tb_Colaborador SET EstaActivo = 0 WHERE Codigo = '0000000XXX';  -- baja
UPDATE rrhh.tb_Colaborador SET EstaActivo = 1 WHERE Codigo = '0000000XXX';  -- alta
```
