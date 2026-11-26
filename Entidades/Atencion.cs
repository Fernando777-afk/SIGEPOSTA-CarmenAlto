using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Entidades
{
    public sealed class Cita
    {
        public Cita() { }
        public Cita(int paciente, int medico, int consultorio, DateTime fecha)
        {
            IdPaciente = paciente;
            IdMedico = medico;
            IdConsultorio = consultorio;
            FechaHora = fecha;
            Estado = "Programada";
        }

        public int Id { get; set; }
        public int IdPaciente { get; set; }
        public int IdMedico { get; set; }
        public int IdConsultorio { get; set; }
        public DateTime FechaHora { get; set; }
        public DateTime? Llegada { get; set; }
        public string Estado { get; set; }
        public string Paciente { get; set; }
        public string Medico { get; set; }
        public string Consultorio { get; set; }
        public string EstadoVisible { get { return Estado == "EnEspera" ? "En espera" : Estado; } }

        public void Reprogramar(DateTime fecha) { FechaHora = fecha; }
        public void Reprogramar(int consultorio) { IdConsultorio = consultorio; }
    }

    public sealed class HistoriaClinica
    {
        public HistoriaClinica() { Atenciones = new List<Atencion>(); }
        public HistoriaClinica(int numero, int paciente) : this() { Id = numero; IdPaciente = paciente; }
        public int Id { get; set; }
        public int IdPaciente { get; set; }
        public string NombrePaciente { get; set; }
        public string Numero { get { return "HC-" + Id.ToString("D6"); } }
        public List<Atencion> Atenciones { get; set; }
    }

    public sealed class Atencion
    {
        public Atencion() { Recetas = new List<Receta>(); }
        public int Id { get; set; }
        public int IdCita { get; set; }
        public DateTime Fecha { get; set; }
        public string Diagnostico { get; set; }
        public decimal Temperatura { get; set; }
        public decimal Peso { get; set; }
        public string Presion { get; set; }
        public List<Receta> Recetas { get; set; }
    }

    public sealed class Receta
    {
        public int IdMedicamento { get; set; }
        public string Medicamento { get; set; }
        public int Cantidad { get; set; }
        public string Indicaciones { get; set; }
    }
}
