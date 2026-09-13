# SAPS - Sistema de Administración para la Soda

Sistema para RecyPlast: registro de pedidos, gestión de menús/catálogo y reportes para planilla.

## Stack

- .NET 10 (ASP.NET Core MVC + Identity)
- Entity Framework Core
- SQL Server

## Requisitos previos

- .NET 10 SDK instalado (verificar con `dotnet --version`, debe mostrar 10.x)
- Un SQL Server accesible

### Sobre el SQL Server según tu sistema operativo

- **Windows:** se puede usar SQL Server Express o Docker.
- **Mac:** usar Docker (cambiar `TuPassword123` por la contraseña que prefieran):

```
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=TuPassword123!" -p 1433:1433 --name saps-sql -d mcr.microsoft.com/mssql/server:2022-latest
```

## Configuración inicial

1. Clonar el repo y entrar a la carpeta del proyecto:

```
cd SAPS.Web
```

2. Configurar una cadena de conexión local (esto es individual por máquina, no se sube al repo):

```
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=TU_SERVIDOR;Database=SapsDb;User Id=sa;Password=TU_PASSWORD;TrustServerCertificate=True;"
```

Si usan SQL Server Express con autenticación de Windows (instalación por defecto), la cadena cambia a este formato:

```
Server=localhost\SQLEXPRESS;Database=SapsDb;Trusted_Connection=True;TrustServerCertificate=True;
```

3. Instalar la herramienta de migraciones:

```
dotnet tool install --global dotnet-ef
```

4. Aplicar las migraciones para crear las tablas:

```
dotnet ef database update
```

5. Correr el proyecto:

```
dotnet run
```

## Usuario de prueba

En ambiente de desarrollo, el sistema crea automáticamente un usuario de prueba:

- Código de empleado: `EMP001`
- Contraseña: `Prueba123`

## Estado actual

- HU-001 (Autenticar usuarias con usuario y contraseña): backend funcional. Login por código de empleado, bloqueo tras 5 intentos fallidos, contraseña alfanumérica mínimo 6 caracteres.
- Vista de login: pendiente de diseño.
