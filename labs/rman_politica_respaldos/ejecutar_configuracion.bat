@echo off
REM Aplica la configuracion global de RMAN (retencion 28 dias). Ejecutar una sola vez.
set ORACLE_SID=ORCL
if not exist C:\oracle_scripts\backups mkdir C:\oracle_scripts\backups
if not exist C:\oracle_scripts\logs mkdir C:\oracle_scripts\logs
rman target / cmdfile=C:\oracle_scripts\00_configuracion_politica.rman log=C:\oracle_scripts\logs\configuracion.log
type C:\oracle_scripts\logs\configuracion.log
pause
