using System;
using System.Collections.Generic;
using System.Linq;
using SIGEPOSTA.Entidades;

namespace SIGEPOSTA.Negocio
{
    public sealed partial class CentroSalud
    {
        public List<Medicamento> Medicamentos() { Exigir(Seccion.Farmacia); return datos.Medicamentos(); }
        public List<RecetaFarmacia> RecetasFarmacia(DateTime dia) { Exigir(Seccion.Farmacia); return datos.RecetasFarmacia(dia); }

        public List<Medicamento> StockCritico()
        {
            Exigir(Seccion.Reportes);
            return datos.Medicamentos().Where(m => m.StockCritico).ToList();
        }

        public void GuardarMedicamento(Medicamento medicamento)
        {
            ExigirEdicion(Seccion.Farmacia);
            medicamento.Nombre = Texto(medicamento.Nombre, 1, 100, "El nombre del medicamento");
            medicamento.Concentracion = Texto(medicamento.Concentracion, 1, 40, "La concentración");
            medicamento.FormaFarmaceutica = Texto(medicamento.FormaFarmaceutica, 1, 80, "La forma farmacéutica");
            medicamento.UnidadMedida = Texto(medicamento.UnidadMedida, 1, 30, "La unidad de medida");
            medicamento.Lote = Texto(medicamento.Lote, 1, 40, "El lote");
            if (medicamento.StockMinimo <= 0 || medicamento.FechaVencimiento.Year < 1900)
                throw new InvalidOperationException("Ingresa un stock mínimo positivo y una fecha de vencimiento válida.");
            datos.GuardarMedicamento(medicamento);
        }

        public List<MovimientoInventario> Movimientos(int id) { Exigir(Seccion.Farmacia); return datos.Movimientos(id); }

        public void RegistrarMovimiento(MovimientoInventario movimiento)
        {
            ExigirEdicion(Seccion.Farmacia);
            if (movimiento.IdMedicamento <= 0 || movimiento.Cantidad <= 0
                || (movimiento.Tipo != "Ingreso" && movimiento.Tipo != "Salida"))
                throw new InvalidOperationException("Selecciona un medicamento, tipo y cantidad positiva.");
            movimiento.Motivo = Texto(movimiento.Motivo, 1, 160, "El motivo");
            datos.RegistrarMovimiento(movimiento);
        }
    }
}
