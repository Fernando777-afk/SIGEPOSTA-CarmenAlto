USE [SIGEPOSTA_CarmenAlto_V2];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() <> N'SIGEPOSTA_CarmenAlto_V2'
    THROW 51100, N'Abre SIGEPOSTA_CarmenAlto_V2 antes de continuar.', 1;
IF OBJECT_ID(N'dbo.SigeMigracion',N'U') IS NULL RETURN;
IF NOT EXISTS (SELECT 1 FROM dbo.SigeMigracion WHERE Id=2) RETURN;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @bloqueo int;
    EXEC @bloqueo=sys.sp_getapplock @Resource=N'SIGEPOSTA.Esquema', @LockMode='Exclusive',
        @LockOwner='Transaction', @LockTimeout=10000;
    IF @bloqueo<0 THROW 51102, N'Otra instalación está en curso.', 1;

    EXEC sys.sp_executesql N'    IF EXISTS (SELECT 1 FROM dbo.Usuario WITH (TABLOCKX,HOLDLOCK) WHERE IdPersonal IS NOT NULL OR Rol IN (''Responsable'',''Estadistica''))
        THROW 51104, N''Hay cuentas o asignaciones nuevas. La reversión se detuvo para conservarlas.'', 1;
    IF EXISTS (SELECT 1 FROM dbo.Medicamento m WITH (TABLOCKX,HOLDLOCK)
        WHERE (m.Concentracion IS NOT NULL OR m.FormaFarmaceutica IS NOT NULL OR m.UnidadMedida IS NOT NULL OR m.EsEjemplo=1)
        AND NOT EXISTS (SELECT 1 FROM dbo.SigeCatalogoAcademico002 h WHERE h.IdMedicamento=m.IdMedicamento))
        THROW 51105, N''Hay datos de farmacia ingresados después de migrar. La reversión se detuvo.'', 1;
    IF EXISTS (SELECT 1 FROM dbo.SigeCatalogoAcademico002 h
        LEFT JOIN dbo.Medicamento m ON m.IdMedicamento=h.IdMedicamento
        WHERE m.IdMedicamento IS NULL OR h.Huella<>HASHBYTES(''SHA2_256'',
            (SELECT m.Nombre,m.Lote,m.StockActual,m.StockMinimo,m.FechaVencimiento,m.Concentracion,
                m.FormaFarmaceutica,m.UnidadMedida,m.EsEjemplo FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))
        OR EXISTS (SELECT 1 FROM dbo.Receta r WHERE r.IdMedicamento=m.IdMedicamento)
        OR (SELECT COUNT(*) FROM dbo.MovimientoInventario v WHERE v.IdMedicamento=m.IdMedicamento) >
            CASE WHEN h.NombreAnterior IS NULL THEN 0 ELSE 1 END)
        THROW 51106, N''El catálogo académico ya fue editado o utilizado. La reversión se detuvo.'', 1;

    DELETE m FROM dbo.Medicamento m JOIN dbo.SigeCatalogoAcademico002 h ON h.IdMedicamento=m.IdMedicamento
        WHERE h.NombreAnterior IS NULL;
    UPDATE m SET Nombre=h.NombreAnterior FROM dbo.Medicamento m
        JOIN dbo.SigeCatalogoAcademico002 h ON h.IdMedicamento=m.IdMedicamento WHERE h.NombreAnterior IS NOT NULL;
    ALTER TABLE dbo.Medicamento DROP CONSTRAINT DF_Medicamento_Ejemplo_002;
    ALTER TABLE dbo.Medicamento DROP COLUMN EsEjemplo, Concentracion, FormaFarmaceutica, UnidadMedida;
    DROP TABLE dbo.SigeCatalogoAcademico002;

    ALTER TABLE dbo.Usuario DROP CONSTRAINT FK_Usuario_Personal_002, CK_Usuario_IdPersonal_002, CK_Usuario_Rol_002;
    ALTER TABLE dbo.Usuario DROP COLUMN IdPersonal;
    UPDATE dbo.Usuario SET Rol=''Clinico'' WHERE Rol=''ProfesionalSalud'';
    ALTER TABLE dbo.Usuario ALTER COLUMN Rol varchar(14) NOT NULL;
    ALTER TABLE dbo.Usuario WITH CHECK ADD CONSTRAINT CK_Usuario_Rol
        CHECK (Rol IN (''Administrador'',''Admision'',''Clinico'',''Farmacia''));';
    DELETE FROM dbo.SigeMigracion WHERE Id=2;
    COMMIT;
    PRINT N'Migración 002 revertida.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
