# Política de respaldos con RMAN (Windows)

EIF-402 Administración de Bases de Datos — Laboratorio Política con RMAN.

## Requerimientos y cómo se cumplen

| Requerimiento | Implementación |
|---|---|
| Retención de 4 semanas | `CONFIGURE RETENTION POLICY TO RECOVERY WINDOW OF 28 DAYS;` + `DELETE NOPROMPT OBSOLETE` en cada respaldo de base de datos |
| Incremental martes, jueves y sábado | `backup_incremental_L1.rman` (`BACKUP INCREMENTAL LEVEL 1 DATABASE`), programado `/sc WEEKLY /d TUE,THU,SAT` |
| Transaccional todos los días | `backup_transaccional.rman` (`BACKUP ARCHIVELOG ALL ... DELETE INPUT`), programado `/sc DAILY` |

Un incremental de nivel 1 necesita una **base de nivel 0**. Por eso se agrega un
respaldo base nivel 0 el **domingo** (`backup_semanal_base_L0.rman`). Sin él,
RMAN haría automáticamente un nivel 0 completo el primer martes y la cadena de
incrementales crecería sin base periódica.

## Calendario

| Día | 21:00 | 23:00 |
|---|---|---|
| Domingo | Transaccional | Base Nivel 0 |
| Lunes | Transaccional | — |
| Martes | Transaccional | Incremental Nivel 1 |
| Miércoles | Transaccional | — |
| Jueves | Transaccional | Incremental Nivel 1 |
| Viernes | Transaccional | — |
| Sábado | Transaccional | Incremental Nivel 1 |

## Archivos

| Archivo | Uso |
|---|---|
| `01_prerrequisitos.sql` | Verifica ARCHIVELOG y activa Block Change Tracking (SQL*Plus) |
| `00_configuracion_politica.rman` / `ejecutar_configuracion.bat` | Configuración global (retención 28 días, autobackup, optimización). Se ejecuta una vez |
| `backup_semanal_base_L0.rman` / `ejecutar_semanal_base_L0.bat` | Domingo: base nivel 0 + archivelogs + limpieza de obsoletos |
| `backup_incremental_L1.rman` / `ejecutar_incremental.bat` | Martes, jueves, sábado: incremental nivel 1 + archivelogs |
| `backup_transaccional.rman` / `ejecutar_transaccional.bat` | Diario: respaldo de archivelogs |
| `programar_tareas.bat` | Crea las 3 tareas en el Programador de Tareas con `schtasks` |
| `verificar_respaldos.rman` / `ejecutar_verificacion.bat` | Evidencia: `LIST BACKUP`, `REPORT OBSOLETE`, `RESTORE DATABASE VALIDATE` |

## Pasos

1. Copiar todos los archivos a `C:\oracle_scripts\` y crear `C:\oracle_scripts\backups\`.
2. Cambiar `ORACLE_SID=ORCL` en los `.bat` por el SID de su base de datos.
3. `sqlplus / as sysdba @C:\oracle_scripts\01_prerrequisitos.sql`
4. Ejecutar `ejecutar_configuracion.bat` (aplica la retención de 4 semanas).
5. Ejecutar manualmente `ejecutar_semanal_base_L0.bat` una vez para crear la primera base nivel 0.
6. Ejecutar `programar_tareas.bat` **como Administrador**.
7. Ejecutar `ejecutar_verificacion.bat` y tomar captura como evidencia.

Las bitácoras quedan en `C:\oracle_scripts\logs\` con fecha y hora en el nombre.

## Cambios respecto a la plantilla del profesor

- La plantilla usaba `RECOVERY WINDOW OF 7 DAYS` y `DELETE OBSOLETE REDUNDANCY 1`, que
  borra respaldos ignorando la ventana configurada. Aquí se usa `DELETE NOPROMPT OBSOLETE`,
  que respeta la ventana de 28 días.
- `BACKUP ARCHIVELOG ALL ... PLUS ARCHIVELOG` no es sintaxis válida; lo correcto es
  `BACKUP ... DATABASE PLUS ARCHIVELOG`.
- El transaccional usa `ALTER SYSTEM ARCHIVE LOG CURRENT` (espera a que el log quede
  archivado) en lugar de `SWITCH LOGFILE`.
