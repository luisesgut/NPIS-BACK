# Decisiones de arquitectura (ADR) — Fase 0

Formato breve: Contexto → Decisión → Consecuencias. Numeradas para referencia cruzada desde otros documentos.

---

### ADR-001 — Plataforma: .NET 9 (STS) — **excepción documentada, reemplaza la elección original de .NET 10 LTS**
**Contexto:** en la Fase 0 (2026-08-12) esta máquina tenía instalados los SDKs 9.0.315 y 10.0.301; se eligió `net10.0` (LTS) por ser la versión par más reciente y la que ya usaba el cascarón `NpisBack`. Al iniciar la Fase 1 (mismo día, sesión posterior), se verificó de nuevo el entorno (`dotnet --list-sdks` vía Bash y PowerShell) y el SDK 10.0.301 **ya no está instalado** — sólo permanece 9.0.315. Se le preguntó al propietario cómo proceder (instalar .NET 10 vía `winget`, usar 9.0 STS temporalmente, o instalarlo él manualmente); el propietario eligió **continuar con .NET 9.0 STS por ahora**.
**Decisión:** el backend se construye sobre `net9.0` mientras el entorno no tenga un SDK LTS (8 o 10) instalado. Todos los proyectos de la solución (`Domain`, `Application`, `Infrastructure`, `Api` y los 3 de test) apuntan a `net9.0` de forma consistente — nunca se mezclan `TargetFramework` distintos entre proyectos de la misma solución.
**Consecuencias:**
- Esto es una **desviación explícita** del principio "usa la versión LTS disponible en el entorno" del pedido original, motivada por un cambio del entorno fuera de nuestro control, no por preferencia de diseño.
- .NET 9.0 sale de soporte STS (18 meses) antes que una LTS — se registra como **deuda técnica de plataforma**: migrar a `net10.0` (o `net8.0` si 10 no estuviera disponible) en cuanto el entorno tenga un SDK LTS instalado, lo cual sólo debería requerir cambiar `TargetFramework` en los 7 `.csproj` y volver a ejecutar `dotnet build`/`dotnet test` (el código de dominio no usa APIs específicas de una versión de .NET que compliquen ese cambio).
- No se instaló ningún SDK adicional en esta sesión (se descartó la opción de `winget install` para no modificar el sistema sin necesidad estricta, dado que el propietario prefirió avanzar con lo ya instalado).

---

### ADR-002 — Motor de base de datos: **SQL Server** (reemplaza PostgreSQL)
**Contexto:** en la Fase 0 original se propuso PostgreSQL/Npgsql (pedido inicial). El propietario indicó posteriormente que el motor definitivo es **Microsoft SQL Server**.
**Decisión:** se usa SQL Server como único motor soportado, en todos los entornos (incluido Testcontainers para integración). Se elimina cualquier referencia a Npgsql, sintaxis o tipos exclusivos de PostgreSQL de toda la documentación y del diseño.
**Consecuencias:**
- Paquete: `Microsoft.EntityFrameworkCore.SqlServer` (no `Npgsql.EntityFrameworkCore.PostgreSQL`).
- Migraciones EF Core generadas y aplicadas contra el proveedor SQL Server (`dotnet ef migrations add` con el `DbContext` apuntando al proveedor SQL Server) — no son portables 1:1 a Postgres ni viceversa.
- Cadenas de conexión SQL Server (`Server=...;Database=...;...`) gestionadas por configuración/`User Secrets`/variables de entorno — nunca commiteadas (ver `.env.example`).
- Desarrollo local: SQL Server en contenedor Docker (`mcr.microsoft.com/mssql/server`), instalación local, o **LocalDB** (`(localdb)\MSSQLLocalDB`) — cualquiera de las tres, a elección del desarrollador; nunca se apunta a una base compartida/externa.
- Pruebas de integración: Testcontainers con imagen `mcr.microsoft.com/mssql/server` si Docker está disponible; si no, esas pruebas se marcan como omitidas explícitamente (no se usa InMemory como sustituto de integración real).
- Numeración de NPI, concurrencia optimista y cualquier mecanismo transaccional se rediseñan en términos nativos de SQL Server (ver ADR-004 y ADR-005) — **no** se usa `SELECT ... FOR UPDATE` (sintaxis Postgres); el equivalente en SQL Server es un `SEQUENCE` o bloqueo pesimista explícito con `UPDLOCK, HOLDLOCK` dentro de una transacción `SERIALIZABLE`/`READ COMMITTED` según el mecanismo elegido.

---

### ADR-003 — ORM: EF Core + proveedor SQL Server
**Decisión:** Entity Framework Core con `Microsoft.EntityFrameworkCore.SqlServer`. Configuraciones vía `IEntityTypeConfiguration<T>` (una clase por entidad), no *data annotations* dispersas. `DbContext` vive en `Infrastructure`, nunca referenciado desde `Domain` ni `Application` (que sólo ven interfaces de repositorio).
**Consecuencias:** tipos de columna explícitos en cada configuración (ver ADR siguiente sobre tipos), longitudes máximas para todo `nvarchar` (nunca `nvarchar(max)` salvo campos de texto libre largo como `Observaciones`, `Description` de comentarios/solicitudes), `datetimeoffset` para toda fecha (nunca `datetime`/`datetime2` sin offset), `uniqueidentifier` para todos los `Id` (`Guid`, generados en aplicación con `Guid.NewGuid()` o `Guid.CreateVersion7()` si se prioriza orden de inserción — decisión de detalle en Fase 2).

---

### ADR-004 — Concurrencia optimista: columna `rowversion`
**Contexto:** el pedido exige concurrencia optimista en `Npi`; SQL Server ofrece el tipo nativo `rowversion` (alias `timestamp`), auto-incrementado por el motor en cada `UPDATE`, ideal para este propósito (no requiere gestionar un contador manual como sí sería necesario en Postgres con `xmin` o una columna de versión propia).
**Decisión:** `Npi.RowVersion` se mapea como `rowversion` vía `.IsRowVersion()` de EF Core. Toda actualización del agregado incluye el valor leído; un `DbUpdateConcurrencyException` se traduce a `409 Conflict` con `ProblemDetails.type="concurrency-conflict"`.
**Consecuencias:** no se requiere columna de versión manual ni comparación de timestamps para detectar conflictos; el motor lo hace nativamente.

---

### ADR-005 — Numeración atómica de NPI: `SEQUENCE` de SQL Server
**Contexto:** el frontend generaba `NPI-000X` con `max(existente)+1` sin ningún bloqueo — condición de carrera explícitamente prohibida en el pedido. Debe reemplazarse por un mecanismo transaccional seguro, sin usar `SELECT ... FOR UPDATE` (Postgres) ni ninguna sintaxis específica de otro motor.
**Decisión:** se crea un objeto `SEQUENCE` de SQL Server (`CREATE SEQUENCE dbo.NpiNumberSequence AS int START WITH 1 INCREMENT BY 1`). La acción `submit` (Draft→Active) obtiene el siguiente valor con `NEXT VALUE FOR dbo.NpiNumberSequence` dentro de la misma transacción que persiste el `Npi`, y formatea el número en la capa de aplicación como `NPI-{valor:0000}`. Se agrega **restricción/índice único** (`UNIQUE INDEX`) sobre `Npi.NpiNumber` como salvaguarda adicional (defensa en profundidad: aunque `SEQUENCE` ya garantiza unicidad del entero, el índice único protege contra errores de formateo o inserciones manuales de seed).
**Alternativa considerada y descartada por ahora:** tabla de folios con bloqueo pesimista explícito (`UPDLOCK, HOLDLOCK` + transacción) — más control fino, pero más compleja de mantener que un `SEQUENCE` nativo para un caso de uso simple (un solo contador). Se documenta como alternativa válida si en el futuro se necesitaran secuencias independientes por tipo/año de NPI.
**Consecuencias:**
- Los NPIs semilla de desarrollo (`NPI-1000` en adelante, igual que hoy en `seedNpis.ts`) se insertan con número explícito, **sin** pasar por el `SEQUENCE`, y el `SEQUENCE` de cada base de datos de desarrollo se reinicia (`ALTER SEQUENCE ... RESTART WITH 1`) de forma independiente al rango reservado para semillas, para que la numeración "real" (`NPI-0001…`) nunca choque con las semillas — mismo criterio que ya aplica el frontend, ahora garantizado por el propio arranque del `SEQUENCE` en cada entorno de desarrollo en vez de por una regla de cliente.
- El número se asigna en `submit`, no en la creación del borrador (un borrador `Draft` no tiene `NpiNumber` todavía, o tiene uno reservado no expuesto como definitivo — decisión de detalle en Fase 2 según si se prefiere reservar el número al crear o al enviar; por defecto se asigna al enviar, para no "quemar" números de borradores nunca enviados).

---

### ADR-006 — Factibilidades de Ingeniería y Diseño: paralelas e independientes (decisión 1)
**Decisión:** ninguna de las dos `FeasibilityReview` depende de que la otra exista o esté aprobada. Se confirma el comportamiento del código fuente actual (`canStartIngenieriaFeasibility()` siempre `true`) sobre la narrativa histórica del README y del script E2E, que reflejaban una regla secuencial descontinuada.
**Consecuencias:** `docs/domain-model.md` invariante 3, `docs/workflow.md` acciones 7–8. El texto de bloqueo aún presente en el frontend actual (inalcanzable en la práctica) no se replica en el backend.

---

### ADR-007 — Precondiciones para crear una versión de PrintCard (decisión 2)
**Decisión:** crear `PrintCardVersion` exige las **cuatro** condiciones: diseñador asignado, factibilidad de Ingeniería aprobada, factibilidad de Diseño aprobada y Artes Validadas. Esto es **más estricto** que el frontend actual (que sólo exigía factibilidad de Ingeniería + asignación a Diseño).
**Consecuencias:** cambio de comportamiento deliberado respecto al frontend — se documenta explícitamente para que, cuando se actualice el frontend (fuera de alcance de este proyecto por ahora), sepa que debe alinear su UI a esta regla más estricta antes de intentar la llamada, o mostrar los 4 candados de forma proactiva. `docs/domain-model.md` invariante 2, `docs/workflow.md` acción 12.

---

### ADR-008 — "PrintCard cargado en Sistema" no es terminal (decisión 3)
**Decisión:** esta acción sólo cierra `DesignTimer`; no cambia `LifecycleStatus` ni cierra `GlobalTimer`. El ciclo permanece `Active`.
**Consecuencias:** corrige la lectura implícita del frontend (`isCicloCerrado = printCardLoadedAt && etSisproAt`, que ya insinuaba esto, pero sin una regla explícita de qué cierra cada timer). `docs/workflow.md` acción 17.

---

### ADR-009 — Condición de cierre exitoso del ciclo (decisión 4)
**Decisión:** `Active → Completed` requiere simultáneamente: PrintCard cargado, PT registrado en SAP, Pedido SAP registrado y ET cargada en SisPro. Se evalúa automáticamente al completarse el último de los cuatro (en la práctica, `load-et-sispro`, por ser el que depende transitivamente de los otros tres).
**Consecuencias:** `docs/domain-model.md` §4.2, `docs/workflow.md` acción 21.

---

### ADR-010 — El cronómetro global sólo cierra en éxito o rechazo terminal (decisión 5)
**Decisión:** se elimina la lógica de cierre en cascada que existía en el frontend (`NPIContext.updateNPI` cerraba `GlobalTimer` cada vez que Ingeniería+Diseño quedaban cerrados, en cualquier acción). En el backend, `GlobalTimer.Close()` sólo se invoca desde `CompleteCycle()` (ADR-009) o desde cualquiera de las tres transiciones de rechazo terminal (ADR-011).
**Excepción documentada (no pedida explícitamente, aplicada por higiene de datos):** la acción administrativa `cancel` (ADR-024) también cierra los timers abiertos, para no dejar cronómetros corriendo indefinidamente sobre un NPI cancelado y así no distorsionar promedios/KPIs. Se marca como recomendación aplicada, sujeta a confirmación del propietario (ver `assumptions-and-ambiguities.md`).
**Consecuencias:** `docs/domain-model.md` invariante 7.

---

### ADR-011 — Catálogo cerrado de rechazos terminales (decisión 6)
**Decisión:** `TerminalReason ∈ { NoFeasibility, SuppliesRejectionTotal, ClientRejection }` (más `Cancelled` como `LifecycleStatus` propio, no como `TerminalReason` de `Rejected` — ver ADR-015). No existen otros caminos a `Rejected`.
**Consecuencias:** corrige el hallazgo de `TERMINAL_STATUSES` incompleto del frontend (`assumptions-and-ambiguities.md` B3).

---

### ADR-012 — Nueva acción: rechazo definitivo del cliente (decisión 7)
**Decisión:** se añade la acción `reject-client` (ausente en el frontend actual, aunque el tipo `NPIStatus.rechazado_cliente` ya la anticipaba sin implementación — hallazgo B2 de `assumptions-and-ambiguities.md`). Actor: Comercial. `Comment` obligatorio, evidencia opcional.
**Consecuencias:** `docs/workflow.md` acción 16; cierra el hueco funcional entre lo documentado (README: "Aceptado... o bien Rechazado por el Cliente") y lo implementado.

---

### ADR-013 — El cliente no es un actor autenticado del sistema (decisión 8)
**Decisión:** toda acción relacionada con la decisión del cliente (aprobar PrintCard, solicitar cambios, rechazo definitivo) la ejecuta siempre un usuario `Comercial` (propio/asignado o admin), nunca un rol "Cliente". No se modela `Role="Cliente"` en Identity.
**Consecuencias:** simplifica la matriz de autorización (no hay una octava fila de rol); deja una nota de extensibilidad futura en §5 de este documento por si el negocio pide un portal de cliente más adelante (fuera de alcance actual).

---

### ADR-014 — ET Creada y ET cargada en SisPro como hitos separados (decisión 9)
**Decisión:** `EtCreatedAt` y `EtLoadedInSisproAt` son columnas y acciones (`register-et`, `load-et-sispro`) independientes, aunque el frontend actual las registre en un mismo clic (`handleETCreada` hace ambas cosas a la vez). `load-et-sispro` requiere `EtCreatedAt != null`.
**Consecuencias:** `docs/domain-model.md` invariante 8; añade una acción más al catálogo de `docs/workflow.md` (acción 20 vs. 21) respecto al conteo original de endpoints sugeridos.

---

### ADR-015 — `LifecycleStatus` de alto nivel + estados por área (decisión 10)
**Decisión:** `Npi.LifecycleStatus ∈ { Draft, Active, Completed, Rejected, Cancelled }` es la única fuente de verdad sobre "en qué fase macro está el NPI". El detalle operativo (a qué usuario está asignado, si está en revisión, si se solicitaron cambios, etc.) se representa con `EngineeringAreaStatus`/`DesignAreaStatus` (enums de presentación) + hitos con fecha + `AuditEvent`/`TimerEvent`, nunca con un enum global monolítico reutilizado para significados distintos (el defecto detectado en `NPIStatus.producto_en_sap` del frontend, hallazgo B4).
**Consecuencias:** `docs/domain-model.md` §4.

---

### ADR-016 — Visibilidad simétrica y restringida para usuarios normales de Ingeniería y Diseño (decisión 11)
**Decisión:** `UsuarioIngenieria` y `UsuarioDiseno` sólo ven/operan NPIs asignados a ellos; los no asignados sólo son visibles para el admin de esa área y `AdminTotal`. Se aplica igual en ambas áreas (el frontend tenía la regla asimétrica descrita en `assumptions-and-ambiguities.md` B1 — Diseño exponía además lo no asignado).
**Consecuencias:** `docs/authorization-matrix.md` completo; se aplica también a superficies que en el frontend no filtraban en absoluto (Dashboard general — ver `authorization-matrix.md` §1).

---

### ADR-017 — No se usa MediatR
**Contexto:** el pedido pide CQRS pragmático, evitando abstracciones ceremoniales.
**Decisión:** los casos de uso de `Application` se implementan como clases de servicio/comando directas (`IRequestHandler`-like manual, sin librería de mediador), inyectadas por interfaz. Cada acción de `docs/workflow.md` corresponde a una clase `XxxCommandHandler` con un método `Handle(command, CancellationToken)`.
**Consecuencias:** menos indirección para 29 acciones bien definidas; se reevaluará si en el futuro aparecen *cross-cutting concerns* (logging, validación, transacciones) que un pipeline de MediatR simplificaría más que un decorator/filtro manual — no es el caso hoy.

---

### ADR-018 — Abstracciones de infraestructura obligatorias
**Decisión:** `IClock` (reloj inyectable, sin `DateTime.UtcNow` directo en `Domain`), `IBusinessCalendar` (horario hábil MX + festivos, hoy hardcodeados hasta 2030 — se deja la puerta abierta a moverlos a una tabla `PublicHoliday` administrable sin romper la interfaz), `IFileStorage` (implementación local en Development, reemplazable por S3/Azure Blob/MinIO), `IQuotationProvider` (implementación local por defecto sobre la tabla `Quotation`, reemplazable por integración externa), `INotificationService` (tabla `Notification` + stub de email que loguea en Development).
**Consecuencias:** dominio 100% testeable de forma determinista (fechas y festivos inyectados en los tests).

---

### ADR-019 — Autenticación: ASP.NET Identity + JWT con refresh rotativo
**Decisión:** Identity para hashing de contraseñas y bloqueo por intentos fallidos; JWT de acceso de corta duración; refresh token aleatorio almacenado **hasheado** (nunca en texto plano) en `RefreshToken`, con rotación (cada uso invalida el anterior y emite uno nuevo) y revocación explícita en logout. Los 7 roles funcionales son roles nativos de Identity.
**Consecuencias:** reemplaza por completo la autenticación 100% cliente actual (hallazgo B8).

---

### ADR-020 — Catálogos administrables en base de datos vs. enums de dominio
**Decisión:** los 19 catálogos de opciones de negocio (`Catalog`/`CatalogOption`) van en base de datos porque cambian sin tocar código. Los enums que gobiernan lógica de transición (`LifecycleStatus`, `TerminalReason`, `EngineeringAreaStatus`, `DesignAreaStatus`, `AreaType`, `AttachmentContext`, `TimerStage/Action`, roles) permanecen como enums de dominio/Identity porque el código depende de sus valores específicos para decidir comportamiento.
**Consecuencias:** `docs/domain-model.md` §9.

---

### ADR-021 — Adjuntos: metadatos en BD, binario fuera de la fila
**Decisión:** `IFileStorage` guarda el binario (disco local fuera del código fuente en Development, con límites de tamaño/tipo configurables y validación de firma de archivo, no sólo extensión/Content-Type declarado por el cliente); la tabla `Attachment` sólo guarda metadatos + `StorageKey`. Nombres de archivo saneados; `StorageKey` generado por el servidor (nunca el nombre de archivo del cliente usado directamente como ruta) para evitar *path traversal*.
**Consecuencias:** corrige el antipatrón base64-en-fila del frontend (hallazgo B7).

---

### ADR-022 — Observabilidad y calidad
**Decisión:** Serilog (structured logging, sin registrar tokens/contraseñas/datos sensibles), FluentValidation para todos los DTOs de entrada, `ProblemDetails`/`ValidationProblemDetails` en todas las respuestas de error, Swagger/OpenAPI con esquema `Bearer` configurado, health checks (`/health`, `/health/ready` con verificación de conexión SQL Server).
**Consecuencias:** ninguna especial más allá de lo estándar.

---

### ADR-023 — Estrategia de pruebas
**Decisión:** xUnit + FluentAssertions en los tres niveles. Integración con **Testcontainers usando una imagen de SQL Server** (`mcr.microsoft.com/mssql/server`) si Docker está disponible en el entorno de ejecución de pruebas; si no lo está, esas pruebas se omiten explícitamente (skip razonado, no se sustituyen por EF InMemory, que no reproduce restricciones/relaciones reales de SQL Server).
**Consecuencias:** ninguna prueba de integración depende de una base de datos externa/compartida; cada corrida levanta y destruye su propio contenedor.

---

### ADR-024 — Acción administrativa "Cancelar NPI" (soporte a `LifecycleStatus.Cancelled`)
**Contexto:** la decisión 10 pide mantener `Cancelled` en el enum de alto nivel, pero ninguna acción del frontend ni de la lista original de endpoints sugeridos lo produce.
**Decisión:** se añade una acción mínima `cancel` (Draft o Active → Cancelled), reservada a `AdminComercial`/`AdminTotal`, con `reason` obligatorio, para que el valor del enum sea alcanzable.
**Consecuencias:** es una adición no solicitada explícitamente por ninguna de las 11 decisiones ni por el catálogo de endpoints original — se documenta como tal y se registra en `assumptions-and-ambiguities.md` como punto no bloqueante sujeto a confirmación/ajuste del propietario (¿quién más debería poder cancelar? ¿aplica también sobre NPIs con factibilidades ya aprobadas, o sólo en etapas tempranas?).

---

## Resumen de decisiones tomadas en esta ronda (para referencia rápida)

1. SQL Server reemplaza a PostgreSQL en toda la arquitectura (ADR-002 a ADR-005).
2. Las 11 decisiones de negocio del propietario quedan formalizadas en ADR-006 a ADR-016 y propagadas a `domain-model.md`, `workflow.md` y `authorization-matrix.md`.
3. Se mantiene sin cambios lo ya decidido en la ronda anterior: .NET 10 LTS, Identity+JWT, FluentValidation, Serilog, Swagger, health checks, xUnit+FluentAssertions+Testcontainers, sin MediatR, abstracciones `IClock`/`IBusinessCalendar`/`IFileStorage`/`IQuotationProvider`/`INotificationService`.
