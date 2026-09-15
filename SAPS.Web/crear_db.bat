@echo off
title SAPS - SQL Server Docker
echo ========================================
echo     Iniciando SQL Server - SAPS
echo ========================================
echo.

docker start saps-sqlserver >nul 2>&1

if %errorlevel% neq 0 (
    echo El contenedor no existe.
    echo Creando e iniciando contenedor de SQL Server...
    echo.

    docker run -e "ACCEPT_EULA=Y" ^
    -e "MSSQL_SA_PASSWORD=GarfieldPapuPro1234!" ^
    -p 1433:1433 ^
    --name saps-sqlserver ^
    -d mcr.microsoft.com/mssql/server:2022-latest
)

echo Esperando a que el motor de SQL Server este totalmente listo...

:WAIT_LOOP
timeout /t 4 /nobreak >nul
docker exec -i saps-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "GarfieldPapuPro1234!" -C -Q "SELECT 1" >nul 2>&1
if %errorlevel% neq 0 (
    echo  - Inicializando servicio de autenticacion...
    goto WAIT_LOOP
)

echo.
echo Motor activo y autenticado. Verificando / Creando la base de datos SAPS_Db...
docker exec -i saps-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "GarfieldPapuPro1234!" -C -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SAPS_Db') BEGIN CREATE DATABASE [SAPS_Db]; PRINT 'Base de datos SAPS_Db creada exitosamente.'; END ELSE BEGIN PRINT 'La base de datos SAPS_Db ya existe.'; END"

echo.
echo ========================================
echo  SQL Server iniciado y listo para usar
echo  Servidor:      localhost,1433
echo  Base de datos: SAPS_Db
echo  Usuario:       sa
echo  Password:      GarfieldPapuPro1234!
echo ========================================
echo.
pause