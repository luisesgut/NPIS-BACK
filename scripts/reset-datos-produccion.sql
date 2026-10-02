/*
  ============================================================================================
  Deja la base NpiManagement en blanco para arrancar el piloto.
  ============================================================================================

  SE CONSERVA
    - La cuenta indicada en @CuentaQueSeConserva (debe existir, estar activa y ser AdminTotal).
    - Roles, catálogos y sus opciones, cotizaciones e historial de migraciones.

  SE BORRA
    - Todos los NPIs y lo que cuelga de ellos: comentarios, adjuntos (sólo sus registros),
      auditoría, cronómetros, factibilidades, versiones de PrintCard y sus aprobaciones,
      solicitudes de cambio e información.
    - Notificaciones, reportes y sugerencias, y sesiones abiertas de las demás cuentas.
    - Todas las demás cuentas, con sus roles y claims.
    - El folio se reinicia: el siguiente NPI que se envíe será NPI-0001.

  CÓMO USARLO
    1. Pide al equipo que administra el SQL Server un respaldo completo de NpiManagement ANTES.
    2. Escribe tu correo en @CuentaQueSeConserva y deja @Confirmar = 0. Ejecuta el script:
       muestra cuántas filas hay antes y cuántas quedarían después, y deshace todo al final.
       En este modo NO cambia nada.
    3. Si los números cuadran, cambia @Confirmar = 1 y vuelve a ejecutarlo.
    4. Los archivos no viven en la base: después, en el servidor de la API, corre
       limpiar-adjuntos.ps1 para vaciar la carpeta de adjuntos.

  Si cualquier instrucción falla, SET XACT_ABORT deshace la transacción completa: nunca queda a medias.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CuentaQueSeConserva nvarchar(256) = N'tu.correo@bioflex.mx';
DECLARE @Confirmar bit = 0;

-------------------------------------------------------------------------------------------------
-- Verificaciones previas: si la cuenta que se conserva no puede administrar, no se toca nada.
-------------------------------------------------------------------------------------------------
DECLARE @AdminId uniqueidentifier =
    (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedEmail = UPPER(@CuentaQueSeConserva));

IF @AdminId IS NULL
    THROW 50001, N'No existe una cuenta con ese correo. Revisa @CuentaQueSeConserva.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.AspNetUserRoles ur
    JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
    WHERE ur.UserId = @AdminId AND r.Name = N'AdminTotal')
    THROW 50002, N'La cuenta indicada no es AdminTotal: quedarías sin nadie que pueda aprobar altas.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE Id = @AdminId AND Status = N'Active')
    THROW 50003, N'La cuenta indicada no está activa.', 1;

-------------------------------------------------------------------------------------------------
PRINT N'== ANTES ==';
-------------------------------------------------------------------------------------------------
SELECT Tabla, Filas FROM (
              SELECT 1 AS Orden, N'Npis' AS Tabla, COUNT(*) AS Filas FROM dbo.Npis
    UNION ALL SELECT 2, N'NpiComments', COUNT(*) FROM dbo.NpiComments
    UNION ALL SELECT 3, N'Attachments', COUNT(*) FROM dbo.Attachments
    UNION ALL SELECT 4, N'AuditEvents', COUNT(*) FROM dbo.AuditEvents
    UNION ALL SELECT 5, N'TimerEvents', COUNT(*) FROM dbo.TimerEvents
    UNION ALL SELECT 6, N'FeasibilityReviews', COUNT(*) FROM dbo.FeasibilityReviews
    UNION ALL SELECT 7, N'PrintCardVersions', COUNT(*) FROM dbo.PrintCardVersions
    UNION ALL SELECT 8, N'PrintCardApprovals', COUNT(*) FROM dbo.PrintCardApprovals
    UNION ALL SELECT 9, N'ChangeRequests', COUNT(*) FROM dbo.ChangeRequests
    UNION ALL SELECT 10, N'InformationRequests', COUNT(*) FROM dbo.InformationRequests
    UNION ALL SELECT 11, N'Notifications', COUNT(*) FROM dbo.Notifications
    UNION ALL SELECT 12, N'FeedbackReports', COUNT(*) FROM dbo.FeedbackReports
    UNION ALL SELECT 13, N'RefreshTokens', COUNT(*) FROM dbo.RefreshTokens
    UNION ALL SELECT 14, N'AspNetUsers', COUNT(*) FROM dbo.AspNetUsers
    UNION ALL SELECT 15, N'Catalogs (se conserva)', COUNT(*) FROM dbo.Catalogs
    UNION ALL SELECT 16, N'CatalogOptions (se conserva)', COUNT(*) FROM dbo.CatalogOptions
    UNION ALL SELECT 17, N'Quotations (se conserva)', COUNT(*) FROM dbo.Quotations
) AS conteo
ORDER BY Orden;

BEGIN TRANSACTION;

-- Tienen llave foránea RESTRICT hacia las cuentas: deben irse antes que ellas.
DELETE FROM dbo.FeedbackReports;
DELETE FROM dbo.Notifications;

-- En cascada: comentarios, adjuntos, auditoría, cronómetros, factibilidades, PrintCards (y sus
-- aprobaciones), solicitudes de cambio e información.
DELETE FROM dbo.Npis;

-- Sesiones de las demás cuentas, y las ya revocadas o vencidas de la tuya.
DELETE FROM dbo.RefreshTokens
WHERE UserId <> @AdminId OR RevokedAt IS NOT NULL OR ExpiresAt < SYSDATETIMEOFFSET();

-- En cascada: roles, claims, logins y tokens de Identity de esas cuentas.
DELETE FROM dbo.AspNetUsers WHERE Id <> @AdminId;

-- El siguiente folio será NPI-0001.
ALTER SEQUENCE dbo.NpiNumberSequence RESTART WITH 1;

-------------------------------------------------------------------------------------------------
PRINT N'== DESPUÉS ==';
-------------------------------------------------------------------------------------------------
SELECT Tabla, Filas FROM (
              SELECT 1 AS Orden, N'Npis' AS Tabla, COUNT(*) AS Filas FROM dbo.Npis
    UNION ALL SELECT 2, N'NpiComments', COUNT(*) FROM dbo.NpiComments
    UNION ALL SELECT 3, N'Attachments', COUNT(*) FROM dbo.Attachments
    UNION ALL SELECT 4, N'AuditEvents', COUNT(*) FROM dbo.AuditEvents
    UNION ALL SELECT 5, N'TimerEvents', COUNT(*) FROM dbo.TimerEvents
    UNION ALL SELECT 6, N'FeasibilityReviews', COUNT(*) FROM dbo.FeasibilityReviews
    UNION ALL SELECT 7, N'PrintCardVersions', COUNT(*) FROM dbo.PrintCardVersions
    UNION ALL SELECT 8, N'PrintCardApprovals', COUNT(*) FROM dbo.PrintCardApprovals
    UNION ALL SELECT 9, N'ChangeRequests', COUNT(*) FROM dbo.ChangeRequests
    UNION ALL SELECT 10, N'InformationRequests', COUNT(*) FROM dbo.InformationRequests
    UNION ALL SELECT 11, N'Notifications', COUNT(*) FROM dbo.Notifications
    UNION ALL SELECT 12, N'FeedbackReports', COUNT(*) FROM dbo.FeedbackReports
    UNION ALL SELECT 13, N'RefreshTokens', COUNT(*) FROM dbo.RefreshTokens
    UNION ALL SELECT 14, N'AspNetUsers', COUNT(*) FROM dbo.AspNetUsers
    UNION ALL SELECT 15, N'Catalogs (se conserva)', COUNT(*) FROM dbo.Catalogs
    UNION ALL SELECT 16, N'CatalogOptions (se conserva)', COUNT(*) FROM dbo.CatalogOptions
    UNION ALL SELECT 17, N'Quotations (se conserva)', COUNT(*) FROM dbo.Quotations
) AS conteo
ORDER BY Orden;

SELECT u.Email AS CuentaQueQueda, u.FullName, u.Status, r.Name AS Rol
FROM dbo.AspNetUsers u
LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId;

IF @Confirmar = 1
BEGIN
    COMMIT TRANSACTION;
    PRINT N'Limpieza CONFIRMADA. Falta vaciar la carpeta de adjuntos con limpiar-adjuntos.ps1.';
END
ELSE
BEGIN
    ROLLBACK TRANSACTION;
    PRINT N'ENSAYO: no se cambió nada. Si los números cuadran, pon @Confirmar = 1 y vuelve a ejecutar.';
END
