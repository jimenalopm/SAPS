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

## 4. Confirmar la cadena de conexión

Asegúrate de que el archivo `appsettings.json`, ubicado en la raíz de `SAPS.Web`, tenga exactamente esta cadena de conexión con la contraseña `GarfieldPapuPro1234!`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=SAPS_Db;User Id=sa;Password=GarfieldPapuPro1234!;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## 5. Aplicar las migraciones de la base de datos

Como el proyecto ya cuenta con la carpeta `Migrations`, ejecuta el siguiente comando para construir el esquema de tablas dentro de la base de datos `SAPS_Db`:

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