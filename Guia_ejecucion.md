1. para ejecutar el proyecto debe tener instalado dotnet

    winget install Microsoft.DotNet.SDK.8

    dotnet --version

    Esto instala el SDK de .NET 8 (LTS). Si quieres otra versión, cambia el número (por ejemplo Microsoft.DotNet.SDK.9).

---

2. instalar tools necesarios

    dotnet tool install --global dotnet-ef

    Si indica que ya está instalada, puedes continuar sin problemas.

---

3. Confirmar la cadena de conexión

    Asegúrate de que el archivo appsettings.json en la raíz de SAPS.Web tenga exactamente esta cadena de conexión con la contraseña GarfieldPapuPro1234!

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

---

4. Aplicar las migraciones de la base de datos

    Como tu proyecto ya cuenta con la carpeta Migrations, ejecuta el siguiente comando para construir el esquema de tablas dentro de la base de datos SAPS_Db

    dotnet ef database update

---

5. ejecutar el la aplicacion

    Iniciar el servidor web local

    dotnet run

    O si prefieres que la aplicación detecte cambios en tus vistas Razor en tiempo real sin reiniciar manualmente:

    dotnet watch

---

6. Abrir en el navegador
    En la salida de la consola verás las URLs locales asignadas, por ejemplo:
    Building...
    info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5120

Nota: normalmente en en el puerto 5120
http://localhost:5120

