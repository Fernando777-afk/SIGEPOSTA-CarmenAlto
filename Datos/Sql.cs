using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SIGEPOSTA.Datos
{
    internal sealed class Sql
    {
        private readonly string cadena;

        public Sql(string cadena)
        {
            var configuracion = new SqlConnectionStringBuilder(cadena);
            if (configuracion.InitialCatalog != "SIGEPOSTA_CarmenAlto_V2" || !configuracion.IntegratedSecurity)
                throw new InvalidOperationException("Configura la base SIGEPOSTA_CarmenAlto_V2 con autenticación de Windows.");
            this.cadena = configuracion.ConnectionString;
        }

        public T Ejecutar<T>(Func<Consulta, T> trabajo)
        {
            using (var conexion = new SqlConnection(cadena))
            {
                Abrir(conexion);
                return trabajo(new Consulta(conexion, null));
            }
        }

        public T Transaccion<T>(Func<Consulta, T> trabajo)
        {
            using (var conexion = new SqlConnection(cadena))
            {
                Abrir(conexion);
                using (SqlTransaction transaccion = conexion.BeginTransaction())
                {
                    // Dispose revierte la transacción si una operación lanza una excepción.
                    T resultado = trabajo(new Consulta(conexion, transaccion));
                    transaccion.Commit();
                    return resultado;
                }
            }
        }

        private static void Abrir(SqlConnection conexion)
        {
            conexion.Open();
            using (var comando = new SqlCommand("SET ARITHABORT ON; SET QUOTED_IDENTIFIER ON;", conexion))
                comando.ExecuteNonQuery();
        }
    }

    internal sealed class Consulta
    {
        private readonly SqlConnection conexion;
        private readonly SqlTransaction transaccion;

        public Consulta(SqlConnection conexion, SqlTransaction transaccion)
        {
            this.conexion = conexion;
            this.transaccion = transaccion;
        }

        private SqlCommand Comando(string texto, SqlParameter[] parametros)
        {
            var comando = new SqlCommand(texto, conexion, transaccion);
            comando.Parameters.AddRange(parametros);
            return comando;
        }

        public int Ejecutar(string texto, params SqlParameter[] parametros)
        {
            using (SqlCommand comando = Comando(texto, parametros))
                return comando.ExecuteNonQuery();
        }

        public object Valor(string texto, params SqlParameter[] parametros)
        {
            using (SqlCommand comando = Comando(texto, parametros))
                return comando.ExecuteScalar();
        }

        public List<T> Leer<T>(string texto, Func<IDataRecord, T> mapear, params SqlParameter[] parametros)
        {
            var registros = new List<T>();
            using (SqlCommand comando = Comando(texto, parametros))
            using (SqlDataReader lector = comando.ExecuteReader())
                while (lector.Read()) registros.Add(mapear(lector));
            return registros;
        }

        public DataTable Tabla(string texto, params SqlParameter[] parametros)
        {
            var tabla = new DataTable();
            using (SqlCommand comando = Comando(texto, parametros))
            using (var adaptador = new SqlDataAdapter(comando)) adaptador.Fill(tabla);
            return tabla;
        }

        public void Bloquear(string recurso)
        {
            int resultado = Convert.ToInt32(Valor(@"DECLARE @resultado int;
EXEC @resultado = sys.sp_getapplock @Resource=@recurso, @LockMode='Exclusive',
     @LockOwner='Transaction', @LockTimeout=5000;
SELECT @resultado;", Almacen.P("@recurso", SqlDbType.NVarChar, recurso, 100)));
            if (resultado < 0) throw new InvalidOperationException("Hay otra operación en curso. Vuelve a intentarlo.");
        }
    }

    internal static class Campos
    {
        public static int Entero(this IDataRecord r, string nombre) { return Convert.ToInt32(r[nombre]); }
        public static string Texto(this IDataRecord r, string nombre) { return r[nombre] as string; }
        public static byte[] Bytes(this IDataRecord r, string nombre) { return r[nombre] as byte[]; }
        public static DateTime Fecha(this IDataRecord r, string nombre) { return (DateTime)r[nombre]; }
        public static decimal Decimal(this IDataRecord r, string nombre) { return (decimal)r[nombre]; }
    }
}
