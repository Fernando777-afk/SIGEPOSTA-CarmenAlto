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
IF OBJECT_ID(N'dbo.SigeVersion',N'U') IS NULL
    THROW 51101, N'Primero ejecuta el script base de SIGEPOSTA.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @bloqueo int;
    EXEC @bloqueo=sys.sp_getapplock @Resource=N'SIGEPOSTA.Esquema', @LockMode='Exclusive',
        @LockOwner='Transaction', @LockTimeout=10000;
    IF @bloqueo<0 THROW 51102, N'Otra instalación está en curso.', 1;

    IF OBJECT_ID(N'dbo.SigeMigracion', N'U') IS NULL
        CREATE TABLE dbo.SigeMigracion (Id int NOT NULL PRIMARY KEY, Aplicada datetime2(0) NOT NULL DEFAULT SYSDATETIME());
    IF EXISTS (SELECT 1 FROM dbo.SigeMigracion WHERE Id=2)
    BEGIN
        COMMIT;
        PRINT N'La migración 002 ya está aplicada.';
        RETURN;
    END;

    DECLARE @rolAnterior sysname;
    SELECT @rolAnterior=name FROM sys.check_constraints
      WHERE parent_object_id=OBJECT_ID(N'dbo.Usuario') AND definition LIKE N'%Clinico%';
    IF @rolAnterior IS NULL THROW 51103, N'No se reconoció la restricción de roles inicial.', 1;
    DECLARE @quitar nvarchar(max)=N'ALTER TABLE dbo.Usuario DROP CONSTRAINT '+QUOTENAME(@rolAnterior);
    EXEC sys.sp_executesql @quitar;

    ALTER TABLE dbo.Usuario ALTER COLUMN Rol varchar(24) NOT NULL;
    ALTER TABLE dbo.Usuario ADD IdPersonal int NULL CONSTRAINT FK_Usuario_Personal_002 REFERENCES dbo.PersonalSalud(IdPersonal);
    ALTER TABLE dbo.Medicamento ADD Concentracion nvarchar(40) NULL, FormaFarmaceutica nvarchar(80) NULL,
        UnidadMedida nvarchar(30) NULL, EsEjemplo bit NOT NULL CONSTRAINT DF_Medicamento_Ejemplo_002 DEFAULT 0;
    CREATE TABLE dbo.SigeCatalogoAcademico002 (
        IdMedicamento int NOT NULL PRIMARY KEY, NombreAnterior nvarchar(100) NULL, Huella binary(32) NULL
    );

    EXEC sys.sp_executesql N'UPDATE dbo.Usuario SET Rol=''ProfesionalSalud'' WHERE Rol=''Clinico'';
ALTER TABLE dbo.Usuario WITH CHECK ADD CONSTRAINT CK_Usuario_Rol_002
    CHECK (Rol IN (''Administrador'',''Responsable'',''Admision'',''ProfesionalSalud'',''Farmacia'',''Estadistica''));
ALTER TABLE dbo.Usuario WITH CHECK ADD CONSTRAINT CK_Usuario_IdPersonal_002
    CHECK (IdPersonal IS NULL OR Rol=''ProfesionalSalud'');

DECLARE @Catalogo TABLE (
    Codigo char(3) PRIMARY KEY, Nombre nvarchar(100), Concentracion nvarchar(40),
    Forma nvarchar(80), Unidad nvarchar(30), Anterior char(1), Dias int
);
INSERT @Catalogo VALUES
    (''PAR'', N''Paracetamol'', N''500 mg'', N''Tableta'', N''Tableta'', ''A'', 180),
    (''LOR'', N''Loratadina'', N''10 mg'', N''Tableta'', N''Tableta'', ''B'', 24),
    (''AMO'', N''Amoxicilina'', N''500 mg'', N''Tableta'', N''Tableta'', ''C'', -1),
    (''IBU'', N''Ibuprofeno'', N''400 mg'', N''Tableta'', N''Tableta'', NULL, 365),
    (''MET'', N''Metformina clorhidrato'', N''850 mg'', N''Tableta'', N''Tableta'', NULL, 365),
    (''OME'', N''Omeprazol'', N''20 mg'', N''Tableta de liberación modificada'', N''Tableta'', NULL, 365),
    (''SRO'', N''Sales de rehidratación oral'', N''20,5 g/L'', N''Polvo para solución oral'', N''Sobre'', NULL, 365),
    (''ALB'', N''Albendazol'', N''400 mg'', N''Tableta'', N''Tableta'', NULL, 365),
    (''SAL'', N''Salbutamol (como sulfato)'', N''100 mcg/dosis'', N''Aerosol para inhalación (200 dosis)'', N''Inhalador'', NULL, 365);

-- Se reutilizan las filas iniciales solo si siguen siendo datos de prueba sin uso operativo.
UPDATE m SET Nombre=c.Nombre, Concentracion=c.Concentracion, FormaFarmaceutica=c.Forma,
    UnidadMedida=c.Unidad, EsEjemplo=1
OUTPUT inserted.IdMedicamento, deleted.Nombre
INTO dbo.SigeCatalogoAcademico002 (IdMedicamento, NombreAnterior)
FROM dbo.Medicamento m JOIN @Catalogo c ON m.Lote=''FICT-''+c.Anterior
WHERE m.Nombre=N''Medicamento de prueba ''+c.Anterior
  AND NOT EXISTS (SELECT 1 FROM dbo.Receta r WHERE r.IdMedicamento=m.IdMedicamento)
  AND EXISTS (SELECT 1 FROM dbo.MovimientoInventario v WHERE v.IdMedicamento=m.IdMedicamento
      AND v.Tipo=''Ingreso'' AND v.Motivo=N''Inventario inicial ficticio'' AND v.Cantidad=m.StockActual)
  AND (SELECT COUNT(*) FROM dbo.MovimientoInventario v WHERE v.IdMedicamento=m.IdMedicamento)=1;

INSERT dbo.Medicamento (Nombre,Lote,StockActual,StockMinimo,FechaVencimiento,
    Concentracion,FormaFarmaceutica,UnidadMedida,EsEjemplo)
OUTPUT inserted.IdMedicamento, NULL INTO dbo.SigeCatalogoAcademico002 (IdMedicamento, NombreAnterior)
SELECT c.Nombre, ''EJEMPLO-''+c.Codigo, 0, 5, DATEADD(DAY,c.Dias,CONVERT(date,SYSDATETIME())),
       c.Concentracion,c.Forma,c.Unidad,1
FROM @Catalogo c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Medicamento m WHERE m.Nombre=c.Nombre AND m.Lote=''EJEMPLO-''+c.Codigo)
  AND NOT EXISTS (SELECT 1 FROM dbo.Medicamento m JOIN dbo.SigeCatalogoAcademico002 h
      ON h.IdMedicamento=m.IdMedicamento WHERE m.Lote=''FICT-''+c.Anterior);

UPDATE h SET Huella=HASHBYTES(''SHA2_256'',
    (SELECT m.Nombre,m.Lote,m.StockActual,m.StockMinimo,m.FechaVencimiento,m.Concentracion,
        m.FormaFarmaceutica,m.UnidadMedida,m.EsEjemplo FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES))
FROM dbo.SigeCatalogoAcademico002 h JOIN dbo.Medicamento m ON m.IdMedicamento=h.IdMedicamento;';

    INSERT dbo.SigeMigracion (Id) VALUES (2);
    COMMIT;
    PRINT N'Migración 002 aplicada.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
