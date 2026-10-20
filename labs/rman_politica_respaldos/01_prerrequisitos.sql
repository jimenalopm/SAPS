-- ==========================================================
-- 01_prerrequisitos.sql  (ejecutar con: sqlplus / as sysdba @01_prerrequisitos.sql)
-- 1) Verifica modo ARCHIVELOG   2) Activa Block Change Tracking
-- ==========================================================
ARCHIVE LOG LIST;

-- Si dice "No Archive Mode", descomente y ejecute:
-- SHUTDOWN IMMEDIATE;
-- STARTUP MOUNT;
-- ALTER DATABASE ARCHIVELOG;
-- ALTER DATABASE OPEN;

-- Block Change Tracking: acelera los incrementales de nivel 1
SELECT STATUS, FILENAME FROM V$BLOCK_CHANGE_TRACKING;
ALTER DATABASE ENABLE BLOCK CHANGE TRACKING
  USING FILE 'C:\oracle_scripts\backups\block_change_tracking.bct' REUSE;
SELECT STATUS, FILENAME, BYTES FROM V$BLOCK_CHANGE_TRACKING;

EXIT;
