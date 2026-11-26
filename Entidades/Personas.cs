using System;
using System.Collections.Generic;
using System.Linq;

namespace SIGEPOSTA.Entidades
{
    public abstract class Persona
    {
        private string dni;

        protected Persona() { }

        protected Persona(string dni, string nombres, string apellidos, DateTime nacimiento)
        {
            Dni = dni;
            Nombres = nombres;
            Apellidos = apellidos;
            FechaNacimiento = nacimiento;
        }

        public int Id { get; set; }
        public string Dni
        {
            get { return dni; }
            set
            {
                if (value == null || value.Length != 8 || value.Any(letra => letra < '0' || letra > '9'))
                    throw new InvalidOperationException("El DNI debe contener ocho dígitos.");
                dni = value;
            }
        }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Telefono { get; set; }
        public string NombreCompleto { get { return (Nombres + " " + Apellidos).Trim(); } }

        public int ObtenerEdad()
        {
            DateTime hoy = DateTime.Today;
            int edad = hoy.Year - FechaNacimiento.Year;
            return FechaNacimiento.Date > hoy.AddYears(-edad) ? edad - 1 : edad;
        }

        public virtual string MostrarDatos() { return NombreCompleto; }
    }

    public sealed class Paciente : Persona
    {
        public Paciente() { }
        public Paciente(string dni, string nombres, string apellidos, DateTime nacimiento)
            : base(dni, nombres, apellidos, nacimiento) { }
        public int IdHistoria { get; set; }
        public string NumeroHistoria { get { return "HC-" + IdHistoria.ToString("D6"); } }
        public string Direccion { get; set; }
        public override string MostrarDatos() { return NombreCompleto + " · " + NumeroHistoria; }
    }

    public abstract class PersonalSalud : Persona
    {
        protected PersonalSalud() { }
        protected PersonalSalud(string dni, string nombres, string apellidos, DateTime nacimiento, string area)
            : base(dni, nombres, apellidos, nacimiento) { Area = area; }

        public abstract string Tipo { get; }
        public string Area { get; set; }
        public string Colegiatura { get; set; }
        public override string MostrarDatos() { return NombreCompleto + " · " + Area; }
    }

    public sealed class Medico : PersonalSalud
    {
        public Medico() { }
        public Medico(string dni, string nombres, string apellidos, DateTime nacimiento, string especialidad)
            : base(dni, nombres, apellidos, nacimiento, especialidad) { }
        public override string Tipo { get { return "Medico"; } }
        public override string MostrarDatos() { return base.MostrarDatos() + " · Médico"; }
    }

    public sealed class Enfermero : PersonalSalud
    {
        public Enfermero() { }
        public Enfermero(string dni, string nombres, string apellidos, DateTime nacimiento, string area)
            : base(dni, nombres, apellidos, nacimiento, area) { }
        public override string Tipo { get { return "Enfermero"; } }
        public override string MostrarDatos() { return base.MostrarDatos() + " · Enfermería"; }
    }

    public sealed class PersonalApoyo : PersonalSalud
    {
        private readonly string tipo;
        public PersonalApoyo(string tipo)
        {
            if (tipo != "Tecnico" && tipo != "Administrativo")
                throw new InvalidOperationException("Selecciona un tipo de personal válido.");
            this.tipo = tipo;
        }
        public override string Tipo { get { return tipo; } }
    }

    public sealed class Posta
    {
        public Posta() { }
        public Posta(string nombre, string distrito) { Nombre = nombre; Distrito = distrito; }
        public string Nombre { get; set; }
        public string Distrito { get; set; }
        public string Direccion { get; set; }
        public string Categoria { get; set; }
        public string Responsable { get; set; }
    }

    public sealed class Horario
    {
        public int Id { get; set; }
        public int IdPersonal { get; set; }
        public string Profesional { get; set; }
        public DayOfWeek Dia { get; set; }
        public TimeSpan Inicio { get; set; }
        public TimeSpan Fin { get; set; }
        public string NombreDia { get { return new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" }[(int)Dia]; } }
        public string Desde { get { return Inicio.ToString(@"hh\:mm"); } }
        public string Hasta { get { return Fin.ToString(@"hh\:mm"); } }
    }
}
