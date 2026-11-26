using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Datos
{
    public sealed partial class Almacen
    {
        public int RegistrarAtencion(Atencion atencion, byte[] diagnostico, DateTime ahora, int? medico)
        {
            return sql.Transaccion(c =>
            {
                int modificadas = c.Ejecutar(@"UPDATE dbo.Cita SET Estado='Atendida'
WHERE IdCita=@cita AND Estado='EnEspera' AND FechaHora>=@desde AND FechaHora<@hasta
AND (@medico IS NULL OR IdPersonal=@medico)", Id("@cita", atencion.IdCita),
                    Fecha("@desde", ahora.Date), Fecha("@hasta", ahora.Date.AddDays(1)), P("@medico", SqlDbType.Int, medico));
                if (modificadas != 1) throw new InvalidOperationException("Selecciona una cita asignada que siga en la cola de hoy.");

                var temperatura = P("@temperatura", SqlDbType.Decimal, atencion.Temperatura);
                temperatura.Precision = 4;
                temperatura.Scale = 1;
                var peso = P("@peso", SqlDbType.Decimal, atencion.Peso);
                peso.Precision = 5;
                peso.Scale = 1;
                int id = Convert.ToInt32(c.Valor(@"INSERT dbo.Atencion (IdCita, DiagnosticoCifrado, Temperatura, Peso, Presion, FechaRegistro)
OUTPUT inserted.IdAtencion VALUES (@cita, @diagnostico, @temperatura, @peso, @presion, @ahora)",
                    Id("@cita", atencion.IdCita), P("@diagnostico", SqlDbType.VarBinary, diagnostico, -1),
                    temperatura, peso, P("@presion", SqlDbType.NVarChar, atencion.Presion, 15), Fecha("@ahora", ahora)));

                // Un orden estable de lotes evita invertir el orden de bloqueos entre dos recetas.
                foreach (Receta receta in atencion.Recetas.OrderBy(r => r.IdMedicamento))
                {
                    AplicarMovimiento(c, new MovimientoInventario(receta.IdMedicamento, "Salida", receta.Cantidad, "Receta de atención"), id, ahora);
                    c.Ejecutar(@"INSERT dbo.Receta (IdAtencion, IdMedicamento, Cantidad, Indicaciones)
VALUES (@atencion, @medicamento, @cantidad, @indicaciones)", Id("@atencion", id),
                        Id("@medicamento", receta.IdMedicamento), Id("@cantidad", receta.Cantidad),
                        P("@indicaciones", SqlDbType.NVarChar, receta.Indicaciones, 250));
                }
                return id;
            });
        }

        public List<HistoriaClinica> Historias(int? paciente, Func<byte[], string> descifrar, int? medico = null)
        {
            return sql.Ejecutar(c =>
            {
                var historias = new Dictionary<int, HistoriaClinica>();
                var atenciones = new Dictionary<int, Atencion>();
                c.Leer(@"SELECT h.IdHistoria, h.IdPaciente, p.Nombres + N' ' + p.Apellidos AS Paciente,
    a.IdAtencion, a.IdCita, a.FechaRegistro, a.DiagnosticoCifrado, a.Temperatura, a.Peso, a.Presion,
    r.IdMedicamento, r.Cantidad, r.Indicaciones, m.Nombre + N' · ' + m.Lote AS Medicamento
FROM dbo.HistoriaClinica h JOIN dbo.Persona p ON p.IdPersona=h.IdPaciente
LEFT JOIN dbo.Cita x ON x.IdPaciente=h.IdPaciente AND x.Estado='Atendida'
LEFT JOIN dbo.Atencion a ON a.IdCita=x.IdCita LEFT JOIN dbo.Receta r ON r.IdAtencion=a.IdAtencion
LEFT JOIN dbo.Medicamento m ON m.IdMedicamento=r.IdMedicamento
WHERE (@paciente IS NULL OR h.IdPaciente=@paciente)
AND (@medico IS NULL OR EXISTS (SELECT 1 FROM dbo.Cita asignada WHERE asignada.IdPaciente=h.IdPaciente
    AND asignada.IdPersonal=@medico AND asignada.Estado<>'Cancelada'))
ORDER BY h.IdHistoria, a.FechaRegistro DESC, a.IdAtencion DESC, r.IdMedicamento",
                    r =>
                    {
                        int id = r.Entero("IdHistoria");
                        HistoriaClinica historia;
                        if (!historias.TryGetValue(id, out historia))
                        {
                            historia = new HistoriaClinica(id, r.Entero("IdPaciente")) { NombrePaciente = r.Texto("Paciente") };
                            historias.Add(id, historia);
                        }
                        if (r["IdAtencion"] == DBNull.Value) return false;
                        int idAtencion = r.Entero("IdAtencion");
                        Atencion atencion;
                        if (!atenciones.TryGetValue(idAtencion, out atencion))
                        {
                            atencion = new Atencion
                            {
                                Id = idAtencion, IdCita = r.Entero("IdCita"), Fecha = r.Fecha("FechaRegistro"),
                                Diagnostico = descifrar(r.Bytes("DiagnosticoCifrado")),
                                Temperatura = r.Decimal("Temperatura"), Peso = r.Decimal("Peso"), Presion = r.Texto("Presion")
                            };
                            atenciones.Add(idAtencion, atencion);
                            historia.Atenciones.Add(atencion);
                        }
                        if (r["IdMedicamento"] != DBNull.Value)
                            atencion.Recetas.Add(new Receta
                            {
                                IdMedicamento = r.Entero("IdMedicamento"), Medicamento = r.Texto("Medicamento"),
                                Cantidad = r.Entero("Cantidad"), Indicaciones = r.Texto("Indicaciones")
                            });
                        return true;
                    }, P("@paciente", SqlDbType.Int, paciente), P("@medico", SqlDbType.Int, medico));
                return historias.Values.OrderBy(h => h.Id).ToList();
            });
        }
    }
}
