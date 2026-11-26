using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    internal sealed class Acceso : Form
    {
        private readonly CentroSalud centro;
        private readonly TextBox usuario = Ui.Campo(40);
        private readonly TextBox clave = Ui.Campo(128, privado: true);
        private readonly TextBox confirmacion = Ui.Campo(128, privado: true);
        private readonly Label subtitulo = Ui.Etiqueta("Iniciar sesión", Ui.Titulo);
        private readonly Label textoConfirmacion = Ui.Etiqueta("Confirmar contraseña", Ui.Negrita);
        private readonly Label mensaje = Ui.Etiqueta("", Ui.Auxiliar);
        private readonly Button ingresar = Ui.Boton("Iniciar sesión", null, true);
        private readonly Button verClave = Ui.Boton("Mostrar contraseña", icono: Glifo.Ojo);
        private readonly Control campoConfirmacion;
        private bool listo;
        private bool configuracion;

        public Acceso(CentroSalud centro)
        {
            this.centro = centro;
            Text = "SIGEPOSTA · Acceso";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Texto;
            ClientSize = new Size(1000, 600);
            MinimumSize = new Size(785, 540);
            BackColor = Ui.AzulMuyClaro;

            var fondo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Padding = new Padding(22) };
            fondo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fondo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 830));
            fondo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fondo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fondo.RowStyles.Add(new RowStyle(SizeType.Absolute, 498));
            fondo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            fondo.SizeChanged += (s, e) =>
            {
                fondo.ColumnStyles[1].Width = Math.Min(Px(860), Math.Max(Px(615), fondo.ClientSize.Width - Px(52)));
                fondo.RowStyles[1].Height = Math.Min(Px(520), Math.Max(Px(425), fondo.ClientSize.Height - Px(48)));
            };

            var tarjeta = new Superficie { Dock = DockStyle.Fill, Padding = new Padding(12), Radio = 24, Linea = Ui.Linea };
            var cuerpo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Ui.Blanco
            };
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            cuerpo.Controls.Add(Marca(), 0, 0);

            var zona = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.Blanco, Padding = new Padding(29, 22, 20, 14) };
            var formulario = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, BackColor = Ui.Blanco };
            formulario.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            subtitulo.Margin = new Padding(0, 6, 0, 18);
            formulario.Controls.Add(subtitulo);
            Campo(formulario, Ui.Etiqueta("Usuario", Ui.Negrita), usuario, Glifo.Usuario);
            Campo(formulario, Ui.Etiqueta("Contraseña", Ui.Negrita), clave, Glifo.Seguridad);
            campoConfirmacion = Campo(formulario, textoConfirmacion, confirmacion, Glifo.Seguridad);
            verClave.Margin = new Padding(0, 2, 0, 9);
            formulario.Controls.Add(verClave);
            mensaje.MaximumSize = new Size(330, 0);
            mensaje.Margin = new Padding(0, 2, 0, 8);
            formulario.Controls.Add(mensaje);
            ingresar.Dock = DockStyle.Fill;
            ingresar.MinimumSize = new Size(84, 43);
            ingresar.Margin = new Padding(0);
            formulario.Controls.Add(ingresar);
            zona.Controls.Add(formulario);
            void CentrarFormulario()
            {
                int arriba = Math.Max(Px(17), (zona.ClientSize.Height - formulario.Height) / 2);
                if (zona.Padding.Top != arriba) zona.Padding = new Padding(Px(29), arriba, Px(20), Px(14));
            }
            zona.SizeChanged += (s, e) => CentrarFormulario();
            formulario.SizeChanged += (s, e) => CentrarFormulario();
            cuerpo.Controls.Add(zona, 1, 0);
            tarjeta.Controls.Add(cuerpo);
            fondo.Controls.Add(tarjeta, 1, 1);
            Controls.Add(fondo);
            AcceptButton = ingresar;

            verClave.Click += (s, e) =>
            {
                bool mostrar = clave.UseSystemPasswordChar;
                clave.UseSystemPasswordChar = !mostrar;
                if (configuracion) confirmacion.UseSystemPasswordChar = !mostrar;
                verClave.Text = mostrar ? "Ocultar contraseña" : "Mostrar contraseña";
                clave.Focus();
            };
            ingresar.Click += async (s, e) =>
            {
                if (listo) await Autenticar();
                else await Verificar();
            };
            Shown += async (s, e) => await Verificar();
            ingresar.Enabled = false;
            SetCampos(false);
        }

        private int Px(int medida) { return (int)Math.Round(medida * DeviceDpi / 96.0); }

        private static Control Marca()
        {
            var panel = new Superficie
            {
                Dock = DockStyle.Fill, BackColor = Ui.AzulOscuro, Linea = Color.Transparent, Radio = 19,
                Padding = new Padding(23), Margin = Padding.Empty
            };
            var centro = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Ui.AzulOscuro };
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            centro.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            centro.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            var bloque = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Margin = Padding.Empty, BackColor = Ui.AzulOscuro
            };
            var icono = new Distintivo(Glifo.Salud, Ui.AzulSuave, Ui.AzulOscuro) { Size = new Size(56, 56), Margin = new Padding(0, 0, 0, 18) };
            Label marca = Ui.Etiqueta("SIGEPOSTA", Ui.Titulo);
            marca.ForeColor = Ui.Blanco;
            marca.Margin = new Padding(0, 0, 0, 7);
            Label sede = Ui.Etiqueta("Centro de Salud\nCarmen Alto", Ui.Subtitulo);
            sede.ForeColor = Ui.MenuTexto;
            sede.Margin = new Padding(0, 0, 0, 13);
            Label ciudad = Ui.Etiqueta("Ayacucho, Perú", Ui.Auxiliar);
            ciudad.ForeColor = Ui.MenuIcono;
            ciudad.Margin = Padding.Empty;
            bloque.Controls.AddRange(new Control[] { icono, marca, sede, ciudad });
            centro.Controls.Add(bloque, 0, 1);
            panel.Controls.Add(centro);
            return panel;
        }

        private static Control Campo(TableLayoutPanel formulario, Label etiqueta, TextBox caja, Glifo icono)
        {
            etiqueta.Margin = new Padding(1, 0, 0, 2);
            formulario.Controls.Add(etiqueta);
            caja.AccessibleName = etiqueta.Text;
            Control marco = Ui.Enmarcar(caja, icono);
            marco.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            marco.Margin = new Padding(0, 0, 0, 9);
            formulario.Controls.Add(marco);
            return marco;
        }

        private void SetCampos(bool activo)
        {
            usuario.Enabled = clave.Enabled = confirmacion.Enabled = verClave.Enabled = activo;
            textoConfirmacion.Visible = campoConfirmacion.Visible = configuracion;
        }

        private async Task Verificar()
        {
            ingresar.Enabled = false;
            mensaje.ForeColor = Ui.AzulOscuro;
            mensaje.Text = "Verificando conexión…";
            try
            {
                configuracion = await Task.Run(() => centro.RequiereConfiguracion());
                if (IsDisposed) return;
                listo = true;
                SetCampos(true);
                subtitulo.Text = configuracion ? "Configurar acceso" : "Iniciar sesión";
                ingresar.Text = configuracion ? "Crear cuenta administradora" : "Iniciar sesión";
                mensaje.Text = configuracion ? "Configura la primera cuenta. Usa una contraseña de al menos 12 caracteres." : "";
                usuario.Focus();
            }
            catch (Exception error)
            {
                if (IsDisposed) return;
                mensaje.ForeColor = Ui.Error;
                mensaje.Text = CentroSalud.Mensaje(error);
                ingresar.Text = "Reintentar conexión";
                listo = false;
            }
            finally { if (!IsDisposed) ingresar.Enabled = true; }
        }

        private async Task Autenticar()
        {
            string nombre = usuario.Text;
            string contraseña = clave.Text;
            string segunda = confirmacion.Text;
            ingresar.Enabled = false;
            SetCampos(false);
            mensaje.ForeColor = Ui.AzulOscuro;
            mensaje.Text = "Verificando acceso…";
            try
            {
                if (configuracion)
                {
                    Ui.ClavesIguales(contraseña, segunda);
                    await Task.Run(() => centro.Configurar(nombre, contraseña));
                    configuracion = false;
                    ingresar.Text = "Iniciar sesión";
                }
                await Task.Run(() => centro.Iniciar(nombre, contraseña));
                if (IsDisposed) return;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error)
            {
                if (IsDisposed) return;
                clave.Clear();
                if (configuracion) confirmacion.Clear();
                mensaje.ForeColor = Ui.Error;
                mensaje.Text = CentroSalud.Mensaje(error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    ingresar.Enabled = true;
                    SetCampos(true);
                }
            }
        }
    }
}
