using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Datos
{
    public sealed class RegistroProtegido<T>
    {
        public RegistroProtegido(T registro, byte[] cifrado) { Registro = registro; Cifrado = cifrado; }
        public T Registro { get; }
        public byte[] Cifrado { get; }
    }

    public sealed class Credencial
    {
        public Usuario Usuario { get; set; }
        public byte[] Sal { get; set; }
        public byte[] Hash { get; set; }
    }

    public sealed partial class Almacen
    {
        private readonly Sql sql;
        public Almacen(string cadena) { sql = new Sql(cadena); }

        internal static SqlParameter P(string nombre, SqlDbType tipo, object valor, int longitud = 0)
        {
            var parametro = new SqlParameter(nombre, tipo) { Value = valor ?? DBNull.Value };
            if (longitud != 0) parametro.Size = longitud;
            return parametro;
        }

        private static SqlParameter Id(string nombre, int valor) { return P(nombre, SqlDbType.Int, valor); }
        private static SqlParameter Fecha(string nombre, DateTime valor) { return P(nombre, SqlDbType.DateTime2, valor); }

        public byte[] HuellaClave()
        {
            return sql.Ejecutar(c =>
            {
                if (Convert.ToInt32(c.Valor("SELECT Version FROM dbo.SigeVersion")) != 1)
                    throw new InvalidOperationException("Ejecuta el script SQL de SIGEPOSTA antes de iniciar.");
                if (c.Valor("SELECT OBJECT_ID(N'dbo.SigeMigracion', N'U')") == DBNull.Value
                    || Convert.ToInt32(c.Valor("SELECT COUNT(*) FROM dbo.SigeMigracion WHERE Id=2")) != 1)
                    throw new InvalidOperationException("Ejecuta la migración 002 de SIGEPOSTA para continuar.");
                return c.Valor("SELECT HuellaClave FROM dbo.ConfiguracionSegura WHERE Id=1") as byte[];
            });
        }

        public void Inicializar(byte[] huella, string usuario, byte[] sal, byte[] hash)
        {
            sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Configuracion");
                if (c.Valor("SELECT Id FROM dbo.ConfiguracionSegura WHERE Id=1") != null)
                    throw new InvalidOperationException("SIGEPOSTA ya fue configurado. Abre de nuevo el inicio de sesión.");
                c.Ejecutar("INSERT dbo.ConfiguracionSegura (Id, HuellaClave) VALUES (1, @huella)",
                    P("@huella", SqlDbType.Binary, huella, 32));
                InsertarUsuario(c, usuario, Rol.Administrador, sal, hash, null);
                return true;
            });
        }

        public Credencial Acceso(string nombre)
        {
            return LeerAcceso("Nombre=@nombre", P("@nombre", SqlDbType.NVarChar, nombre, 40));
        }

        public Credencial Acceso(int id)
        {
            return LeerAcceso("IdUsuario=@id", Id("@id", id));
        }

        private Credencial LeerAcceso(string filtro, SqlParameter parametro)
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT IdUsuario, Nombre, Rol, Activo, Sal, HashClave, IdPersonal,
    CAST(NULL AS nvarchar(151)) AS ProfesionalAsignado FROM dbo.Usuario WHERE " + filtro,
                r => new Credencial { Usuario = LeerUsuario(r), Sal = r.Bytes("Sal"), Hash = r.Bytes("HashClave") },
                parametro).SingleOrDefault());
        }

        public Usuario UsuarioPorId(int id)
        {
            return sql.Ejecutar(c => BuscarUsuario(c, id));
        }

        private static Usuario BuscarUsuario(Consulta c, int id)
        {
            return c.Leer(@"SELECT IdUsuario, Nombre, Rol, Activo, IdPersonal,
    CAST(NULL AS nvarchar(151)) AS ProfesionalAsignado FROM dbo.Usuario WHERE IdUsuario=@id",
                LeerUsuario, Id("@id", id)).SingleOrDefault();
        }

        public List<Usuario> Usuarios()
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT u.IdUsuario, u.Nombre, u.Rol, u.Activo, u.IdPersonal,
    p.Nombres + N' ' + p.Apellidos AS ProfesionalAsignado FROM dbo.Usuario u
LEFT JOIN dbo.Persona p ON p.IdPersona=u.IdPersonal ORDER BY u.Nombre", LeerUsuario));
        }

        private static Usuario LeerUsuario(IDataRecord r)
        {
            return new Usuario
            {
                Id = r.Entero("IdUsuario"), Nombre = r.Texto("Nombre"),
                Rol = (Rol)Enum.Parse(typeof(Rol), r.Texto("Rol")), Activo = (bool)r["Activo"],
                IdPersonal = r["IdPersonal"] as int?, ProfesionalAsignado = r.Texto("ProfesionalAsignado")
            };
        }

        private static void InsertarUsuario(Consulta c, string nombre, Rol rol, byte[] sal, byte[] hash, int? personal)
        {
            c.Ejecutar("INSERT dbo.Usuario (Nombre, Rol, Sal, HashClave, IdPersonal) VALUES (@nombre, @rol, @sal, @hash, @personal)",
                P("@nombre", SqlDbType.NVarChar, nombre, 40), P("@rol", SqlDbType.VarChar, rol.ToString(), 24),
                P("@sal", SqlDbType.Binary, sal, 16), P("@hash", SqlDbType.Binary, hash, 32), P("@personal", SqlDbType.Int, personal));
        }

        private T AdministrarUsuarios<T>(int administrador, Func<Consulta, T> operacion)
        {
            return sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Usuarios");
                Usuario cuenta = BuscarUsuario(c, administrador);
                if (cuenta == null || !cuenta.Activo || cuenta.Rol != Rol.Administrador)
                    throw new InvalidOperationException("Solo un administrador activo puede gestionar las cuentas.");
                return operacion(c);
            });
        }

        public void CrearUsuario(int administrador, string nombre, Rol rol, byte[] sal, byte[] hash, int? personal)
        {
            AdministrarUsuarios(administrador, c =>
            {
                ComprobarNombreDisponible(c, nombre, 0);
                InsertarUsuario(c, nombre, rol, sal, hash, personal);
                return true;
            });
        }

        private static void ComprobarNombreDisponible(Consulta c, string nombre, int excepto)
        {
            if (c.Valor("SELECT IdUsuario FROM dbo.Usuario WHERE Nombre=@nombre AND IdUsuario<>@excepto",
                P("@nombre", SqlDbType.NVarChar, nombre, 40), Id("@excepto", excepto)) != null)
                throw new InvalidOperationException("Ya existe una cuenta con ese nombre de usuario.");
        }

        private static Usuario ComprobarDatosCuenta(Consulta c, Usuario seleccion)
        {
            Usuario actual = BuscarUsuario(c, seleccion.Id);
            if (actual == null || actual.Nombre != seleccion.Nombre || actual.Rol != seleccion.Rol
                || actual.Activo != seleccion.Activo || actual.IdPersonal != seleccion.IdPersonal)
                throw new InvalidOperationException("La cuenta cambió. Actualiza la lista y vuelve a seleccionarla.");
            return actual;
        }

        private static void ProtegerAdministrador(Consulta c, Usuario cuenta, Rol rol, bool activo)
        {
            if (!cuenta.Activo || cuenta.Rol != Rol.Administrador || (rol == Rol.Administrador && activo)) return;
            int cantidad = Convert.ToInt32(c.Valor(@"SELECT COUNT(*) FROM dbo.Usuario WITH (UPDLOCK,HOLDLOCK)
WHERE Rol='Administrador' AND Activo=1"));
            if (cantidad <= 1)
                throw new InvalidOperationException("Debe permanecer al menos una cuenta administradora activa.");
        }

        public void EditarUsuario(int administrador, Usuario seleccion, string nombre, Rol rol, bool activo, int? personal)
        {
            AdministrarUsuarios(administrador, c =>
            {
                Usuario cuenta = ComprobarDatosCuenta(c, seleccion);
                ProtegerAdministrador(c, cuenta, rol, activo);
                ComprobarNombreDisponible(c, nombre, cuenta.Id);
                c.Ejecutar(@"UPDATE dbo.Usuario SET Nombre=@nombre, Rol=@rol, Activo=@activo, IdPersonal=@personal WHERE IdUsuario=@id",
                    P("@nombre", SqlDbType.NVarChar, nombre, 40), P("@rol", SqlDbType.VarChar, rol.ToString(), 24),
                    P("@activo", SqlDbType.Bit, activo), P("@personal", SqlDbType.Int, personal), Id("@id", cuenta.Id));
                return true;
            });
        }

        public void CambiarEstadoUsuario(int administrador, Usuario seleccion, bool activo)
        {
            AdministrarUsuarios(administrador, c =>
            {
                Usuario cuenta = ComprobarDatosCuenta(c, seleccion);
                ProtegerAdministrador(c, cuenta, cuenta.Rol, activo);
                c.Ejecutar("UPDATE dbo.Usuario SET Activo=@activo WHERE IdUsuario=@id",
                    P("@activo", SqlDbType.Bit, activo), Id("@id", cuenta.Id));
                return true;
            });
        }

        public bool EliminarUsuario(int administrador, Usuario seleccion)
        {
            return AdministrarUsuarios(administrador, c =>
            {
                if (administrador == seleccion.Id)
                    throw new InvalidOperationException("Para eliminar tu cuenta debes cerrar sesión y pedirlo a otro administrador.");
                Usuario cuenta = ComprobarDatosCuenta(c, seleccion);
                ProtegerAdministrador(c, cuenta, cuenta.Rol, false);
                if (cuenta.IdPersonal.HasValue || TieneRegistrosVinculados(c, cuenta.Id)) return false;
                c.Ejecutar("DELETE FROM dbo.Usuario WHERE IdUsuario=@id", Id("@id", cuenta.Id));
                return true;
            });
        }

        private static bool TieneRegistrosVinculados(Consulta c, int usuario)
        {
            // Los identificadores provienen del catálogo de SQL Server y se escapan allí.
            var relaciones = c.Leer(@"SELECT k.object_id AS Relacion, QUOTENAME(s.name)+N'.'+QUOTENAME(t.name) AS Tabla,
    N'r.'+QUOTENAME(origen.name)+N'=u.'+QUOTENAME(destino.name) AS Condicion
FROM sys.foreign_keys k JOIN sys.foreign_key_columns f ON f.constraint_object_id=k.object_id
JOIN sys.tables t ON t.object_id=k.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns origen ON origen.object_id=f.parent_object_id AND origen.column_id=f.parent_column_id
JOIN sys.columns destino ON destino.object_id=f.referenced_object_id AND destino.column_id=f.referenced_column_id
WHERE k.referenced_object_id=OBJECT_ID(N'dbo.Usuario')", r => new
            {
                Relacion = r.Entero("Relacion"), Tabla = r.Texto("Tabla"), Condicion = r.Texto("Condicion")
            });
            foreach (var relacion in relaciones.GroupBy(r => r.Relacion))
            {
                string tabla = relacion.First().Tabla;
                string condicion = string.Join(" AND ", relacion.Select(r => r.Condicion));
                if (c.Valor("SELECT TOP (1) 1 FROM " + tabla + " r WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Usuario u ON "
                    + condicion + " WHERE u.IdUsuario=@id", Id("@id", usuario)) != null) return true;
            }
            return false;
        }

        public void AsignarProfesional(int administrador, Usuario seleccion, int personal)
        {
            AdministrarUsuarios(administrador, c =>
            {
                Usuario cuenta = ComprobarDatosCuenta(c, seleccion);
                if (cuenta.Rol != Rol.ProfesionalSalud)
                    throw new InvalidOperationException("Selecciona una cuenta Profesional de Salud.");
                c.Ejecutar("UPDATE dbo.Usuario SET IdPersonal=@personal WHERE IdUsuario=@usuario",
                    Id("@usuario", cuenta.Id), Id("@personal", personal));
                return true;
            });
        }

        public void CambiarClave(int usuario, byte[] sal, byte[] hash, byte[] hashAnterior)
        {
            sql.Ejecutar(c => { GuardarClave(c, usuario, sal, hash, hashAnterior); return true; });
        }

        public void RestablecerClave(int administrador, int usuario, byte[] sal, byte[] hash)
        {
            AdministrarUsuarios(administrador, c => { GuardarClave(c, usuario, sal, hash, null); return true; });
        }

        private static void GuardarClave(Consulta c, int usuario, byte[] sal, byte[] hash, byte[] hashAnterior)
        {
            int filas = c.Ejecutar(@"UPDATE dbo.Usuario SET Sal=@sal, HashClave=@hash
WHERE IdUsuario=@id AND Activo=1 AND (@anterior IS NULL OR HashClave=@anterior)",
                P("@sal", SqlDbType.Binary, sal, 16), P("@hash", SqlDbType.Binary, hash, 32),
                P("@anterior", SqlDbType.Binary, hashAnterior, 32), Id("@id", usuario));
            if (filas != 1) throw new InvalidOperationException("La cuenta o contraseña cambió. Vuelve a iniciar sesión.");
        }

        public Posta Establecimiento()
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT Nombre, Distrito, Direccion, Categoria, Responsable
FROM dbo.Posta WHERE IdPosta=1", r => new Posta(r.Texto("Nombre"), r.Texto("Distrito"))
            {
                Direccion = r.Texto("Direccion"), Categoria = r.Texto("Categoria"), Responsable = r.Texto("Responsable")
            }).Single());
        }
    }
}
