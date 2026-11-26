# SIGEPOSTA · Centro de Salud Carmen Alto

Aplicación de escritorio en C# y Windows Forms para el curso de Programación Orientada a Objetos (EIS24-014). Usa .NET Framework 4.8, ADO.NET y SQL Server Express con autenticación de Windows.

## Preparar el entorno y ejecutar

1. En Visual Studio 2022, instala **Desarrollo de escritorio de .NET** y el **paquete de destino de .NET Framework 4.8**. Ten disponible SQL Server Express y SQL Server Management Studio (SSMS).
2. En SSMS, conéctate a `DESKTOP-907MLR8\SQLEXPRESS` con **Autenticación de Windows**. Si la conexión falla, comprueba que el servicio **SQL Server (SQLEXPRESS)** esté iniciado y que el usuario de Windows tenga acceso. La instalación inicial requiere permiso para crear una base de datos.
3. Para una instalación nueva, ejecuta [SIGEPOSTA_CarmenAlto_V2.sql](SQL/SIGEPOSTA_CarmenAlto_V2.sql) y luego [002_Rediseno_Roles_Farmacia.sql](SQL/002_Rediseno_Roles_Farmacia.sql). En una base que ya tiene la migración 002, puedes abrir directamente la aplicación. La migración admite reejecución y conserva la información existente. [Revertir_002.sql](SQL/Revertir_002.sql) comprueba si se puede revertir sin perder asignaciones ni datos de farmacia.
4. Abre [SIGEPOSTA.sln](SIGEPOSTA.sln), establece **SIGEPOSTA.Presentacion** como proyecto de inicio y compila. Verifica la cadena en [App.config](Presentacion/App.config). Si tu instancia tiene otro nombre, actualiza **Data Source** y conserva **Initial Catalog=SIGEPOSTA_CarmenAlto_V2** e **Integrated Security=True**.
5. Presiona **F5**. En el primer acceso, elige el nombre de la cuenta administradora y una contraseña de 12 a 128 caracteres. Usa el perfil de Windows con el que configuraste la base. **Seguridad y respaldo** permite crear cuentas con los seis roles y vincular el médico de una cuenta de Profesional de Salud.
6. Para ejecutar la compilación de forma directa, abre `Presentacion\bin\Release\SIGEPOSTA.Presentacion.exe` y conserva en la misma carpeta sus DLL y su archivo `.exe.config`.

Si SSMS presenta un error de confianza del certificado en tu instancia local, revisa el certificado del servidor o marca **Confiar en el certificado del servidor** en las opciones de esa conexión local. Una cuenta SQL de instalación y una cuenta de Windows de operación pueden tener permisos distintos. Para operar, la cuenta de Windows necesita conectarse y leer y modificar las tablas de esta base.

## Recorrido para probar y sustentar

| Paso | Acción con datos ficticios | Resultado esperado |
| --- | --- | --- |
| 1 | Crea cuentas para los roles que vas a probar. Vincula un médico a la cuenta Profesional de Salud. | El menú muestra las secciones del rol. Las operaciones validan permisos en Negocio, incluida la asignación clínica. |
| 2 | Registra un paciente. Búscalo por DNI, nombre o número de historia. Edita el teléfono y abre la ficha. Repite el DNI al intentar otro registro. | El paciente conserva una historia única y el duplicado se rechaza. |
| 3 | Registra un médico y asígnale un horario que incluya dos turnos futuros de 30 minutos. Agenda una cita y reprográmala. Intenta ocupar el mismo médico, paciente o consultorio a la misma hora. Cancela otra cita de prueba. | La agenda muestra disponibilidad, impide cruces y libera el turno cancelado. |
| 4 | En una cita de hoy, registra la llegada. Abre **Cola de atención**, elige consultorio y actualiza. Con un rol clínico, registra triaje, diagnóstico y una receta. | La cola se actualiza en segundo plano. La cita pasa a atendida y la historia muestra el diagnóstico y la receta. |
| 5 | En Farmacia, registra un lote y su ingreso. Registra una salida y abre su historial. Para probar la reversión, prepara una receta con dos lotes, reduce el stock del segundo desde otro acceso antes de guardar y vuelve a la atención. | El movimiento válido actualiza el stock. Si falta stock en el segundo lote, la cita sigue en espera y el primero conserva su stock y sus movimientos. |
| 6 | Revisa **Citas del día**, **Pacientes atendidos** y **Stock crítico**. Exporta el reporte diario. | El panel y los reportes reflejan la agenda y el inventario. El archivo `.xml.gz` contiene los turnos sin nombres ni DNI. |
| 7 | Genera un respaldo `.sige` y verifícalo. Modifica una copia del archivo y vuelve a verificarla. | El respaldo original se puede leer con la configuración que lo creó. La copia alterada se rechaza por integridad. |

Consulta el [manual de usuario](Documentacion/Manual-de-usuario.pdf) y el [diagrama UML](Documentacion/Diagrama-UML.pdf).

## Arquitectura y evidencias

`Presentacion → Negocio → Datos → SQL Server`. Presentacion, Negocio y Datos usan Entidades para intercambiar datos. La capa de datos usa consultas parametrizadas con los identificadores del script y mantiene la misma conexión y transacción en las operaciones atómicas.

| Unidad | Código y demostración |
| --- | --- |
| 1 · POO | [Personas.cs](Entidades/Personas.cs) define la jerarquía, el DNI encapsulado y `MostrarDatos` polimórfico. [Atencion.cs](Entidades/Atencion.cs) contiene las dos sobrecargas de `Cita.Reprogramar`. |
| 2 · Colecciones | [CentroSalud.Agenda.cs](Negocio/CentroSalud.Agenda.cs) agrupa la cola con `Dictionary`, ordena con LINQ y recorre una `List<Cita>` con un enumerador basado en `yield`. Los módulos de pacientes y reportes usan filtros LINQ. |
| 3 · Archivos | [Archivos.cs](Negocio/Archivos.cs) serializa a XML y comprime con `GZipStream`. Para respaldo clínico aplica cifrado después de comprimir y verifica integridad antes de abrir. |
| 4 · Seguridad | [Proteccion.cs](Negocio/Proteccion.cs) usa AES-256 con IV aleatorio y HMAC-SHA-256. Un HMAC independiente permite validar DNI duplicados y búsquedas exactas. Usa PBKDF2-HMAC-SHA-256 con 600 000 iteraciones y sal aleatoria por contraseña. |
| 5 · Asincronía | [Principal.Agenda.cs](Presentacion/Principal.Agenda.cs) actualiza la cola con `Task.Run` y `await`. Bloquea refrescos duplicados y actualiza los controles desde el hilo de la interfaz. |
| 6 · ADO.NET | [Sql.cs](Datos/Sql.cs) usa `SqlConnection`, `SqlCommand`, `SqlDataReader` y `SqlDataAdapter`. [Almacen.Clinica.cs](Datos/Almacen.Clinica.cs) registra cita, atención, receta, stock y movimientos en una transacción. |

### Decisiones del proyecto

- **Sede.** `Posta` se carga desde un único registro de Carmen Alto. El número de historia se deriva de su identificador SQL. Los turnos duran 30 minutos y se comprueban contra los horarios del médico. Los reportes cuentan pacientes atendidos distintos por fecha de cita.
- **Roles.** Administrador accede a todas las secciones. Responsable supervisa personal, citas, cola y reportes. Admisión registra pacientes y citas y gestiona llegadas. Profesional de Salud consulta a sus pacientes asignados y registra triaje, atenciones y recetas. Farmacia consulta las recetas y gestiona lotes y movimientos. Estadística consulta indicadores y reportes. Todos pueden cambiar su contraseña desde el botón de su cuenta.
- **Clave de datos.** La aplicación de esta sede usa un perfil de Windows para el puesto de trabajo. DPAPI protege su clave en el almacenamiento aislado de SIGEPOSTA y SQL conserva únicamente una huella. Las compilaciones Debug y Release del mismo perfil comparten la clave. Conserva ese perfil para leer los datos cifrados y respaldos existentes. Los roles de aplicación controlan el uso de WinForms. El acceso directo en SSMS depende de permisos de Windows y SQL Server.
- **Respaldo.** El archivo clínico permite comprobar la lectura de las historias con su clave original. La restauración completa de la base corresponde a SQL Server. El envío externo de recordatorios queda pendiente de un canal acordado.
- **Sustentación.** En 10 a 15 minutos, explica el modelo y la sede única, demuestra el recorrido de paciente a receta y stock, verifica el respaldo y muestra las seis evidencias en el código. Cierra con la prueba de reversión de una receta con stock insuficiente.

## Catálogo académico de Farmacia

El catálogo 002 incluye paracetamol de 500 mg, loratadina de 10 mg, amoxicilina de 500 mg, ibuprofeno de 400 mg, metformina clorhidrato de 850 mg, omeprazol de 20 mg en tableta de liberación modificada, sales de rehidratación oral de 20,5 g/L, albendazol de 400 mg y salbutamol de 100 mcg por dosis en aerosol para inhalación. Las presentaciones se contrastaron con el [Petitorio Nacional Único de Medicamentos Esenciales de DIGEMID](https://www.digemid.minsa.gob.pe/Archivos/Normatividad/2023/ANEXO_RM_633-2023-MINSA.pdf).

La etiqueta **Ejemplo** identifica lotes con cantidades, mínimos y fechas simulados para la sustentación. El catálogo académico es ilustrativo y la unidad expresa cómo se cuentan las existencias en el sistema.
