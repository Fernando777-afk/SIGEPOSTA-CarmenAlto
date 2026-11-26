using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Entidades
{
    public enum Rol { Administrador, Responsable, Admision, ProfesionalSalud, Farmacia, Estadistica }

    public sealed class Sesion
    {
        public Sesion(int id, string nombre, Rol rol, int? idPersonal = null)
        {
            Id = id; Nombre = nombre; Rol = rol; IdPersonal = idPersonal;
        }
        public int Id { get; }
        public string Nombre { get; }
        public Rol Rol { get; }
        public int? IdPersonal { get; }
    }

    public sealed class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public Rol Rol { get; set; }
        public bool Activo { get; set; }
        public int? IdPersonal { get; set; }
        public string ProfesionalAsignado { get; set; }
    }

    public sealed class Opcion
    {
        public Opcion(int id, string nombre) { Id = id; Nombre = nombre; }
        public int Id { get; }
        public string Nombre { get; }
        public override string ToString() { return Nombre; }
    }

    public sealed class Resumen
    {
        public int Citas { get; set; }
        public int PacientesAtendidos { get; set; }
        public int StockCritico { get; set; }
        public int EnEspera { get; set; }
    }

    public sealed class AtencionesPorDia
    {
        public DateTime Fecha { get; set; }
        public int Total { get; set; }
    }

    public sealed class PanelInicio
    {
        public Resumen Indicadores { get; set; }
        public List<AtencionesPorDia> Atenciones { get; set; }
        public List<Cita> Proximas { get; set; }
        public List<Cita> Cola { get; set; }
        public List<Medicamento> Alertas { get; set; }
    }

    public sealed class RespaldoClinico
    {
        public int Version { get; set; }
        public string Establecimiento { get; set; }
        public DateTime Generado { get; set; }
        public List<HistoriaClinica> Historias { get; set; }
    }

    public sealed class ReporteDiario
    {
        public DateTime Fecha { get; set; }
        public int PacientesAtendidos { get; set; }
        public List<TurnoReporte> Turnos { get; set; }
    }

    public sealed class TurnoReporte
    {
        public DateTime FechaHora { get; set; }
        public string Consultorio { get; set; }
        public string Estado { get; set; }
    }
}
