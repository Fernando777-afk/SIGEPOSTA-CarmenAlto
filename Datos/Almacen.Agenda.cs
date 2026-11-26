using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Datos
{
    public sealed partial class Almacen
    {
        private const string ConsultaCitas = @"SELECT x.IdCita, x.IdPaciente, x.IdPersonal, x.IdConsultorio, x.FechaHora, x.Estado, x.Llegada,
    p.Nombres + N' ' + p.Apellidos AS Paciente, m.Nombres + N' ' + m.Apellidos AS Medico, o.Nombre AS Consultorio
FROM dbo.Cita x JOIN dbo.Persona p ON p.IdPersona=x.IdPaciente
JOIN dbo.Persona m ON m.IdPersona=x.IdPersonal JOIN dbo.Consultorio o ON o.IdConsultorio=x.IdConsultorio ";

        public List<Opcion> Consultorios()
        {
            return sql.Ejecutar(c => c.Leer("SELECT IdConsultorio, Nombre FROM dbo.Consultorio ORDER BY Nombre",
                r => new Opcion(r.Entero("IdConsultorio"), r.Texto("Nombre"))));
        }

        public List<Cita> Citas(DateTime dia, int? medico = null)
        {
            return sql.Ejecutar(c => c.Leer(ConsultaCitas + @"WHERE x.FechaHora>=@desde AND x.FechaHora<@hasta
AND (@medico IS NULL OR x.IdPersonal=@medico) ORDER BY x.FechaHora, x.IdCita",
                LeerCita, Fecha("@desde", dia.Date), Fecha("@hasta", dia.Date.AddDays(1)), P("@medico", SqlDbType.Int, medico)));
        }

        public List<Cita> ProximasCitas(DateTime desde, DateTime hasta, int? medico)
        {
            return sql.Ejecutar(c => c.Leer(ConsultaCitas + @"WHERE x.FechaHora>=@desde AND x.FechaHora<@hasta
AND x.Estado='Programada' AND (@medico IS NULL OR x.IdPersonal=@medico)
ORDER BY x.FechaHora, x.IdCita OFFSET 0 ROWS FETCH NEXT 12 ROWS ONLY", LeerCita,
                Fecha("@desde", desde), Fecha("@hasta", hasta), P("@medico", SqlDbType.Int, medico)));
        }

        private static Cita LeerCita(IDataRecord r)
        {
            return new Cita
            {
                Id = r.Entero("IdCita"), IdPaciente = r.Entero("IdPaciente"), IdMedico = r.Entero("IdPersonal"),
                IdConsultorio = r.Entero("IdConsultorio"), FechaHora = r.Fecha("FechaHora"),
                Estado = r.Texto("Estado"), Llegada = r["Llegada"] as DateTime?,
                Paciente = r.Texto("Paciente"), Medico = r.Texto("Medico"), Consultorio = r.Texto("Consultorio")
            };
        }

        private const string FiltroDisponibles = @"s.Tipo='Medico'
AND EXISTS (SELECT 1 FROM dbo.Horario h WHERE h.IdPersonal=s.IdPersonal AND h.Dia=@dia
    AND h.Inicio<=@inicio AND h.Fin>=@fin)
AND NOT EXISTS (SELECT 1 FROM dbo.Cita x WHERE x.IdPersonal=s.IdPersonal AND x.FechaHora=@fecha
    AND x.IdCita<>@id AND x.Estado<>'Cancelada')";

        private static SqlParameter[] Turno(DateTime fecha, int cita)
        {
            return new[]
            {
                Fecha("@fecha", fecha), Id("@id", cita), P("@dia", SqlDbType.TinyInt, (byte)fecha.DayOfWeek),
                P("@inicio", SqlDbType.Time, fecha.TimeOfDay),
                P("@fin", SqlDbType.Time, fecha.AddMinutes(30).TimeOfDay)
            };
        }

        public List<Opcion> MedicosDisponibles(DateTime fecha, int cita)
        {
            if (fecha.TimeOfDay >= TimeSpan.FromHours(23.5)) return new List<Opcion>();
            return sql.Ejecutar(c => c.Leer(@"SELECT s.IdPersonal, p.Nombres + N' ' + p.Apellidos + N' · ' + s.Area AS Nombre
FROM dbo.PersonalSalud s JOIN dbo.Persona p ON p.IdPersona=s.IdPersonal WHERE " + FiltroDisponibles
                + " ORDER BY p.Apellidos, p.Nombres", r => new Opcion(r.Entero("IdPersonal"), r.Texto("Nombre")), Turno(fecha, cita)));
        }

        public void GuardarCita(Cita cita)
        {
            sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Agenda");
                SqlParameter[] turno = Turno(cita.FechaHora, cita.Id);
                int disponible = Convert.ToInt32(c.Valor(@"SELECT COUNT(*) FROM dbo.PersonalSalud s
WHERE s.IdPersonal=@medico AND " + FiltroDisponibles,
                    turno.Concat(new[] { Id("@medico", cita.IdMedico) }).ToArray()));
                if (disponible != 1)
                    throw new InvalidOperationException("El médico ya no está disponible en ese turno. Consulta la disponibilidad.");
                int conflictos = Convert.ToInt32(c.Valor(@"SELECT COUNT(*) FROM dbo.Cita
WHERE FechaHora=@fecha AND IdCita<>@id AND Estado<>'Cancelada'
AND (IdPaciente=@paciente OR IdConsultorio=@consultorio)", Fecha("@fecha", cita.FechaHora), Id("@id", cita.Id),
                    Id("@paciente", cita.IdPaciente), Id("@consultorio", cita.IdConsultorio)));
                if (conflictos > 0)
                    throw new InvalidOperationException("El consultorio o paciente ya tiene una cita en ese turno.");

                SqlParameter[] datos = { Id("@paciente", cita.IdPaciente), Id("@medico", cita.IdMedico),
                    Id("@consultorio", cita.IdConsultorio), Fecha("@fecha", cita.FechaHora) };
                if (cita.Id == 0)
                    c.Ejecutar(@"INSERT dbo.Cita (IdPaciente, IdPersonal, IdConsultorio, FechaHora)
VALUES (@paciente, @medico, @consultorio, @fecha)", datos);
                else if (c.Ejecutar(@"UPDATE dbo.Cita SET IdPersonal=@medico, IdConsultorio=@consultorio, FechaHora=@fecha
WHERE IdCita=@id AND IdPaciente=@paciente AND Estado='Programada'",
                    datos.Concat(new[] { Id("@id", cita.Id) }).ToArray()) != 1)
                    throw new InvalidOperationException("Solo se pueden reprogramar citas programadas.");
                return true;
            });
        }

        public void CancelarCita(int id)
        {
            int filas = sql.Ejecutar(c => c.Ejecutar(@"UPDATE dbo.Cita SET Estado='Cancelada'
WHERE IdCita=@id AND Estado IN ('Programada','EnEspera')", Id("@id", id)));
            if (filas != 1) throw new InvalidOperationException("La cita ya no admite cancelación.");
        }

        public void RegistrarLlegada(int id, DateTime ahora)
        {
            int filas = sql.Ejecutar(c => c.Ejecutar(@"UPDATE dbo.Cita SET Estado='EnEspera', Llegada=@ahora
WHERE IdCita=@id AND Estado='Programada' AND FechaHora>=@desde AND FechaHora<@hasta",
                Id("@id", id), Fecha("@ahora", ahora), Fecha("@desde", ahora.Date), Fecha("@hasta", ahora.Date.AddDays(1))));
            if (filas != 1) throw new InvalidOperationException("Solo se puede registrar la llegada de una cita programada para hoy.");
        }

        public Resumen Resumen(DateTime dia, int? medico = null)
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT
    (SELECT COUNT(*) FROM dbo.Cita WHERE FechaHora>=@desde AND FechaHora<@hasta AND Estado<>'Cancelada'
        AND (@medico IS NULL OR IdPersonal=@medico)) AS Citas,
    (SELECT COUNT(DISTINCT IdPaciente) FROM dbo.Cita WHERE FechaHora>=@desde AND FechaHora<@hasta AND Estado='Atendida'
        AND (@medico IS NULL OR IdPersonal=@medico)) AS Pacientes,
    (SELECT COUNT(*) FROM dbo.Cita WHERE FechaHora>=@desde AND FechaHora<@hasta AND Estado='EnEspera'
        AND (@medico IS NULL OR IdPersonal=@medico)) AS Espera,
    (SELECT COUNT(*) FROM (SELECT DISTINCT Nombre,Concentracion,FormaFarmaceutica
        FROM dbo.Medicamento WHERE StockActual<StockMinimo) AS Criticos) AS Stock",
                r => new Resumen
                {
                    Citas = r.Entero("Citas"), PacientesAtendidos = r.Entero("Pacientes"),
                    EnEspera = r.Entero("Espera"), StockCritico = r.Entero("Stock")
                }, Fecha("@desde", dia.Date), Fecha("@hasta", dia.Date.AddDays(1)), P("@medico", SqlDbType.Int, medico)).Single());
        }

        public List<AtencionesPorDia> SerieAtenciones(DateTime desde, DateTime hasta, int? medico)
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT CONVERT(date,a.FechaRegistro) AS Dia, COUNT(*) AS Total
FROM dbo.Atencion a JOIN dbo.Cita x ON x.IdCita=a.IdCita
WHERE a.FechaRegistro>=@desde AND a.FechaRegistro<@hasta AND (@medico IS NULL OR x.IdPersonal=@medico)
GROUP BY CONVERT(date,a.FechaRegistro) ORDER BY Dia", r => new AtencionesPorDia { Fecha = r.Fecha("Dia"), Total = r.Entero("Total") },
                Fecha("@desde", desde), Fecha("@hasta", hasta), P("@medico", SqlDbType.Int, medico)));
        }

        public DataTable PacientesAtendidos(DateTime dia)
        {
            return sql.Ejecutar(c => c.Tabla(@"SELECT h.IdHistoria, p.Nombres + N' ' + p.Apellidos AS Paciente,
COUNT(*) AS Atenciones FROM dbo.Cita x
JOIN dbo.Persona p ON p.IdPersona=x.IdPaciente JOIN dbo.HistoriaClinica h ON h.IdPaciente=x.IdPaciente
WHERE x.Estado='Atendida' AND x.FechaHora>=@desde AND x.FechaHora<@hasta
GROUP BY h.IdHistoria, p.Nombres, p.Apellidos ORDER BY p.Apellidos, p.Nombres",
                Fecha("@desde", dia.Date), Fecha("@hasta", dia.Date.AddDays(1))));
        }
    }
}
