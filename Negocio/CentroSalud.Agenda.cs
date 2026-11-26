using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    public sealed partial class CentroSalud
    {
        public List<Opcion> Consultorios()
        {
            Sesion sesion = ExigirSesion();
            if (!Puede(sesion, Seccion.Citas) && !Puede(sesion, Seccion.Cola))
                throw new InvalidOperationException("Tu cuenta no tiene permiso para consultar los consultorios.");
            return datos.Consultorios();
        }

        public List<Cita> Citas(DateTime dia) { Exigir(Seccion.Citas); return datos.Citas(dia); }
        public List<Cita> CitasReporte(DateTime dia) { Exigir(Seccion.Reportes); return datos.Citas(dia); }
        public DataTable PacientesAtendidos(DateTime dia) { Exigir(Seccion.Reportes); return datos.PacientesAtendidos(dia); }

        public List<Opcion> MedicosDisponibles(DateTime fecha, int cita)
        {
            ExigirEdicion(Seccion.Citas);
            ValidarTurno(fecha);
            return datos.MedicosDisponibles(fecha, cita);
        }

        public void GuardarCita(Cita cita)
        {
            ExigirEdicion(Seccion.Citas);
            ValidarTurno(cita.FechaHora);
            if (cita.IdPaciente <= 0 || cita.IdMedico <= 0 || cita.IdConsultorio <= 0)
                throw new InvalidOperationException("Selecciona paciente, médico disponible y consultorio.");
            datos.GuardarCita(cita);
        }

        private static void ValidarTurno(DateTime fecha)
        {
            if (fecha <= DateTime.Now)
                throw new InvalidOperationException("Selecciona un horario posterior a la hora actual.");
            if (fecha.Ticks % TimeSpan.FromMinutes(30).Ticks != 0 || fecha.TimeOfDay >= TimeSpan.FromHours(23.5))
                throw new InvalidOperationException("Elige un turno de 30 minutos dentro del día.");
        }

        public void CancelarCita(int id) { ExigirEdicion(Seccion.Citas); datos.CancelarCita(id); }
        public void RegistrarLlegada(int id) { ExigirEdicion(Seccion.Citas); datos.RegistrarLlegada(id, DateTime.Now); }

        public List<Cita> Cola(int consultorio)
        {
            return ConsultarCola(consultorio, MedicoDe(Exigir(Seccion.Cola)));
        }

        private List<Cita> ConsultarCola(int consultorio, int? medico)
        {
            var grupos = datos.Citas(DateTime.Today, medico).Where(x => x.Estado == "EnEspera")
                .GroupBy(x => x.IdConsultorio)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Llegada).ThenBy(x => x.Id).ToList());
            List<Cita> seleccion;
            if (consultorio == 0)
                seleccion = grupos.Values.SelectMany(g => g).OrderBy(x => x.Llegada).ThenBy(x => x.Id).ToList();
            else if (!grupos.TryGetValue(consultorio, out seleccion))
                seleccion = new List<Cita>();
            return new ColaConsultorio(seleccion).ToList();
        }

        public PanelInicio ConsultarInicio(DateTime fechaAgenda)
        {
            Sesion sesion = Exigir(Seccion.Inicio);
            DateTime ahora = DateTime.Now;
            DateTime hoy = ahora.Date;
            DateTime primerDia = hoy.AddDays(-6);
            int? medico = MedicoDe(sesion);
            var serie = datos.SerieAtenciones(primerDia, hoy.AddDays(1), medico).ToDictionary(x => x.Fecha, x => x.Total);
            bool verAgenda = Puede(sesion, Seccion.Citas) || Puede(sesion, Seccion.Cola) || Puede(sesion, Seccion.Reportes);
            return new PanelInicio
            {
                Indicadores = datos.Resumen(hoy, medico),
                Atenciones = Enumerable.Range(0, 7).Select(i =>
                {
                    DateTime dia = primerDia.AddDays(i);
                    return new AtencionesPorDia { Fecha = dia, Total = serie.TryGetValue(dia, out int total) ? total : 0 };
                }).ToList(),
                Proximas = verAgenda ? datos.ProximasCitas(fechaAgenda.Date == hoy ? ahora : fechaAgenda.Date,
                    fechaAgenda.Date.AddDays(fechaAgenda.Date == hoy ? 7 : 1), medico) : null,
                Cola = Puede(sesion, Seccion.Cola) ? ConsultarCola(0, medico).Take(4).ToList() : null,
                Alertas = Puede(sesion, Seccion.Farmacia) || Puede(sesion, Seccion.Reportes) ? datos.Medicamentos()
                    .Where(m => m.StockCritico || m.FechaVencimiento <= hoy.AddDays(30))
                    .OrderBy(m => m.FechaVencimiento).ThenBy(m => m.StockActual).Take(4).ToList() : null
            };
        }

        public void ExportarReporte(DateTime fecha, string ruta)
        {
            Exigir(Seccion.Reportes);
            List<Cita> citas = datos.Citas(fecha);
            var reporte = new ReporteDiario
            {
                Fecha = fecha.Date,
                PacientesAtendidos = citas.Where(c => c.Estado == "Atendida").Select(c => c.IdPaciente).Distinct().Count(),
                Turnos = citas.Select(c => new TurnoReporte
                {
                    FechaHora = c.FechaHora, Consultorio = c.Consultorio, Estado = c.EstadoVisible
                }).ToList()
            };
            Archivos.GuardarReporte(ruta, reporte);
        }

        private sealed class ColaConsultorio : IEnumerable<Cita>
        {
            private readonly List<Cita> citas;
            public ColaConsultorio(List<Cita> citas) { this.citas = citas; }
            public IEnumerator<Cita> GetEnumerator()
            {
                foreach (Cita cita in citas) yield return cita;
            }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }
    }
}
