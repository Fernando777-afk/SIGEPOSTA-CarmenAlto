using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    public sealed partial class CentroSalud
    {
        public List<Paciente> BuscarPacientes(string busqueda)
        {
            Exigir(Seccion.Pacientes);
            return FiltrarPacientes(busqueda, true);
        }

        public List<Paciente> BuscarHistorias(string busqueda)
        {
            return FiltrarPacientes(busqueda, false, MedicoDe(Exigir(Seccion.Historias)));
        }

        public List<Paciente> PacientesParaCita()
        {
            ExigirEdicion(Seccion.Citas);
            return datos.Pacientes(null);
        }

        private List<Paciente> FiltrarPacientes(string busqueda, bool dniPermitido, int? medico = null)
        {
            string texto = (busqueda ?? "").Trim();
            if (dniPermitido && texto.Length == 8 && texto.All(c => c >= '0' && c <= '9'))
                return datos.Pacientes(proteccion.IndiceDni(texto));

            CompareInfo comparador = CultureInfo.GetCultureInfo("es-PE").CompareInfo;
            return datos.Pacientes(null, medico).Where(p => texto.Length == 0
                || comparador.IndexOf(p.NombreCompleto, texto, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
                || p.NumeroHistoria.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        public Paciente Paciente(int id)
        {
            Exigir(Seccion.Pacientes);
            return AbrirIdentidad(datos.Paciente(id));
        }

        public List<PersonalSalud> Personal()
        {
            Exigir(Seccion.Personal);
            return datos.Personal();
        }

        public PersonalSalud Profesional(int id)
        {
            ExigirEdicion(Seccion.Personal);
            return AbrirIdentidad(datos.Profesional(id));
        }

        private T AbrirIdentidad<T>(RegistroProtegido<T> fila) where T : Persona
        {
            if (fila == null) throw new InvalidOperationException("El registro ya no está disponible.");
            fila.Registro.Dni = proteccion.LeerTexto(fila.Cifrado, "dni");
            return fila.Registro;
        }

        public int GuardarPaciente(Paciente paciente)
        {
            ExigirEdicion(Seccion.Pacientes);
            ValidarPersona(paciente);
            paciente.Direccion = Texto(paciente.Direccion, 0, 160, "La dirección");
            return datos.GuardarPaciente(paciente, proteccion.CifrarTexto(paciente.Dni, "dni"), proteccion.IndiceDni(paciente.Dni));
        }

        public int GuardarPersonal(PersonalSalud personal)
        {
            ExigirEdicion(Seccion.Personal);
            ValidarPersona(personal);
            personal.Area = Texto(personal.Area, 1, 80, "El área o especialidad");
            personal.Colegiatura = Texto(personal.Colegiatura, 0, 30, "La colegiatura");
            return datos.GuardarPersonal(personal, proteccion.CifrarTexto(personal.Dni, "dni"), proteccion.IndiceDni(personal.Dni));
        }

        private static void ValidarPersona(Persona persona)
        {
            if (string.IsNullOrEmpty(persona.Dni)) throw new InvalidOperationException("Ingresa el DNI de ocho dígitos.");
            persona.Nombres = Texto(persona.Nombres, 1, 70, "El nombre");
            persona.Apellidos = Texto(persona.Apellidos, 1, 80, "El apellido");
            persona.Telefono = Texto(persona.Telefono, 0, 20, "El teléfono");
            if (persona.FechaNacimiento.Date > DateTime.Today || persona.FechaNacimiento.Date < new DateTime(1900, 1, 1))
                throw new InvalidOperationException("Selecciona una fecha de nacimiento válida.");
        }

        public List<Horario> Horarios()
        {
            Exigir(Seccion.Personal);
            return datos.Horarios();
        }

        public void GuardarHorario(Horario turno)
        {
            ExigirEdicion(Seccion.Personal);
            if (turno.IdPersonal <= 0 || !Enum.IsDefined(typeof(DayOfWeek), turno.Dia)
                || turno.Inicio < TimeSpan.Zero || turno.Fin >= TimeSpan.FromDays(1) || turno.Fin <= turno.Inicio)
                throw new InvalidOperationException("Elige un profesional y una hora de fin posterior a la de inicio.");
            datos.GuardarHorario(turno);
        }

        public void QuitarHorario(int id) { ExigirEdicion(Seccion.Personal); datos.QuitarHorario(id); }
    }
}
