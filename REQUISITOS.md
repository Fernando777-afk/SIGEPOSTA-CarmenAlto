# Requisitos de SIGEPOSTA

SIGEPOSTA es el Sistema de Gestión del Centro de Salud Carmen Alto, Ayacucho, Perú. Es un proyecto académico del curso de Programación Orientada a Objetos (EIS24-014) de la Escuela de Educación Superior Tecnológica La Pontificia. La referencia es el [enunciado del profesor, de seis páginas](Proyecto_Final_SIGEPOSTA_Ayacucho.pdf).

## Requisitos obligatorios del profesor

El alcance funcional del enunciado original (sección 3, p. 2) comprende gestionar las altas, actualizaciones y bajas de la red de postas, registrar personal de salud con especialidad y establecimiento asignado, registrar pacientes con una historia clínica única, gestionar citas con disponibilidad del personal, mantener una cola del día por consultorio, controlar entradas, salidas, stock mínimo y vencimientos de medicamentos, proteger DNI y diagnósticos, respaldar las historias clínicas, persistir la información con ADO.NET y emitir reportes de citas del día, pacientes atendidos y stock crítico. La aplicación de ese alcance a Carmen Alto se detalla más adelante.

### Seis unidades y evidencias

| Unidad | Aplicación en SIGEPOSTA | Evidencia para la sustentación |
| --- | --- | --- |
| 1. Programación orientada a objetos | Clases y objetos, encapsulamiento con validaciones, herencia, polimorfismo, constructores por defecto y parametrizados en las clases del modelo y sobrecarga de `Cita.Reprogramar` para cambiar fecha o consultorio. | Diagrama UML y código de la jerarquía. Mostrar `MostrarDatos` o `Atender` con implementaciones sobrescritas y una validación como DNI de ocho dígitos. |
| 2. Colecciones y enumeradores | `List<T>` para pacientes y citas, `Dictionary` con una agrupación útil en Carmen Alto, como citas por consultorio, LINQ para búsquedas y filtros y un enumerador para recorrer la cola por orden de llegada. | Listados, búsquedas y filtros de pacientes, citas y medicamentos con stock inferior al mínimo. |
| 3. Administración de datos | Exportación de una historia clínica o un reporte diario mediante `XmlSerializer`. Compresión con `GZipStream` y respaldo de historias clínicas comprimido y cifrado. | Archivo de exportación o respaldo generado por la aplicación y comprobación de que puede descomprimirse y leerse después del descifrado cuando corresponda. |
| 4. Seguridad de datos | Cifrado verificable de DNI y diagnóstico y almacenamiento local seguro de configuración, con `IsolatedStorage` cuando corresponda. | Datos protegidos en la base o en archivos, y lectura limitada a operaciones autorizadas. |
| 5. Procesos asíncronos | Actualización de la cola del día por consultorio en segundo plano con `BackgroundWorker` o un mecanismo compatible. La sección 5.5 del PDF también menciona el envío de recordatorios de cita como tarea asíncrona. | Windows Forms sigue respondiendo mientras se recalcula la cola y muestra el resultado al terminar. |
| 6. Acceso a datos | ADO.NET con `SqlConnection`, `SqlCommand`, `SqlDataReader` y `SqlDataAdapter` según el caso, CRUD parametrizado y transacciones en SQL Server. | Consultas con parámetros y registro atómico de una atención, su cita, el descuento de stock recetado y los movimientos respectivos. Comprobar la reversión si falla alguna operación. |

Fuente de esta matriz: secciones 4 y 5 del PDF (pp. 2–5). La tabla aplica al proyecto las decisiones tecnológicas acordadas.

### Modelo mínimo

- `Persona` abstracta reúne DNI, nombres, apellidos, fecha de nacimiento y teléfono. `Paciente` hereda de `Persona` y se vincula a su historia clínica. `PersonalSalud` también es abstracta y define el comportamiento común de atención. `Medico` y `Enfermero` heredan de ella con su especialidad o área. El registro de personal también contempla a técnicos y administrativos, según el enunciado.
- `Posta` representa el establecimiento fijo Carmen Alto. Conserva los datos del establecimiento que correspondan al modelo académico, como nombre, dirección, distrito, categoría y responsable, sin inventar datos oficiales.
- `Cita` vincula paciente, médico, fecha, hora, estado y consultorio. `HistoriaClinica` vincula número único, paciente y atenciones con diagnóstico protegido.
- `Medicamento` registra su identificación, nombre, existencia, mínimo y vencimiento según el esquema. `MovimientoInventario` vincula medicamento, tipo de ingreso o salida, cantidad y fecha.

Fuente del modelo: secciones 5.1 y 7 del PDF (pp. 3 y 6). Los nombres de atributos del modelo y los ejemplos del PDF sirven de referencia académica. El script principal definirá los identificadores SQL definitivos.

### Entregables académicos de la semana 16

La sustentación de proyecto (SP1) comprende el ejecutable de escritorio con las seis unidades integradas, el código fuente comentado y organizado por capas, el diagrama UML de la solución, un script SQL, un manual de usuario de hasta tres páginas con capturas y una sustentación oral individual de 10 a 15 minutos. El enunciado pide que el script cubra al menos `Posta`, el modelo `Persona` / `Paciente` / `PersonalSalud`, `Cita`, `Medicamento` y `MovimientoInventario` (p. 6).

## Adaptaciones definitivas para Carmen Alto

- Una sola sede. `Posta` representa un registro fijo de Carmen Alto. La historia clínica es única en el centro, el personal pertenece al establecimiento y los reportes e inventario corresponden a Carmen Alto. Las listas, filtros y agrupaciones operativas usan criterios como fecha, consultorio o stock. El mantenimiento de una red de postas y los reportes entre sedes quedan fuera del alcance.
- Plataforma definitiva. C#, Windows Forms, Visual Studio 2022, .NET Framework 4.8, SQL Server Express, SSMS y ADO.NET. Arquitectura por capas de Presentación, Negocio y Datos, con servicios de seguridad y respaldo. Esta plataforma actualiza la versión de .NET Framework del enunciado.
- Protección de datos. Usa AES con protección de integridad para el DNI, el diagnóstico y los respaldos que contengan historias clínicas. Guarda contraseñas mediante una función de derivación como PBKDF2 con sal aleatoria por contraseña. Mantén claves y secretos fuera del código y protege la configuración local. DES y SHA-1 del enunciado son referencias históricas. Evita datos sensibles en nombres de archivos, registros de errores y exportaciones sin protección.
- Archivos. Usa serialización XML y `GZipStream`. Para respaldos clínicos, serializa, comprime y cifra antes de guardar el archivo. Conserva la posibilidad de comprobar su lectura con datos ficticios.
- Base de datos. La instancia prevista es `DESKTOP-907MLR8\SQLEXPRESS` y la base nueva es `SIGEPOSTA_CarmenAlto_V2`. Un único script principal creará las tablas, relaciones, claves y restricciones de esa base y preservará las bases anteriores. Las consultas C# deben coincidir con los nombres y tipos del script. Comprueba especialmente teléfono, stock y vencimiento antes de escribir consultas o mapeos. Usa datos ficticios en las pruebas.

## Funciones acordadas para la aplicación

| Función | Alcance en Carmen Alto | Origen |
| --- | --- | --- |
| Acceso | Inicio de sesión y permisos de Administrador, Responsable, Admisión, Profesional de Salud, Farmacia y Estadística. La consulta clínica se limita a los pacientes asignados. | Ampliación acordada. |
| Inicio | Indicadores, calendario, gráfico de atenciones y paneles de citas, cola y alertas según el rol. | Ampliación acordada. |
| Pacientes | Registro, búsqueda, edición y consulta, con historia clínica única y validación de duplicados. | Base académica y detalle acordado de la gestión. |
| Personal de salud | Registro por tipo de personal, especialidad o área y horarios para determinar disponibilidad. | Base académica y gestión de horarios acordada. |
| Citas | Programación, reprogramación y cancelación con disponibilidad del personal. | Base académica. |
| Cola de atención | Cola del día por consultorio con actualización asíncrona. | Base académica. |
| Historias clínicas | Consulta de historias y registro de atenciones con triaje básico. | Base académica y triaje acordado. |
| Farmacia | Medicamentos con concentración, forma, unidad y lote, consulta de recetas, ingresos, salidas, stock crítico y vencimientos. Los lotes académicos se identifican como ejemplos. | Base académica y catálogo acordado. |
| Recetas | Recetas básicas vinculadas a atenciones, integradas al registro transaccional cuando corresponda descontar stock. | Ampliación acordada que integra la operación de la unidad 6. |
| Reportes | Citas del día, pacientes atendidos y stock crítico de Carmen Alto. | Base académica con alcance local. |
| Seguridad y respaldo | Protección de datos, almacenamiento local seguro y respaldo comprimido y cifrado. | Base académica con mecanismos actualizados. |

Las funciones pueden agruparse en menos pantallas. El envío de recordatorios se conserva como referencia de la sección 5.5 del PDF. Su canal de envío queda por definir si se incorpora al alcance. Facturación, pagos, seguros, hospitalización, laboratorio, telemedicina y otras funciones empresariales quedan fuera del alcance acordado.

El script principal prepara la base inicial. La migración 002 incorpora los perfiles y el catálogo académico en la base existente. La documentación de interfaz y el manual describen la versión de escritorio vigente.
