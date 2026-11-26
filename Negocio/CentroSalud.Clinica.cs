using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    public sealed partial class CentroSalud
    {
        public HistoriaClinica Historia(int paciente)
        {
            int? medico = MedicoDe(Exigir(Seccion.Historias));
            return datos.Historias(paciente, DescifrarDiagnostico, medico).SingleOrDefault()
                ?? throw new InvalidOperationException("La historia clínica no está disponible para tu cuenta.");
        }

        public List<Medicamento> LotesParaReceta()
        {
            Exigir(Seccion.Historias);
            return datos.Medicamentos().Where(m => m.StockActual > 0 && m.FechaVencimiento.Date >= DateTime.Today).ToList();
        }

        public int RegistrarAtencion(Atencion atencion)
        {
            int? medico = MedicoDe(ExigirEdicion(Seccion.Historias));
            atencion.Diagnostico = Texto(atencion.Diagnostico, 1, 2500, "El diagnóstico");
            atencion.Presion = (atencion.Presion ?? "").Trim();
            if (!Regex.IsMatch(atencion.Presion, @"\A[0-9]{2,3}/[0-9]{2,3}\z"))
                throw new InvalidOperationException("Usa el formato número/número para la presión arterial.");
            if (atencion.Temperatura <= 0 || atencion.Temperatura > 999.9m || atencion.Peso <= 0 || atencion.Peso > 9999.9m)
                throw new InvalidOperationException("Ingresa valores positivos de temperatura y peso.");
            if (atencion.Temperatura != decimal.Round(atencion.Temperatura, 1) || atencion.Peso != decimal.Round(atencion.Peso, 1))
                throw new InvalidOperationException("Temperatura y peso admiten un decimal.");
            if (atencion.Recetas.Select(r => r.IdMedicamento).Distinct().Count() != atencion.Recetas.Count)
                throw new InvalidOperationException("Agrupa la cantidad de cada lote en una sola línea de receta.");
            foreach (Receta receta in atencion.Recetas)
            {
                if (receta.IdMedicamento <= 0 || receta.Cantidad <= 0)
                    throw new InvalidOperationException("Selecciona un lote y escribe una cantidad positiva.");
                receta.Indicaciones = Texto(receta.Indicaciones, 1, 250, "Las indicaciones");
            }
            byte[] cifrado = proteccion.CifrarTexto(atencion.Diagnostico, "diagnostico");
            return datos.RegistrarAtencion(atencion, cifrado, DateTime.Now, medico);
        }

        public void CrearRespaldo(string ruta)
        {
            Exigir(Seccion.Seguridad);
            var respaldo = new RespaldoClinico
            {
                Version = 1, Establecimiento = datos.Establecimiento().Nombre,
                Generado = DateTime.Now, Historias = datos.Historias(null, DescifrarDiagnostico)
            };
            Archivos.GuardarRespaldo(ruta, respaldo, proteccion);
        }

        public string VerificarRespaldo(string ruta)
        {
            Exigir(Seccion.Seguridad);
            RespaldoClinico respaldo = Archivos.VerificarRespaldo(ruta, proteccion);
            return $"Respaldo del {respaldo.Generado:dd/MM/yyyy HH:mm} verificado. Historias: {respaldo.Historias.Count}.";
        }

        private string DescifrarDiagnostico(byte[] diagnostico) { return proteccion.LeerTexto(diagnostico, "diagnostico"); }
    }
}
