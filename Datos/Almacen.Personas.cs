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
        private const string SeleccionPacientes = @"SELECT p.IdPaciente, h.IdHistoria, p.Direccion,
    b.Nombres, b.Apellidos, b.FechaNacimiento, b.Telefono, b.DniCifrado
FROM dbo.Paciente p JOIN dbo.Persona b ON b.IdPersona=p.IdPaciente
JOIN dbo.HistoriaClinica h ON h.IdPaciente=p.IdPaciente ";

        private const string SeleccionPersonal = @"SELECT s.IdPersonal, s.Tipo, s.Area, s.Colegiatura,
    b.Nombres, b.Apellidos, b.FechaNacimiento, b.Telefono, b.DniCifrado
FROM dbo.PersonalSalud s JOIN dbo.Persona b ON b.IdPersona=s.IdPersonal ";

        public List<Paciente> Pacientes(byte[] indice, int? medico = null)
        {
            return sql.Ejecutar(c => c.Leer(SeleccionPacientes + @"WHERE (@indice IS NULL OR b.DniIndice=@indice)
AND (@medico IS NULL OR EXISTS (SELECT 1 FROM dbo.Cita x WHERE x.IdPaciente=p.IdPaciente
    AND x.IdPersonal=@medico AND x.Estado<>'Cancelada')) ORDER BY b.Apellidos, b.Nombres",
                LeerPaciente, P("@indice", SqlDbType.Binary, indice, 32), P("@medico", SqlDbType.Int, medico)));
        }

        public RegistroProtegido<Paciente> Paciente(int id)
        {
            return sql.Ejecutar(c => c.Leer(SeleccionPacientes + "WHERE p.IdPaciente=@id",
                r => new RegistroProtegido<Paciente>(LeerPaciente(r), r.Bytes("DniCifrado")), Id("@id", id)).SingleOrDefault());
        }

        public List<PersonalSalud> Personal()
        {
            return sql.Ejecutar(c => c.Leer(SeleccionPersonal + "ORDER BY b.Apellidos, b.Nombres", LeerPersonal));
        }

        public RegistroProtegido<PersonalSalud> Profesional(int id)
        {
            return sql.Ejecutar(c => c.Leer(SeleccionPersonal + "WHERE s.IdPersonal=@id",
                r => new RegistroProtegido<PersonalSalud>(LeerPersonal(r), r.Bytes("DniCifrado")), Id("@id", id)).SingleOrDefault());
        }

        private static Paciente LeerPaciente(IDataRecord r)
        {
            var paciente = new Paciente
            {
                Id = r.Entero("IdPaciente"), IdHistoria = r.Entero("IdHistoria"), Direccion = r.Texto("Direccion")
            };
            LeerPersona(paciente, r);
            return paciente;
        }

        private static PersonalSalud LeerPersonal(IDataRecord r)
        {
            string tipo = r.Texto("Tipo");
            PersonalSalud personal;
            if (tipo == "Medico") personal = new Medico();
            else if (tipo == "Enfermero") personal = new Enfermero();
            else personal = new PersonalApoyo(tipo);
            personal.Id = r.Entero("IdPersonal");
            personal.Area = r.Texto("Area");
            personal.Colegiatura = r.Texto("Colegiatura");
            LeerPersona(personal, r);
            return personal;
        }

        private static void LeerPersona(Persona persona, IDataRecord r)
        {
            persona.Nombres = r.Texto("Nombres");
            persona.Apellidos = r.Texto("Apellidos");
            persona.FechaNacimiento = r.Fecha("FechaNacimiento");
            persona.Telefono = r.Texto("Telefono");
        }

        private static int GuardarPersona(Consulta c, Persona persona, byte[] cifrado, byte[] indice)
        {
            SqlParameter[] parametros =
            {
                P("@dni", SqlDbType.VarBinary, cifrado, 128), P("@indice", SqlDbType.Binary, indice, 32),
                P("@nombres", SqlDbType.NVarChar, persona.Nombres, 70), P("@apellidos", SqlDbType.NVarChar, persona.Apellidos, 80),
                P("@nacimiento", SqlDbType.Date, persona.FechaNacimiento.Date),
                P("@telefono", SqlDbType.NVarChar, persona.Telefono, 20)
            };
            if (persona.Id == 0)
                return Convert.ToInt32(c.Valor(@"INSERT dbo.Persona (DniCifrado, DniIndice, Nombres, Apellidos, FechaNacimiento, Telefono)
OUTPUT inserted.IdPersona VALUES (@dni, @indice, @nombres, @apellidos, @nacimiento, @telefono)", parametros));

            c.Ejecutar(@"UPDATE dbo.Persona SET DniCifrado=@dni, DniIndice=@indice, Nombres=@nombres,
    Apellidos=@apellidos, FechaNacimiento=@nacimiento, Telefono=@telefono WHERE IdPersona=@id",
                parametros.Concat(new[] { Id("@id", persona.Id) }).ToArray());
            return persona.Id;
        }

        public int GuardarPaciente(Paciente paciente, byte[] cifrado, byte[] indice)
        {
            return sql.Transaccion(c =>
            {
                int id = GuardarPersona(c, paciente, cifrado, indice);
                if (paciente.Id == 0)
                {
                    c.Ejecutar("INSERT dbo.Paciente (IdPaciente, Direccion) VALUES (@id, @direccion)",
                        Id("@id", id), P("@direccion", SqlDbType.NVarChar, paciente.Direccion, 160));
                    c.Ejecutar("INSERT dbo.HistoriaClinica (IdPaciente) VALUES (@id)", Id("@id", id));
                }
                else if (c.Ejecutar("UPDATE dbo.Paciente SET Direccion=@direccion WHERE IdPaciente=@id",
                    Id("@id", id), P("@direccion", SqlDbType.NVarChar, paciente.Direccion, 160)) != 1)
                    throw new InvalidOperationException("El paciente ya no está disponible.");
                return id;
            });
        }

        public int GuardarPersonal(PersonalSalud personal, byte[] cifrado, byte[] indice)
        {
            return sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Agenda");
                int id = GuardarPersona(c, personal, cifrado, indice);
                SqlParameter[] datos =
                {
                    Id("@id", id), P("@tipo", SqlDbType.VarChar, personal.Tipo, 16),
                    P("@area", SqlDbType.NVarChar, personal.Area, 80), P("@colegiatura", SqlDbType.NVarChar, personal.Colegiatura, 30)
                };
                if (personal.Id == 0)
                    c.Ejecutar("INSERT dbo.PersonalSalud (IdPersonal, Tipo, Area, Colegiatura) VALUES (@id, @tipo, @area, @colegiatura)", datos);
                else if (c.Ejecutar(@"UPDATE dbo.PersonalSalud SET Tipo=@tipo, Area=@area, Colegiatura=@colegiatura
WHERE IdPersonal=@id AND Tipo=@tipo", datos) != 1)
                    throw new InvalidOperationException("El tipo de personal debe coincidir con el del registro original.");
                return id;
            });
        }

        public List<Horario> Horarios()
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT h.IdHorario, h.IdPersonal, h.Dia, h.Inicio, h.Fin,
    p.Nombres + N' ' + p.Apellidos AS Profesional FROM dbo.Horario h
JOIN dbo.Persona p ON p.IdPersona=h.IdPersonal ORDER BY p.Apellidos, p.Nombres, h.Dia, h.Inicio",
                r => new Horario
                {
                    Id = r.Entero("IdHorario"), IdPersonal = r.Entero("IdPersonal"),
                    Dia = (DayOfWeek)r.Entero("Dia"), Inicio = (TimeSpan)r["Inicio"], Fin = (TimeSpan)r["Fin"],
                    Profesional = r.Texto("Profesional")
                }));
        }

        public void GuardarHorario(Horario horario)
        {
            sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Agenda");
                int solapados = Convert.ToInt32(c.Valor(@"SELECT COUNT(*) FROM dbo.Horario
WHERE IdPersonal=@personal AND Dia=@dia AND Inicio<@fin AND Fin>@inicio AND IdHorario<>@id",
                    Id("@personal", horario.IdPersonal), P("@dia", SqlDbType.TinyInt, (byte)horario.Dia),
                    P("@inicio", SqlDbType.Time, horario.Inicio), P("@fin", SqlDbType.Time, horario.Fin), Id("@id", horario.Id)));
                if (solapados > 0) throw new InvalidOperationException("El horario coincide con otro turno del profesional.");
                if (horario.Id > 0) ComprobarHorario(c, horario.Id, horario);
                SqlParameter[] valores =
                {
                    Id("@personal", horario.IdPersonal), P("@dia", SqlDbType.TinyInt, (byte)horario.Dia),
                    P("@inicio", SqlDbType.Time, horario.Inicio), P("@fin", SqlDbType.Time, horario.Fin)
                };
                if (horario.Id == 0)
                    c.Ejecutar("INSERT dbo.Horario (IdPersonal, Dia, Inicio, Fin) VALUES (@personal, @dia, @inicio, @fin)", valores);
                else
                    c.Ejecutar(@"UPDATE dbo.Horario SET IdPersonal=@personal, Dia=@dia, Inicio=@inicio, Fin=@fin
WHERE IdHorario=@id", valores.Concat(new[] { Id("@id", horario.Id) }).ToArray());
                return true;
            });
        }

        public void QuitarHorario(int id)
        {
            sql.Transaccion(c =>
            {
                c.Bloquear("SIGEPOSTA.Agenda");
                ComprobarHorario(c, id, null);
                c.Ejecutar("DELETE FROM dbo.Horario WHERE IdHorario=@id", Id("@id", id));
                return true;
            });
        }

        private static void ComprobarHorario(Consulta c, int id, Horario nuevo)
        {
            // El cálculo de día usa un domingo conocido para independizarse de DATEFIRST.
            int afectadas = Convert.ToInt32(c.Valor(@"SELECT COUNT(*) FROM dbo.Horario h
JOIN dbo.Cita x ON x.IdPersonal=h.IdPersonal
WHERE h.IdHorario=@id AND x.FechaHora>=@hoy AND x.Estado IN ('Programada','EnEspera')
AND DATEDIFF(DAY, CONVERT(date,'19000107',112), CONVERT(date,x.FechaHora)) % 7=h.Dia
AND CAST(x.FechaHora AS time)>=h.Inicio
AND DATEADD(MINUTE,30,x.FechaHora)<=DATEADD(SECOND,DATEDIFF(SECOND,CAST('00:00' AS time),h.Fin),CAST(CAST(x.FechaHora AS date) AS datetime2))
AND (@personal IS NULL OR h.IdPersonal<>@personal OR h.Dia<>@dia
     OR CAST(x.FechaHora AS time)<@inicio
     OR DATEADD(MINUTE,30,x.FechaHora)>DATEADD(SECOND,DATEDIFF(SECOND,CAST('00:00' AS time),@fin),CAST(CAST(x.FechaHora AS date) AS datetime2)))",
                Id("@id", id), Fecha("@hoy", DateTime.Today),
                P("@personal", SqlDbType.Int, nuevo?.IdPersonal), P("@dia", SqlDbType.TinyInt, nuevo == null ? (byte?)null : (byte)nuevo.Dia),
                P("@inicio", SqlDbType.Time, nuevo?.Inicio), P("@fin", SqlDbType.Time, nuevo?.Fin)));
            if (afectadas > 0)
                throw new InvalidOperationException("Hay citas activas en este horario. Reprográmalas antes de cambiarlo.");
        }
    }
}
