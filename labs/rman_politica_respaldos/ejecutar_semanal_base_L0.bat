@echo off
REM Domingo: respaldo base Incremental Nivel 0
set ORACLE_SID=ORCL
REM set ORACLE_HOME=C:\app\oracle\product\19.0.0\dbhome_1
REM set PATH=%ORACLE_HOME%\bin;%PATH%

if not exist C:\oracle_scripts\logs mkdir C:\oracle_scripts\logs
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set FECHA=%%i

rman target / cmdfile=C:\oracle_scripts\backup_semanal_base_L0.rman log=C:\oracle_scripts\logs\semanal_L0_%FECHA%.log
exit /b %ERRORLEVEL%
