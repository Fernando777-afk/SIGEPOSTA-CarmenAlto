USE [master];
GO
IF DB_ID(N'SIGEPOSTA_CarmenAlto_V2') IS NULL
    CREATE DATABASE [SIGEPOSTA_CarmenAlto_V2];
GO
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
    THROW 51000, N'Abre la base SIGEPOSTA_CarmenAlto_V2 antes de continuar.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Bloqueo int;
    EXEC @Bloqueo = sys.sp_getapplock @Resource = N'SIGEPOSTA.Esquema', @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 10000;
    IF @Bloqueo < 0 THROW 51001, N'Otra instalación está en curso.', 1;

    IF OBJECT_ID(N'dbo.SigeVersion', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SigeVersion WHERE Version = 1)
        BEGIN
            COMMIT;
            PRINT N'SIGEPOSTA ya está preparado. Los datos se conservaron.';
            RETURN;
        END;
        THROW 51002, N'La versión de la base es diferente.', 1;
    END;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
        THROW 51003, N'La base contiene tablas ajenas a esta instalación.', 1;

    CREATE TABLE dbo.SigeVersion (
        Version int NOT NULL PRIMARY KEY CHECK (Version = 1)
    );
    CREATE TABLE dbo.ConfiguracionSegura (
        Id tinyint NOT NULL PRIMARY KEY CHECK (Id = 1),
        HuellaClave binary(32) NOT NULL
    );
    CREATE TABLE dbo.Posta (
        IdPosta tinyint NOT NULL PRIMARY KEY CHECK (IdPosta = 1),
        Nombre nvarchar(100) NOT NULL,
        Distrito nvarchar(60) NOT NULL,
        Direccion nvarchar(160) NULL,
        Categoria nvarchar(8) NULL,
        Responsable nvarchar(120) NULL
    );
    CREATE TABLE dbo.Usuario (
        IdUsuario int IDENTITY PRIMARY KEY,
        Nombre nvarchar(40) NOT NULL UNIQUE,
        Rol varchar(14) NOT NULL CHECK (Rol IN ('Administrador', 'Admision', 'Clinico', 'Farmacia')),
        Sal binary(16) NOT NULL,
        HashClave binary(32) NOT NULL,
        Activo bit NOT NULL DEFAULT 1
    );
    CREATE TABLE dbo.Persona (
        IdPersona int IDENTITY PRIMARY KEY,
        DniCifrado varbinary(128) NOT NULL,
        DniIndice binary(32) NOT NULL UNIQUE,
        Nombres nvarchar(70) NOT NULL,
        Apellidos nvarchar(80) NOT NULL,
        FechaNacimiento date NOT NULL,
        Telefono nvarchar(20) NULL
    );
    CREATE TABLE dbo.Paciente (
        IdPaciente int NOT NULL PRIMARY KEY REFERENCES dbo.Persona(IdPersona),
        IdPosta tinyint NOT NULL DEFAULT 1 REFERENCES dbo.Posta(IdPosta),
        Direccion nvarchar(160) NULL
    );
    CREATE TABLE dbo.HistoriaClinica (
        IdHistoria int IDENTITY PRIMARY KEY,
        IdPaciente int NOT NULL UNIQUE REFERENCES dbo.Paciente(IdPaciente),
        FechaRegistro datetime2(0) NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE TABLE dbo.PersonalSalud (
        IdPersonal int NOT NULL PRIMARY KEY REFERENCES dbo.Persona(IdPersona),
        IdPosta tinyint NOT NULL DEFAULT 1 REFERENCES dbo.Posta(IdPosta),
        Tipo varchar(16) NOT NULL CHECK (Tipo IN ('Medico','Enfermero','Tecnico','Administrativo')),
        Area nvarchar(80) NOT NULL,
        Colegiatura nvarchar(30) NULL
    );
    CREATE TABLE dbo.Horario (
        IdHorario int IDENTITY PRIMARY KEY,
        IdPersonal int NOT NULL REFERENCES dbo.PersonalSalud(IdPersonal),
        Dia tinyint NOT NULL CHECK (Dia BETWEEN 0 AND 6),
        Inicio time(0) NOT NULL,
        Fin time(0) NOT NULL,
        CHECK (Inicio < Fin),
        UNIQUE (IdPersonal, Dia, Inicio)
    );
    CREATE TABLE dbo.Consultorio (
        IdConsultorio int IDENTITY PRIMARY KEY,
        Nombre nvarchar(60) NOT NULL UNIQUE
    );
    CREATE TABLE dbo.Cita (
        IdCita int IDENTITY PRIMARY KEY,
        IdPaciente int NOT NULL REFERENCES dbo.Paciente(IdPaciente),
        IdPersonal int NOT NULL REFERENCES dbo.PersonalSalud(IdPersonal),
        IdConsultorio int NOT NULL REFERENCES dbo.Consultorio(IdConsultorio),
        FechaHora datetime2(0) NOT NULL,
        Estado varchar(12) NOT NULL DEFAULT 'Programada' CHECK (Estado IN ('Programada','EnEspera','Atendida','Cancelada')),
        Llegada datetime2(0) NULL,
        CHECK (DATEPART(MINUTE, FechaHora) IN (0,30) AND DATEPART(SECOND, FechaHora) = 0),
        CHECK ((Estado = 'Programada' AND Llegada IS NULL) OR (Estado IN ('EnEspera','Atendida') AND Llegada IS NOT NULL) OR Estado = 'Cancelada')
    );
    CREATE UNIQUE INDEX IX_Cita_PersonalTurno ON dbo.Cita(IdPersonal, FechaHora) WHERE Estado <> 'Cancelada';
    CREATE UNIQUE INDEX IX_Cita_ConsultorioTurno ON dbo.Cita(IdConsultorio, FechaHora) WHERE Estado <> 'Cancelada';
    CREATE UNIQUE INDEX IX_Cita_PacienteTurno ON dbo.Cita(IdPaciente, FechaHora) WHERE Estado <> 'Cancelada';
    CREATE INDEX IX_Cita_Agenda ON dbo.Cita(FechaHora, Estado);
    CREATE TABLE dbo.Atencion (
        IdAtencion int IDENTITY PRIMARY KEY,
        IdCita int NOT NULL UNIQUE REFERENCES dbo.Cita(IdCita),
        DiagnosticoCifrado varbinary(max) NOT NULL,
        Temperatura decimal(4,1) NOT NULL CHECK (Temperatura > 0),
        Peso decimal(5,1) NOT NULL CHECK (Peso > 0),
        Presion nvarchar(15) NOT NULL,
        FechaRegistro datetime2(0) NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE TABLE dbo.Medicamento (
        IdMedicamento int IDENTITY PRIMARY KEY,
        Nombre nvarchar(100) NOT NULL,
        Lote nvarchar(40) NOT NULL,
        StockActual int NOT NULL DEFAULT 0 CHECK (StockActual >= 0),
        StockMinimo int NOT NULL CHECK (StockMinimo > 0),
        FechaVencimiento date NOT NULL,
        UNIQUE (Nombre, Lote)
    );
    CREATE TABLE dbo.Receta (
        IdAtencion int NOT NULL REFERENCES dbo.Atencion(IdAtencion),
        IdMedicamento int NOT NULL REFERENCES dbo.Medicamento(IdMedicamento),
        Cantidad int NOT NULL CHECK (Cantidad > 0),
        Indicaciones nvarchar(250) NOT NULL,
        PRIMARY KEY (IdAtencion, IdMedicamento)
    );
    CREATE TABLE dbo.MovimientoInventario (
        IdMovimiento int IDENTITY PRIMARY KEY,
        IdMedicamento int NOT NULL REFERENCES dbo.Medicamento(IdMedicamento),
        IdAtencion int NULL REFERENCES dbo.Atencion(IdAtencion),
        Tipo varchar(7) NOT NULL CHECK (Tipo IN ('Ingreso','Salida')),
        Cantidad int NOT NULL CHECK (Cantidad > 0),
        Motivo nvarchar(160) NOT NULL,
        Fecha datetime2(0) NOT NULL DEFAULT SYSDATETIME(),
        CHECK (IdAtencion IS NULL OR Tipo = 'Salida')
    );
    CREATE UNIQUE INDEX IX_Movimiento_Receta ON dbo.MovimientoInventario(IdAtencion, IdMedicamento)
        WHERE IdAtencion IS NOT NULL;

    INSERT dbo.Posta (IdPosta, Nombre, Distrito)
        VALUES (1, N'Centro de Salud Carmen Alto', N'Carmen Alto');
    INSERT dbo.Consultorio (Nombre)
        VALUES (N'Medicina general'), (N'Consultorio 2'), (N'Consultorio 3');
    INSERT dbo.Medicamento (Nombre, Lote, StockActual, StockMinimo, FechaVencimiento)
        VALUES (N'Medicamento de prueba A', N'FICT-A', 40, 10, DATEADD(DAY, 180, CAST(SYSDATETIME() AS date))),
               (N'Medicamento de prueba B', N'FICT-B', 4, 10, DATEADD(DAY, 20, CAST(SYSDATETIME() AS date))),
               (N'Medicamento de prueba C', N'FICT-C', 7, 5, DATEADD(DAY, -1, CAST(SYSDATETIME() AS date)));
    INSERT dbo.MovimientoInventario (IdMedicamento, Tipo, Cantidad, Motivo)
        SELECT IdMedicamento, 'Ingreso', StockActual, N'Inventario inicial ficticio' FROM dbo.Medicamento;
    INSERT dbo.SigeVersion (Version) VALUES (1);

    COMMIT;
    PRINT N'Base SIGEPOSTA_CarmenAlto_V2 preparada.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
