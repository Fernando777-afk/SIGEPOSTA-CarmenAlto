using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Serialization;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    internal static class Archivos
    {
        private const int Limite = 100 * 1024 * 1024;

        public static void GuardarRespaldo(string ruta, RespaldoClinico respaldo, Proteccion proteccion)
        {
            byte[] contenido = Comprimir(respaldo);
            if (contenido.Length > Limite - 65) throw new InvalidOperationException("El respaldo excede el límite de tamaño.");
            byte[] protegido = proteccion.Cifrar(contenido, "respaldo");
            Guardar(ruta, salida => salida.Write(protegido, 0, protegido.Length));
        }

        public static RespaldoClinico VerificarRespaldo(string ruta, Proteccion proteccion)
        {
            byte[] comprimido;
            using (var entrada = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var memoria = new MemoryStream())
            {
                CopiarConLimite(entrada, memoria);
                comprimido = proteccion.Descifrar(memoria.ToArray(), "respaldo");
            }
            using (var entrada = new MemoryStream(comprimido))
            using (var flujo = new GZipStream(entrada, CompressionMode.Decompress))
            using (var xml = new MemoryStream())
            {
                CopiarConLimite(flujo, xml);
                xml.Position = 0;
                using (var lector = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                {
                    var respaldo = (RespaldoClinico)new XmlSerializer(typeof(RespaldoClinico)).Deserialize(lector);
                    if (respaldo.Version != 1 || respaldo.Historias == null)
                        throw new InvalidOperationException("El formato del respaldo no corresponde a SIGEPOSTA.");
                    return respaldo;
                }
            }
        }

        public static void GuardarReporte(string ruta, ReporteDiario reporte)
        {
            byte[] contenido = Comprimir(reporte);
            Guardar(ruta, salida => salida.Write(contenido, 0, contenido.Length));
        }

        private static byte[] Comprimir<T>(T documento)
        {
            using (var plano = new MemoryStream())
            using (var salida = new MemoryStream())
            {
                new XmlSerializer(typeof(T)).Serialize(plano, documento);
                if (plano.Length > Limite) throw new InvalidOperationException("El documento excede el límite de tamaño.");
                plano.Position = 0;
                using (var gzip = new GZipStream(salida, CompressionMode.Compress, true)) plano.CopyTo(gzip);
                return salida.ToArray();
            }
        }

        private static void CopiarConLimite(Stream origen, Stream destino)
        {
            var buffer = new byte[8192];
            int leidos;
            while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (destino.Length + leidos > Limite)
                    throw new InvalidOperationException("El archivo excede el límite de tamaño permitido.");
                destino.Write(buffer, 0, leidos);
            }
        }

        private static void Guardar(string ruta, Action<Stream> escribir)
        {
            string completa = Path.GetFullPath(ruta);
            string temporal = Path.Combine(Path.GetDirectoryName(completa), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var archivo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None)) escribir(archivo);
                if (File.Exists(completa)) File.Replace(temporal, completa, null);
                else File.Move(temporal, completa);
            }
            finally
            {
                if (File.Exists(temporal)) File.Delete(temporal);
            }
        }
    }
}
