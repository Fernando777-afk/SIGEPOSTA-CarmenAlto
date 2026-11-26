# Diseño de la interfaz de SIGEPOSTA

SIGEPOSTA utiliza componentes propios de Windows Forms y tipografía Segoe UI. La identidad azul se aplica al acceso, a los módulos y a los formularios.

| Color | Valor | Uso |
| --- | --- | --- |
| Blanco | `#FFFFFF` | Tarjetas, formularios, tablas y paneles de contenido. |
| Azul muy claro | `#EDF6FF` | Fondo, encabezados de tabla y estados de foco o selección ligera. |
| Azul suave | `#B7D7F0` | Selección, distintivos e iconos sobre el menú. |
| Azul medio | `#5A9BD5` | Trazos del gráfico, detalles e indicadores del foco. |
| Azul oscuro | `#1F3F66` | Menú lateral, títulos, texto principal y botones primarios. |

Los mensajes de error, advertencia y éxito incluyen texto. Sus colores auxiliares facilitan el reconocimiento.

## Acceso y navegación

- El acceso conserva una tarjeta centrada con la identificación del establecimiento a la izquierda y los campos a la derecha. El mismo diseño se usa en la primera configuración, con confirmación de contraseña. Los campos redondeados tienen iconos y la contraseña se puede mostrar u ocultar.
- La ventana principal abre maximizada dentro del área de trabajo de Windows. El encabezado alinea el nombre del sistema, el centro de salud, la fecha, la cuenta y el rol. El menú lateral muestra iconos, la sección activa y los módulos permitidos.
- Inicio muestra cuatro indicadores, atenciones de los últimos siete días, un calendario y los paneles operativos que correspondan al rol. Seleccionar un día del calendario permite consultar las citas de esa fecha. Los valores y estados vacíos provienen de las consultas de la aplicación.

## Componentes y distribución

- `BotonCurvo`, `Superficie`, `Vista` y `Dialogo` comparten radios, foco, márgenes, botones y mensajes. Los modales mantienen el título y las acciones fuera del área desplazable de campos.
- `SelectorFecha` permite escribir una fecha o elegirla en el calendario desplegable. Admite **Alt + Flecha abajo** y **F4**. `CalendarioAzul` se usa también en Inicio. Los horarios usan listas de hora y minuto.
- `ListaAzul` mantiene el comportamiento del selector nativo y adapta el ancho desplegado a sus opciones. Las pestañas de Personal y Farmacia admiten **Ctrl + Tab**.
- Las tablas separan filas y permiten envolver los textos. Farmacia reserva ancho para el nombre, la concentración, la forma farmacéutica, el lote, el vencimiento y el estado. En ventanas estrechas se puede desplazar la tabla horizontalmente.
- Los indicadores y las acciones ocupan filas distintas. Se utilizan `Dock`, `Anchor`, `TableLayoutPanel` y `FlowLayoutPanel` para que el contenido se ajuste al área disponible. En resoluciones reducidas, Inicio y los modales permiten desplazamiento para llegar al contenido completo.
