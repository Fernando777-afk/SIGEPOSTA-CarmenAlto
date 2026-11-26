using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    public enum Seccion { Inicio, Pacientes, Personal, Citas, Cola, Historias, Farmacia, Reportes, Seguridad }

    public sealed partial class CentroSalud
    {
        private readonly Almacen datos;
        private Proteccion proteccion;
        private Sesion sesionActual;
        public Sesion Sesion
        {
            get { return Volatile.Read(ref sesionActual); }
            private set { Volatile.Write(ref sesionActual, value); }
        }

        public CentroSalud(string cadena) { datos = new Almacen(cadena); }

        public bool RequiereConfiguracion()
        {
            byte[] huella = datos.HuellaClave();
            if (huella == null) return true;
            Proteccion local = Proteccion.Abrir(false);
            if (!Proteccion.Iguales(huella, local.Huella))
                throw new InvalidOperationException("La clave de este perfil de Windows no corresponde a la base de Carmen Alto.");
            proteccion = local;
            return false;
        }

        public void Configurar(string usuario, string clave)
        {
            usuario = NombreUsuario(usuario);
            ValidarClave(clave);
            if (datos.HuellaClave() != null)
                throw new InvalidOperationException("SIGEPOSTA ya fue configurado. Vuelve al inicio de sesión.");
            Proteccion local = Proteccion.Abrir(true);
            byte[] sal = Proteccion.Aleatorio(16);
            datos.Inicializar(local.Huella, usuario, sal, Proteccion.HashClave(clave, sal));
            proteccion = local;
        }

        public Sesion Iniciar(string usuario, string clave)
        {
            if (proteccion == null)
                throw new InvalidOperationException("Verifica la configuración de SIGEPOSTA antes de ingresar.");
            usuario = (usuario ?? "").Trim();
            if (usuario.Length < 1 || usuario.Length > 40 || clave == null || clave.Length > 128)
                throw new InvalidOperationException("Usuario o contraseña incorrectos.");
            Credencial registro = datos.Acceso(usuario);
            byte[] hash = Proteccion.HashClave(clave, registro?.Sal ?? new byte[16]);
            if (registro == null || !Proteccion.Iguales(registro.Hash, hash) || !registro.Usuario.Activo)
                throw new InvalidOperationException("Usuario o contraseña incorrectos.");
            Usuario cuenta = registro.Usuario;
            return Sesion = new Sesion(cuenta.Id, cuenta.Nombre, cuenta.Rol, cuenta.IdPersonal);
        }

        public void CerrarSesion() { Sesion = null; }

        public Sesion ConsultarSesion()
        {
            Sesion actual = Sesion;
            if (actual == null) return null;
            Usuario cuenta = datos.UsuarioPorId(actual.Id);
            if (!ReferenceEquals(actual, Sesion)) return null;
            if (cuenta == null || !cuenta.Activo || cuenta.Rol != actual.Rol || cuenta.IdPersonal != actual.IdPersonal)
            {
                Interlocked.CompareExchange(ref sesionActual, null, actual);
                return null;
            }
            return new Sesion(cuenta.Id, cuenta.Nombre, cuenta.Rol, cuenta.IdPersonal);
        }

        public bool Puede(Seccion seccion) { return Puede(Sesion, seccion); }

        private static bool Puede(Sesion sesion, Seccion seccion)
        {
            if (sesion == null) return false;
            if (seccion == Seccion.Inicio || sesion.Rol == Rol.Administrador) return true;
            switch (sesion.Rol)
            {
                case Rol.Responsable:
                    return seccion == Seccion.Personal || seccion == Seccion.Citas || seccion == Seccion.Cola || seccion == Seccion.Reportes;
                case Rol.Admision:
                    return seccion == Seccion.Pacientes || seccion == Seccion.Citas || seccion == Seccion.Cola;
                case Rol.ProfesionalSalud:
                    return seccion == Seccion.Cola || seccion == Seccion.Historias;
                case Rol.Farmacia:
                    return seccion == Seccion.Farmacia;
                case Rol.Estadistica:
                    return seccion == Seccion.Reportes;
                default:
                    return false;
            }
        }

        public bool PuedeEditar(Seccion seccion) { return PuedeEditar(Sesion, seccion); }

        private static bool PuedeEditar(Sesion sesion, Seccion seccion)
        {
            if (sesion == null) return false;
            if (sesion.Rol == Rol.Administrador) return true;
            if (sesion.Rol == Rol.Admision) return seccion == Seccion.Pacientes || seccion == Seccion.Citas;
            if (sesion.Rol == Rol.ProfesionalSalud) return seccion == Seccion.Historias && sesion.IdPersonal.HasValue;
            return sesion.Rol == Rol.Farmacia && seccion == Seccion.Farmacia;
        }

        private Sesion ExigirEdicion(Seccion seccion)
        {
            Sesion sesion = ExigirSesion();
            if (!PuedeEditar(sesion, seccion)) throw new InvalidOperationException("Tu cuenta no tiene permiso para modificar estos datos.");
            return sesion;
        }

        // El cero excluye todas las filas cuando una cuenta clínica aún no tiene asignación.
        private static int? MedicoDe(Sesion sesion) { return sesion.Rol == Rol.ProfesionalSalud ? sesion.IdPersonal ?? 0 : (int?)null; }

        private Sesion ExigirSesion()
        {
            return ConsultarSesion() ?? throw new InvalidOperationException("La sesión ha cambiado o ya no está activa. Vuelve a iniciar sesión.");
        }

        private Sesion Exigir(Seccion seccion)
        {
            Sesion sesion = ExigirSesion();
            if (!Puede(sesion, seccion)) throw new InvalidOperationException("Tu cuenta no tiene permiso para esta operación.");
            return sesion;
        }

        public List<Usuario> Usuarios() { Exigir(Seccion.Seguridad); return datos.Usuarios(); }

        public void CrearUsuario(string nombre, Rol rol, string clave, int? personal = null)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            ValidarClave(clave);
            ValidarRol(rol, personal);
            byte[] sal = Proteccion.Aleatorio(16);
            datos.CrearUsuario(sesion.Id, NombreUsuario(nombre), rol, sal, Proteccion.HashClave(clave, sal), personal);
        }

        public void EditarUsuario(Usuario seleccion, string nombre, Rol rol, bool activo, int? personal)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            ValidarRol(rol, personal);
            datos.EditarUsuario(sesion.Id, seleccion, NombreUsuario(nombre), rol, activo, personal);
        }

        public void CambiarEstadoUsuario(Usuario seleccion, bool activo)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            datos.CambiarEstadoUsuario(sesion.Id, seleccion, activo);
        }

        public bool EliminarUsuario(Usuario seleccion)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            if (sesion.Id == seleccion.Id) throw new InvalidOperationException("No puedes eliminar tu propia cuenta durante la sesión.");
            try { return datos.EliminarUsuario(sesion.Id, seleccion); }
            catch (SqlException error) when (error.Number == 547) { return false; }
        }

        private void ValidarRol(Rol rol, int? personal)
        {
            if (!Enum.IsDefined(typeof(Rol), rol)) throw new InvalidOperationException("Selecciona un rol válido.");
            if (rol == Rol.ProfesionalSalud) ValidarProfesional(personal);
            else if (personal.HasValue) throw new InvalidOperationException("La asignación corresponde a Profesional de Salud.");
        }

        public List<Opcion> ProfesionalesAsignables()
        {
            Exigir(Seccion.Seguridad);
            return datos.Personal().OfType<Medico>().Select(p => new Opcion(p.Id, p.MostrarDatos())).ToList();
        }

        private void ValidarProfesional(int? personal)
        {
            if (!personal.HasValue || !(datos.Profesional(personal.Value)?.Registro is Medico))
                throw new InvalidOperationException("Selecciona el médico registrado para vincular la cuenta.");
        }

        public void AsignarProfesional(Usuario usuario, int personal)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            ValidarProfesional(personal);
            datos.AsignarProfesional(sesion.Id, usuario, personal);
        }

        public void CambiarClave(string actual, string nueva)
        {
            Sesion sesion = Exigir(Seccion.Inicio);
            ValidarClave(nueva);
            Credencial registro = datos.Acceso(sesion.Id);
            if (actual == null || actual.Length > 128 || registro == null
                || !Proteccion.Iguales(registro.Hash, Proteccion.HashClave(actual, registro.Sal)))
                throw new InvalidOperationException("La contraseña actual no coincide.");
            byte[] sal = Proteccion.Aleatorio(16);
            datos.CambiarClave(sesion.Id, sal, Proteccion.HashClave(nueva, sal), registro.Hash);
        }

        public void RestablecerClave(int usuario, string nueva)
        {
            Sesion sesion = Exigir(Seccion.Seguridad);
            ValidarClave(nueva);
            byte[] sal = Proteccion.Aleatorio(16);
            datos.RestablecerClave(sesion.Id, usuario, sal, Proteccion.HashClave(nueva, sal));
        }

        public Posta Establecimiento() { Exigir(Seccion.Inicio); return datos.Establecimiento(); }
        public Resumen Resumen(DateTime dia) { return datos.Resumen(dia, MedicoDe(Exigir(Seccion.Inicio))); }

        private static string NombreUsuario(string texto)
        {
            texto = Texto(texto, 3, 40, "El usuario");
            foreach (char letra in texto)
                if (!char.IsLetterOrDigit(letra) && letra != '.' && letra != '_' && letra != '-')
                    throw new InvalidOperationException("Usa letras, números, puntos, guiones o guiones bajos para el usuario.");
            return texto;
        }

        private static void ValidarClave(string clave)
        {
            if (string.IsNullOrWhiteSpace(clave) || clave.Length < 12 || clave.Length > 128)
                throw new InvalidOperationException("La contraseña debe tener entre 12 y 128 caracteres.");
        }

        private static string Texto(string valor, int minimo, int maximo, string etiqueta)
        {
            string texto = (valor ?? "").Trim();
            if (texto.Length < minimo || texto.Length > maximo)
                throw new InvalidOperationException(etiqueta + " debe tener entre " + minimo + " y " + maximo + " caracteres.");
            return texto.Length == 0 ? null : texto;
        }

        public static string Mensaje(Exception error)
        {
            var sql = error as SqlException;
            if (sql != null)
            {
                switch (sql.Number)
                {
                    case 2601:
                    case 2627: return "Ya existe un registro con esos datos. Revisa el DNI, usuario, turno o lote.";
                    case 208:
                    case 4060:
                    case 18456: return "Comprueba que la base de Carmen Alto esté preparada y que tengas acceso con tu cuenta de Windows.";
                    case 547: return "La operación incumple una relación o un límite de datos. Revisa los campos y vuelve a intentarlo.";
                    case 1205: return "Otra operación usó estos datos al mismo tiempo. Actualiza la pantalla y vuelve a intentarlo.";
                    default: return "No se pudo completar la operación en SQL Server. Verifica la conexión y vuelve a intentarlo.";
                }
            }
            if (error is CryptographicException || error is IsolatedStorageException)
                return "No se pudo verificar el contenido protegido. Comprueba el archivo y el perfil de Windows de SIGEPOSTA.";
            if (error is UnauthorizedAccessException || error is IOException)
                return "No se pudo leer o guardar el archivo en la ubicación elegida.";
            if (error is InvalidOperationException && error.GetType() == typeof(InvalidOperationException) && error.InnerException == null)
                return error.Message;
            return "No se pudo completar la operación. Revisa los datos y vuelve a intentarlo.";
        }
    }
}
