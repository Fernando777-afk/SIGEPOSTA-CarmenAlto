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
        public List<Medicamento> Medicamentos()
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT IdMedicamento, Nombre, Lote, StockActual, StockMinimo, FechaVencimiento,
    Concentracion, FormaFarmaceutica, UnidadMedida, EsEjemplo
FROM dbo.Medicamento ORDER BY Nombre, FechaVencimiento", r => new Medicamento
            {
                Id = r.Entero("IdMedicamento"), Nombre = r.Texto("Nombre"), Lote = r.Texto("Lote"),
                StockActual = r.Entero("StockActual"), StockMinimo = r.Entero("StockMinimo"),
                FechaVencimiento = r.Fecha("FechaVencimiento"), Concentracion = r.Texto("Concentracion"),
                FormaFarmaceutica = r.Texto("FormaFarmaceutica"), UnidadMedida = r.Texto("UnidadMedida"),
                EsEjemplo = (bool)r["EsEjemplo"]
            }));
        }

        public void GuardarMedicamento(Medicamento medicamento)
        {
            sql.Ejecutar(c =>
            {
                SqlParameter[] datos =
                {
                    P("@nombre", SqlDbType.NVarChar, medicamento.Nombre, 100),
                    P("@lote", SqlDbType.NVarChar, medicamento.Lote, 40), Id("@minimo", medicamento.StockMinimo),
                    P("@vence", SqlDbType.Date, medicamento.FechaVencimiento.Date),
                    P("@concentracion", SqlDbType.NVarChar, medicamento.Concentracion, 40),
                    P("@forma", SqlDbType.NVarChar, medicamento.FormaFarmaceutica, 80),
                    P("@unidad", SqlDbType.NVarChar, medicamento.UnidadMedida, 30)
                };
                if (medicamento.Id == 0)
                    c.Ejecutar(@"INSERT dbo.Medicamento (Nombre, Lote, StockMinimo, FechaVencimiento, Concentracion, FormaFarmaceutica, UnidadMedida)
VALUES (@nombre, @lote, @minimo, @vence, @concentracion, @forma, @unidad)", datos);
                else if (c.Ejecutar(@"UPDATE dbo.Medicamento SET Nombre=@nombre, Lote=@lote, StockMinimo=@minimo, FechaVencimiento=@vence,
    Concentracion=@concentracion, FormaFarmaceutica=@forma, UnidadMedida=@unidad
WHERE IdMedicamento=@id", datos.Concat(new[] { Id("@id", medicamento.Id) }).ToArray()) != 1)
                    throw new InvalidOperationException("El medicamento ya no está disponible.");
                return true;
            });
        }

        public List<RecetaFarmacia> RecetasFarmacia(DateTime dia)
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT a.IdAtencion, a.FechaRegistro,
    p.Nombres + N' ' + p.Apellidos AS Paciente,
    CONCAT(m.Nombre, N' ', m.Concentracion, N' · ', m.Lote) AS Medicamento,
    r.Cantidad, r.Indicaciones
FROM dbo.Receta r JOIN dbo.Atencion a ON a.IdAtencion=r.IdAtencion
JOIN dbo.Cita x ON x.IdCita=a.IdCita JOIN dbo.Persona p ON p.IdPersona=x.IdPaciente
JOIN dbo.Medicamento m ON m.IdMedicamento=r.IdMedicamento
WHERE a.FechaRegistro>=@desde AND a.FechaRegistro<@hasta
ORDER BY a.FechaRegistro DESC, a.IdAtencion DESC, m.Nombre", r => new RecetaFarmacia
            {
                Atencion = r.Entero("IdAtencion"), Fecha = r.Fecha("FechaRegistro"), Paciente = r.Texto("Paciente"),
                Medicamento = r.Texto("Medicamento"), Cantidad = r.Entero("Cantidad"), Indicaciones = r.Texto("Indicaciones")
            }, Fecha("@desde", dia.Date), Fecha("@hasta", dia.Date.AddDays(1))));
        }

        public List<MovimientoInventario> Movimientos(int id)
        {
            return sql.Ejecutar(c => c.Leer(@"SELECT v.IdMovimiento, v.IdMedicamento, v.Tipo, v.Cantidad, v.Motivo, v.Fecha,
    m.Nombre + N' · ' + m.Lote AS Medicamento FROM dbo.MovimientoInventario v
JOIN dbo.Medicamento m ON m.IdMedicamento=v.IdMedicamento
WHERE v.IdMedicamento=@id ORDER BY v.Fecha DESC, v.IdMovimiento DESC", r => new MovimientoInventario
            {
                Id = r.Entero("IdMovimiento"), IdMedicamento = r.Entero("IdMedicamento"),
                Medicamento = r.Texto("Medicamento"), Tipo = r.Texto("Tipo"), Cantidad = r.Entero("Cantidad"),
                Motivo = r.Texto("Motivo"), Fecha = r.Fecha("Fecha")
            }, Id("@id", id)));
        }

        public void RegistrarMovimiento(MovimientoInventario movimiento)
        {
            sql.Transaccion(c =>
            {
                AplicarMovimiento(c, movimiento, null, DateTime.Today);
                return true;
            });
        }

        private static void AplicarMovimiento(Consulta c, MovimientoInventario movimiento, int? atencion, DateTime hoy)
        {
            int cambio = movimiento.Tipo == "Ingreso" ? movimiento.Cantidad : -movimiento.Cantidad;
            int filas = c.Ejecutar(@"UPDATE dbo.Medicamento SET StockActual=CONVERT(int, CONVERT(bigint, StockActual)+@cambio)
WHERE IdMedicamento=@id AND CONVERT(bigint, StockActual)+@cambio BETWEEN 0 AND 2147483647
AND (@receta=0 OR FechaVencimiento>=@hoy)", Id("@id", movimiento.IdMedicamento), Id("@cambio", cambio),
                P("@receta", SqlDbType.Bit, atencion.HasValue), P("@hoy", SqlDbType.Date, hoy.Date));
            if (filas != 1)
                throw new InvalidOperationException("Revisa la existencia, la cantidad y el vencimiento del lote seleccionado.");

            c.Ejecutar(@"INSERT dbo.MovimientoInventario (IdMedicamento, IdAtencion, Tipo, Cantidad, Motivo)
VALUES (@id, @atencion, @tipo, @cantidad, @motivo)", Id("@id", movimiento.IdMedicamento),
                P("@atencion", SqlDbType.Int, atencion), P("@tipo", SqlDbType.VarChar, movimiento.Tipo, 7),
                Id("@cantidad", movimiento.Cantidad), P("@motivo", SqlDbType.NVarChar, movimiento.Motivo, 160));
        }
    }
}
