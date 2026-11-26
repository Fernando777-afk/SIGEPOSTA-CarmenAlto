using System;

namespace SIGEPOSTA.Entidades
{
    public sealed class Medicamento
    {
        public Medicamento() { }
        public Medicamento(string nombre, string lote, int stockMinimo, DateTime vencimiento)
        {
            Nombre = nombre;
            Lote = lote;
            StockMinimo = stockMinimo;
            FechaVencimiento = vencimiento.Date;
        }

        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Concentracion { get; set; }
        public string FormaFarmaceutica { get; set; }
        public string UnidadMedida { get; set; }
        public string Lote { get; set; }
        public bool EsEjemplo { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Presentacion { get { return string.IsNullOrWhiteSpace(Concentracion) ? Nombre : Nombre + " " + Concentracion; } }
        public string LoteVisible { get { return EsEjemplo ? Lote + " · Ejemplo" : Lote; } }
        public string NombreLote { get { return Presentacion + " · " + LoteVisible; } }
        public bool StockCritico { get { return StockActual < StockMinimo; } }
        public string Alerta
        {
            get
            {
                string stock = StockCritico ? "Stock crítico" : "";
                int dias = (FechaVencimiento.Date - DateTime.Today).Days;
                string vencimiento = dias < 0 ? "Vencido" : dias <= 30 ? "Vence en " + dias + " días" : "";
                if (stock.Length == 0 && vencimiento.Length == 0) return "Sin alertas";
                return string.Join(" · ", new[] { stock, vencimiento }, 0, 2).Trim(' ', '·');
            }
        }
    }

    public sealed class RecetaFarmacia
    {
        public int Atencion { get; set; }
        public DateTime Fecha { get; set; }
        public string Paciente { get; set; }
        public string Medicamento { get; set; }
        public int Cantidad { get; set; }
        public string Indicaciones { get; set; }
    }

    public sealed class MovimientoInventario
    {
        public MovimientoInventario() { }
        public MovimientoInventario(int medicamento, string tipo, int cantidad, string motivo)
        {
            IdMedicamento = medicamento; Tipo = tipo; Cantidad = cantidad; Motivo = motivo;
        }
        public int Id { get; set; }
        public int IdMedicamento { get; set; }
        public string Medicamento { get; set; }
        public string Tipo { get; set; }
        public int Cantidad { get; set; }
        public string Motivo { get; set; }
        public DateTime Fecha { get; set; }
    }
}
