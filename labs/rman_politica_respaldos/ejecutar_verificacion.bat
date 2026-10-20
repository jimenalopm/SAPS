@echo off
set ORACLE_SID=ORCL
if not exist C:\oracle_scripts\logs mkdir C:\oracle_scripts\logs
rman target / cmdfile=C:\oracle_scripts\verificar_respaldos.rman log=C:\oracle_scripts\logs\verificacion.log
type C:\oracle_scripts\logs\verificacion.log
pause
