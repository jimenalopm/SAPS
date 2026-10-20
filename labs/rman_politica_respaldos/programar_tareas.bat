@echo off
REM Crea las tareas en el Programador de Tareas de Windows.
REM Ejecutar como Administrador (clic derecho > Ejecutar como administrador).

REM Transaccional: todos los dias a las 21:00
schtasks /create /f /tn "Oracle_Backup_Transaccional" /tr "C:\oracle_scripts\ejecutar_transaccional.bat" /sc DAILY /st 21:00 /ru SYSTEM /rl HIGHEST

REM Incremental Nivel 1: martes, jueves y sabado a las 23:00
schtasks /create /f /tn "Oracle_Backup_Incremental_L1" /tr "C:\oracle_scripts\ejecutar_incremental.bat" /sc WEEKLY /d TUE,THU,SAT /st 23:00 /ru SYSTEM /rl HIGHEST

REM Base Nivel 0: domingo a las 23:00
schtasks /create /f /tn "Oracle_Backup_Semanal_L0" /tr "C:\oracle_scripts\ejecutar_semanal_base_L0.bat" /sc WEEKLY /d SUN /st 23:00 /ru SYSTEM /rl HIGHEST

schtasks /query /tn "Oracle_Backup_Transaccional"
schtasks /query /tn "Oracle_Backup_Incremental_L1"
schtasks /query /tn "Oracle_Backup_Semanal_L0"
pause
