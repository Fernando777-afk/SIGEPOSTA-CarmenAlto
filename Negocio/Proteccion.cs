using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;

namespace SIGEPOSTA.Negocio
{
    internal sealed class Proteccion
    {
        private const string ArchivoClave = "carmentalto-v2.dpapi";
        private const int Iteraciones = 600000;
        private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("SIGEPOSTA.CarmenAlto.V2");
        private readonly byte[] cifrado;
        private readonly byte[] autenticacion;
        private readonly byte[] indice;

        private Proteccion(byte[] raiz)
        {
            cifrado = Derivar(raiz, "AES-CBC");
            autenticacion = Derivar(raiz, "HMAC-SHA256");
            indice = Derivar(raiz, "IndiceDni");
            Huella = Derivar(raiz, "Identidad de la base");
            Array.Clear(raiz, 0, raiz.Length);
        }

        public byte[] Huella { get; }

        public static Proteccion Abrir(bool crear)
        {
            // Una identidad fija permite usar la misma clave desde Debug, Release o una copia del ejecutable.
            var identidad = new Url("file:///SIGEPOSTA/CarmenAlto/V2");
            using (IsolatedStorageFile local = IsolatedStorageFile.GetStore(
                IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, identidad))
            {
                if (crear && !local.FileExists(ArchivoClave))
                    GuardarClave(local);
                if (!local.FileExists(ArchivoClave))
                    throw new InvalidOperationException("No se encontró la clave local. Abre SIGEPOSTA con el perfil de Windows que configuró esta base.");

                using (var archivo = new IsolatedStorageFileStream(ArchivoClave, FileMode.Open, FileAccess.Read, FileShare.Read, local))
                using (var memoria = new MemoryStream())
                {
                    archivo.CopyTo(memoria);
                    byte[] raiz = ProtectedData.Unprotect(memoria.ToArray(), Entropia, DataProtectionScope.CurrentUser);
                    if (raiz.Length != 32) throw new CryptographicException("Formato de clave local inválido.");
                    return new Proteccion(raiz);
                }
            }
        }

        private static void GuardarClave(IsolatedStorageFile local)
        {
            string temporal = Guid.NewGuid().ToString("N") + ".tmp";
            byte[] clave = Aleatorio(32);
            try
            {
                byte[] protegida = ProtectedData.Protect(clave, Entropia, DataProtectionScope.CurrentUser);
                using (var archivo = new IsolatedStorageFileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, local))
                    archivo.Write(protegida, 0, protegida.Length);
                try { local.MoveFile(temporal, ArchivoClave); }
                catch (IsolatedStorageException) when (local.FileExists(ArchivoClave)) { }
            }
            finally
            {
                Array.Clear(clave, 0, clave.Length);
                if (local.FileExists(temporal)) local.DeleteFile(temporal);
            }
        }

        public byte[] IndiceDni(string dni) { return Derivar(indice, dni); }

        public byte[] CifrarTexto(string texto, string uso) { return Cifrar(Encoding.UTF8.GetBytes(texto), uso); }
        public string LeerTexto(byte[] datos, string uso) { return Encoding.UTF8.GetString(Descifrar(datos, uso)); }

        public byte[] Cifrar(byte[] claro, string uso)
        {
            using (Aes aes = CrearAes())
            using (var salida = new MemoryStream())
            {
                salida.WriteByte(1);
                salida.Write(aes.IV, 0, aes.IV.Length);
                using (var flujo = new CryptoStream(salida, aes.CreateEncryptor(), CryptoStreamMode.Write, true))
                    flujo.Write(claro, 0, claro.Length);
                byte[] cuerpo = salida.ToArray();
                byte[] etiqueta = Etiqueta(cuerpo, cuerpo.Length, uso);
                return cuerpo.Concat(etiqueta).ToArray();
            }
        }

        public byte[] Descifrar(byte[] datos, string uso)
        {
            if (datos == null || datos.Length < 65 || datos[0] != 1 || (datos.Length - 49) % 16 != 0)
                throw new CryptographicException("Formato cifrado inválido.");
            int longitud = datos.Length - 32;
            if (!Iguales(Etiqueta(datos, longitud, uso), datos.Skip(longitud).ToArray()))
                throw new CryptographicException("La firma del contenido no coincide.");

            using (Aes aes = CrearAes())
            using (var entrada = new MemoryStream(datos, 17, longitud - 17))
            using (var salida = new MemoryStream())
            {
                aes.IV = datos.Skip(1).Take(16).ToArray();
                using (var flujo = new CryptoStream(entrada, aes.CreateDecryptor(), CryptoStreamMode.Read)) flujo.CopyTo(salida);
                return salida.ToArray();
            }
        }

        private Aes CrearAes()
        {
            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = cifrado;
            aes.GenerateIV();
            return aes;
        }

        private byte[] Etiqueta(byte[] datos, int longitud, string uso)
        {
            using (var hmac = new HMACSHA256(Derivar(autenticacion, uso)))
                return hmac.ComputeHash(datos, 0, longitud);
        }

        private static byte[] Derivar(byte[] clave, string etiqueta)
        {
            using (var hmac = new HMACSHA256(clave))
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(etiqueta));
        }

        public static byte[] Aleatorio(int largo)
        {
            byte[] resultado = new byte[largo];
            using (var generador = RandomNumberGenerator.Create()) generador.GetBytes(resultado);
            return resultado;
        }

        public static byte[] HashClave(string clave, byte[] sal)
        {
            using (var pbkdf = new Rfc2898DeriveBytes(clave, sal, Iteraciones, HashAlgorithmName.SHA256))
                return pbkdf.GetBytes(32);
        }

        public static bool Iguales(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++) diferencia |= a[i] ^ b[i];
            return diferencia == 0;
        }
    }
}
