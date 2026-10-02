# Análisis del Frontend — Sistema de Gestión de NPIs (Fase 0)

> Estado: **descubrimiento completo, sin implementación de backend**. Este documento resume el comportamiento **efectivo** del frontend (código ejecutado), contrastado contra el README y el plan de ajustes interno (`.lovable/plan.md`), como base para diseñar el backend ASP.NET Core.
>
> **Actualizado el 2026-08-12** con las decisiones formales del propietario: 11 reglas de negocio/arquitectura (ver `architecture-decisions.md` ADR-006 a ADR-016, aplicadas en `domain-model.md`, `workflow.md` y `authorization-matrix.md`) y el cambio de motor de base de datos de PostgreSQL a **SQL Server** (ADR-002 a ADR-005). Las secciones §1 y §8 de este documento ya reflejan SQL Server; el resto del análisis del frontend (secciones 2–7, 9–13) no cambia, ya que describe comportamiento observado, independiente del motor de base de datos elegido.

## 1. Rutas y stack detectados

| Elemento | Valor |
|---|---|
| Frontend | `C:\Users\lespinosa\Desktop\NpisOG\Npis-Front\npi-launchpad` — React 18.3 + TypeScript + Vite 5, shadcn/ui (Radix), Tailwind, React Router 6, TanStack Query 5 (no usado para datos reales aún), Recharts, Vitest, Playwright (script manual en `e2e/`). |
| Backend (cascarón actual) | `C:\Users\lespinosa\Desktop\NpisOG\NpisBack\NpisBack\NpisBack` (nota: la carpeta contenedora tiene un nivel extra `NpisBack\NpisBack\` antes de llegar al `.sln`). Plantilla por defecto de Visual Studio: `Microsoft.NET.Sdk.Web`, `net10.0`, un `WeatherForecastController` de ejemplo. Sin lógica de negocio. |
| .NET instalado | SDKs `9.0.315` y `10.0.301`. **.NET 10 es la versión LTS actual** (los pares son LTS: 6, 8, 10) y ya es la que usa el cascarón — se usará como base. |
| Proveedor de base de datos propuesto | **Microsoft SQL Server** vía `Microsoft.EntityFrameworkCore.SqlServer` (decisión actualizada del propietario el 2026-08-12 — reemplaza la propuesta inicial de PostgreSQL/Npgsql; ver `architecture-decisions.md` ADR-002). Sin servidor SQL Server local confirmado en este entorno; las migraciones no se aplicarán a nada hasta que se configure explícitamente en Fase 2 (SQL Server local, contenedor Docker, o LocalDB, sólo para desarrollo). |
| AGENTS.md / CLAUDE.md | **No existen** en ninguno de los dos proyectos ni en la raíz `NpisOG`. No hay instrucciones de repositorio previas que seguir. |
| Control de versiones | La carpeta `NpisOG` **no es un repositorio git** (`Is a git repository: false`). No hay historial para hacer `git blame`; toda inferencia de "última intención" se basa en comentarios de código y en `.lovable/plan.md`. |

## 2. Archivos revisados (todos, íntegros)

`src/types/npi.ts`, `src/context/{NPIContext,AuthContext}.tsx`, `src/pages/{NPIForm,NPIDetail,ComercialPanel,IngenieriaPanel,DisenoPanel,Dashboard,DashboardControl,Login,Index,NotFound}.tsx`, `src/components/{InfoRequestPanel,CommentLog,ChangeRequestLog,AttachmentChips,AttachmentsTimeline,FieldChangeSelector,NPIInconsistencyAlert,NPIStatusBadge,NPIStatusBadges,TimerChip,TimerDisplay,TimerEventLog,AppLayout,AppSidebar,NavLink,TableSearch}.tsx`, `src/lib/{workflowGuards,workflowValidation,timers,timerLog,stageTimer,businessHours,attachments,utils}.ts`, `src/data/{users,quotations,catalogs,seedNpis,npiFields}.ts`, `README.md`, `.lovable/plan.md`, `e2e/npi_flow.py` + `e2e/README.md`, `package.json`, `src/test/example.test.ts`.

No se modificó ningún archivo del frontend.

## 3. Modelo de datos actual (`NPIRecord`)

`NPIRecord` (`src/types/npi.ts`) es un único objeto plano con **~110 campos** que mezcla, sin separación de agregados:

- **Snapshot de cotización** ("verdes"): `numeroCotizacion, cliente, tipoProducto, descripcion, descripcionProducto, ejecutivoVenta, ancho, pi/ppl/pslTipoPelicula, pi/ppl/pslCalibre, pesoBobina, taponCore, piezasCajaBulto` — copiados de `Quotation` al cargar por número.
- **Captura libre** ("amarillos"): ~16 campos de texto libre.
- **Catálogo** ("azules"): ~19 campos `select` contra `CATALOGS`.
- **Booleanos SI/NO** ("naranjas"): 14 campos.
- **Workflow**: `status` (enum global, 19 valores), `statusDiseno` (7 valores), `statusIngenieria` (8 valores), `createdBy/At`, `assignedToIngenieria/Diseno/Comercial`.
- **Cronómetros**: 4 objetos `TimerState` embebidos (`timerGlobal, timerComercial, timerIngenieria, timerDiseno`) + `timerEvents: TimerEvent[]` (log append-only).
- **PrintCard**: `printCardCode`, `printCardFile` (**data URL base64 embebido**), `printCardVersions: PrintCardVersion[]`, `printCardApprovals: { diseno?, ingenieria?, cliente? }`.
- **SAP**: `codigoPT`, `ptName`, `ptSapEvidence`, `pedidoSAP(+At/By)`, `etCreatedAt`, `etSisproAt`.
- **Colecciones**: `comments: NPIComment[]`, `notifications: {...}[]`, `attachments: Attachment[]` (fileData en base64), `changeRequests: ChangeRequest[]`, `infoRequests: InfoRequest[]`.
- **Factibilidades**: `feasibilityDiseno?`, `feasibilityIngenieria?: FeasibilityRecord`.
- **Casos especiales**: `artesValidadasAt/By`, `rechazoTemporalInsumos?`, `insumosAcuerdo?`, `clientApproval*`, `printCardSentToClientAt/By`, `fieldsToChange?: string[]`, `clientChangesRequested?`.

**Conclusión de diseño**: el pedido del usuario de "no convertir NPIRecord en tabla gigante" es válido — este objeto es exactamente eso. El diseño de dominio (Fase 1) descompondrá esto en el agregado `Npi` + entidades satélite con ciclo de vida propio: `NpiComment`, `Attachment`, `ChangeRequest`, `InformationRequest`, `FeasibilityReview` (x2), `PrintCardVersion`, `PrintCardApproval`, `TimerState`/`TimerEvent` (x4 líneas), y campos de hitos SAP como columnas simples del agregado `Npi` (no ameritan entidad propia: son escalares con fecha, sin ciclo de vida ni permisos independientes).

Todos los ~70 campos de captura (verde/amarillo/azul/naranja) permanecen como columnas del agregado `Npi` — **no** se normalizan en tablas adicionales porque no tienen ciclo de vida, autorización ni versión propia; sólo se consultan y auditan como parte del snapshot del NPI. Están catalogados en `src/data/npiFields.ts` (clave interna + etiqueta + grupo), lo cual se reutilizará para nombrar las columnas EF.

## 4. Roles y usuarios

`src/data/users.ts`: 10 usuarios semilla, **contraseña `1234` en texto plano** para todos, bajo dominio `bioflex.mx`. Roles: `admin_total | admin_comercial | admin_ingenieria | admin_diseno | usuario_comercial | usuario_ingenieria | usuario_diseno`, cada uno con `area: 'comercial'|'ingenieria'|'diseno'|'total'`.

`AuthContext.tsx`: **autenticación 100% cliente** — compara email+password contra el arreglo `USERS` en memoria, guarda `{id}` en `localStorage['npi_user']`, sin token, sin expiración, sin hashing. Trivialmente falsificable (editar localStorage). Esto se reemplaza por completo por ASP.NET Identity + JWT (Fase 3); los 10 usuarios se migran como **seed de Development únicamente**, con contraseñas generadas/configurables (nunca `1234` real) y advertencia explícita.

## 5. Estados y workflow

### 5.1 Tres máquinas de estado paralelas
- `NPIStatus` (global, 19 valores) — mezcla estados de fases distintas y además **reutiliza el mismo valor `producto_en_sap`** tanto para "PT registrado en SAP por Ingeniería" como para "ET creada" (se distinguen sólo por `statusIngenieria` y por la presencia de `etSisproAt`). Esto confirma que el estado global por sí solo **no** es una buena fuente de verdad para el dominio; el backend derivará el estado visible a partir de hitos explícitos (¿existe `feasibilityIngenieria`? ¿existe `printCardLoadedAt`? ¿existe `codigoPT`? etc.), no de un enum monolítico.
- `StatusDiseno` (7 valores) y `StatusIngenieria` (8 valores) — estados **paralelos por área**, añadidos después (no documentados en el README original), calculados/mantenidos manualmente en cada handler de `NPIDetail.tsx`.

### 5.2 Inventario de transiciones (comportamiento efectivo, por función/handler)

| # | Acción | Archivo:función | Estado origen (guardas) | Efecto principal |
|---|---|---|---|---|
| 1 | Crear NPI (borrador) | `NPIForm.handleSave(false)` | — | `status=en_creacion`, sin timers |
| 2 | Enviar NPI (nuevo o editado) | `NPIForm.handleSave(true)` | `en_creacion` \| `rechazado_cambios` | `status=enviado\|enviado_con_cambios`; arranca/reanuda `timerIngenieria`; arranca `timerGlobal` (1ª vez); **NO** arranca `timerDiseno` |
| 3 | Asignar a Ingeniería | `NPIDetail.handleAssignIngenieria` | admin ingeniería, sin asignado | `status=en_revision`, `statusIngenieria=npi_asignado` |
| 4 | Asignar a Diseño | `NPIDetail.handleAssignDiseno` | admin diseño, sin asignado | `statusDiseno=npi_asignado`; si `status===aceptado` → `en_diseno` |
| 5 | Factibilidad de Diseño | `NPIDetail.handleDisenoFactible` | usuario diseño asignado, `statusDiseno=npi_asignado` | crea `FeasibilityRecord`, `statusDiseno=diseno_factible` |
| 6 | Factibilidad de Ingeniería | `NPIDetail.handleIngFactible` | ver **hallazgo bloqueante §7.1** | crea `FeasibilityRecord`; **pausa** `timerIngenieria`; si `artesValidadasAt` ya existe arranca `timerDiseno`; `status=creacion_et`, `statusIngenieria=ingenieria_factible` |
| 7 | Rechazar con cambios | `NPIDetail.handleRejectChanges` | ingeniería revisando | `status=rechazado_cambios`, `statusIngenieria=info_solicitada_validacion`; pausa Ing, reanuda Comercial; crea `ChangeRequest` |
| 8 | Rechazar por no factibilidad | `NPIDetail.handleRejectNoFeasibility` | ingeniería revisando | `status=rechazado_no_factibilidad` (**terminal**); cierra timers Ing/Com/Diseño |
| 9 | Artes Validadas | `NPIDetail.handleArtesValidadas` | diseño asignado, sin marca previa | `artesValidadasAt/By`; si `feasibilityIngenieria` ya existe arranca `timerDiseno` |
| 10 | Rechazo temporal por insumos | `NPIDetail.handleRechazoTemporalInsumos` | ingeniería, sin factibilidad aún | pausa timers Ing+Diseño; `statusIngenieria=rechazo_temporal_insumos` |
| 11 | Acuerdo de insumos | `NPIDetail.handleAcuerdoInsumos` | comercial, texto+evidencia obligatorios | reanuda Ing, pausa Com; limpia el rechazo temporal |
| 12 | Rechazo total por insumos | `NPIDetail.handleRechazoTotalInsumos` | comercial | `status=rechazado_falta_insumos` (**terminal**); cierra los 4 timers |
| 13 | Generar/nueva versión PrintCard | `NPIDetail.handleSendPrintCard` | diseño asignado + `feasibilityIngenieria` | nueva `PrintCardVersion`; **resetea** `printCardApprovals`; `status=printcard_enviado`, `statusDiseno=printcard_enviado_aprobacion` |
| 14 | Aprobar PrintCard (diseño/ing/cliente) | `NPIDetail.approvePrintCard` | según rol; **evidencia obligatoria** si `who==='cliente'` | añade aprobación; si las 3 existen → `status=alta_sap_solicitada` automático (Alta SAP no tiene paso manual) |
| 15 | Enviar PrintCard a Cliente | inline en JSX (botón) | comercial, sin aprobación cliente aún | `printCardSentToClientAt/By`, `status=en_aprobacion_cliente` |
| 16 | Cliente solicita cambios | inline en JSX (botón "Enviar a Diseño") | comercial | `clientChangesRequested`, `status=en_diseno`; crea `ChangeRequest` (**no hay acción "Rechazado por el Cliente" real — ver §7.3**) |
| 17 | PrintCard cargado en sistema | `NPIDetail.handlePrintCardLoaded` | diseño, `hasClienteApproval` | `printCardLoadedAt`, `status=printcard_cargado_sistema`; cierra `timerDiseno`; arranca `timerIngenieria` sólo si nunca existió |
| 18 | Registrar PT en SAP | `NPIDetail.handleProductoEnSAP` | ingeniería, **requiere `printCardLoadedAt`** | `codigoPT`, `ptName?`, evidencia opcional; `status=producto_en_sap`, `statusIngenieria=pt_cargado_sap` |
| 19 | Registrar Pedido en SAP | `NPIDetail.handleRegistrarPedidoSAP` | comercial, `codigoPT` presente | `pedidoSAP(+At/By)`; `status=pedido_sap_generado` |
| 20 | ET Creada / cargada en SisPro | `NPIDetail.handleETCreada` | ingeniería, requiere `codigoPT && pedidoSAP` | `etCreatedAt=etSisproAt=now`; cierra `timerIngenieria`; `status=producto_en_sap` (reutiliza el mismo valor que #18), `statusIngenieria=et_cargada_sispro` |
| 21 | Solicitud de información (Diseño→Cliente o →Comercial) | `InfoRequestPanel.submitRequest` | diseño asignado, NPI en cualquier estado activo | pausa `timerDiseno`; si es a Comercial, además arranca/reanuda `timerComercial` |
| 22 | Info recibida del Cliente | `InfoRequestPanel.resolveClienteReq` | diseño | reanuda `timerDiseno` |
| 23 | Comercial responde info a Diseño | `InfoRequestPanel.sendInfoToDis` | comercial | pausa Comercial, reanuda Diseño; `status=informacion_enviada_comercial` |

"Ciclo cerrado" en la UI = `printCardLoadedAt && etSisproAt` (`isCicloCerrado` en `workflowGuards.ts`); no es un valor de `status` por sí mismo.

### 5.3 Cierre automático global (invariante transversal)
`NPIContext.updateNPI` **evalúa en cada actualización** (no sólo en acciones específicas): si `timerIngenieria` y `timerDiseno` están ambos cerrados y `timerGlobal` no lo está, cierra `timerComercial` (si seguía abierto) y `timerGlobal` automáticamente, registrando eventos de auditoría. Este es un invariante de agregado que debe reforzarse en el dominio tras **cualquier** transición que cierre un timer de área (no como middleware genérico de "on every update").

## 6. Cronómetros

`TimerState { start, end, accumulatedMs, runningSince }` + operaciones puras `startTimer/pauseTimer/resumeTimer/closeTimer/getElapsedMs` (`lib/timers.ts`). Toda la aritmética usa **tiempo hábil**: L–V 09:00–18:00 `America/Mexico_City` (offset fijo UTC-6, sin horario de verano desde 2022), excluyendo una lista fija de festivos MX 2024–2030 (`lib/businessHours.ts`, `businessMsBetween`). El acumulado se persiste como milisegundos hábiles ya consumidos (`accumulatedMs`) + `runningSince` para el tramo activo; el tiempo transcurrido se **calcula al leer**, nunca se guarda un contador incrementando cada segundo — exactamente el patrón que el backend debe reproducir vía `IClock` + `IBusinessCalendar`.

`timerLog.ts` envuelve cada operación para producir `TimerEvent` (`start|pause|resume|close|stage_change`) con actor, duración acumulada al momento y nota — log append-only por NPI (`timerEvents`), reutilizable como base del `AuditEvent`/`TimerEvent` de dominio.

`stageTimer.ts` mapea cada `NPIStatus` a una etapa (`comercial|ingenieria|diseno`) para saber qué timer mostrar como "activo" — utilidad de presentación, no de dominio.

## 7. Contradicciones y ambigüedades detectadas

Clasificación: 1) comportamiento efectivo actual, 2) comportamiento documentado, 3) contradicción, 4) recomendación, 5) decisión pendiente del propietario. El detalle completo está en `docs/assumptions-and-ambiguities.md`; aquí sólo el resumen de las que importan para el diseño.

### 7.1 ✅ RESUELTO — Las factibilidades de Ingeniería y Diseño son independientes
- **Código actual** (`workflowGuards.ts:20`): `canStartIngenieriaFeasibility()` **siempre devuelve `true`**, con un comentario explícito: *"Engineering puede evaluar factibilidad en cuanto el Admin de Ingeniería le asigne — no depende de la factibilidad de Diseño."*
- El texto de bloqueo aún **presente en el JSX** de `NPIDetail.tsx` (línea ~910) y el paso 1 del E2E (`e2e/npi_flow.py`) reflejaban una regla secuencial anterior, hoy descontinuada.
- **Decisión del propietario**: se confirma el comportamiento del código actual — Ingeniería y Diseño evalúan su factibilidad **de forma independiente/paralela**. `FeasibilityReview[Ingeniería]` no requiere `FeasibilityReview[Diseño].Approved` como precondición (ni viceversa). `canCreatePrintCard` sigue exigiendo únicamente factibilidad de Ingeniería + asignación a Diseño, igual que hoy. El mensaje de bloqueo y el paso 1 del E2E se documentan como comportamiento histórico descontinuado y no se replican en el backend.

### 7.2 Visibilidad de "no asignados" inconsistente entre Diseño e Ingeniería (no bloqueante)
`IngenieriaPanel.tsx`: un `usuario_ingenieria` sólo ve NPIs **asignados a él**. `DisenoPanel.tsx`: un `usuario_diseno` ve los asignados a él **y además los que aún no tienen `assignedToDiseno`** (línea 33). El README dice "el tipo Usuario podrá ver, editar y borrar solo lo que tiene asignado" — la regla de Diseño lo contradice. **Interpretación adoptada (conservadora)**: en el backend, los usuarios normales sólo ven lo propio/asignado en ambas áreas (igual que Ingeniería); documentado como decisión revisable por el negocio.

### 7.3 Estado `rechazado_cliente` inalcanzable (no bloqueante)
`NPIStatus` incluye `rechazado_cliente` y aparece en filtros, labels y KPIs de rechazo (`Dashboard.tsx`), pero **ningún handler en `NPIDetail.tsx` lo produce** — sólo existe el flujo iterativo "cliente solicita cambios" y la aprobación. El README original sí prevé "Aceptado por el Cliente" **o** "Rechazado por el Cliente" como dos botones. **Recomendación**: implementar la acción `request-client-changes` (ya existe, iterativa) y agregar una acción explícita de rechazo definitivo por cliente (`reject-client`) que cierre el ciclo como terminal, ya que el tipo de datos y toda la UI de listados ya la anticipan.

### 7.4 `TERMINAL_STATUSES` incompleto (no bloqueante)
`NPIContext.tsx:20` sólo considera terminales `printcard_cargado_sistema` y `rechazado_no_factibilidad` (se usa exclusivamente para migrar registros legacy sin `timerGlobal`). `rechazado_falta_insumos` también es terminal en la práctica (cierra los 4 timers) pero no está en esa lista — impacto limitado a la migración de datos antiguos, no a la lógica de negocio en vivo. Se documenta para no replicar el descuido en el backend: el dominio marcará como terminales `printcard_cargado_sistema` (ciclo cerrado exitoso), `rechazado_no_factibilidad` y `rechazado_falta_insumos`.

### 7.5 Reutilización del mismo `status` global para dos hitos distintos (no bloqueante, ya cubierto en §5.2)
`producto_en_sap` se usa tanto para "PT registrado" como para "ET creada". El backend no replicará un enum global; usará hitos con fecha explícita (`PtRegisteredAt`, `EtCreatedAt`, etc.) y derivará cualquier "estado visible" agregando esos hitos.

### 7.6 Autorización de lectura sin control por recurso (hallazgo de seguridad, no bloqueante — el backend ya debe corregirlo por requisito explícito)
`NPIDetail.tsx` no filtra el acceso de lectura: cualquier usuario autenticado que conozca o adivine el `id`/ruta de un NPI puede abrir `/npi/:id` y ver toda la información (comentarios, adjuntos, cronómetros) de **cualquier área**, aunque los botones de acción sí se ocultan según rol. Sólo los *listados* (`ComercialPanel`, `IngenieriaPanel`, `DisenoPanel`) filtran por asignación/área. Esto es exactamente el patrón IDOR que el pedido original prohíbe explícitamente — el backend implementará autorización por recurso también en las rutas de detalle (`GET /api/npis/{id}`), no sólo en listados.

### 7.7 Catálogos declarados pero no usados en el formulario actual (menor, no bloqueante)
`CATALOGS['TIPO DE MATERIAL']` y `CATALOGS['SOLICITANTE']` existen en `data/catalogs.ts` pero ningún campo de `NPIForm.tsx` los referencia hoy. Se migrarán igualmente como catálogos administrables (podrían usarse en una versión futura del formulario), marcados como "actualmente sin campo asociado en el frontend".

## 8. Numeración de NPI
`generateNPINumber()` (`NPIContext.tsx:239`) = `max(número de NPIs con formato "NPI-000X" y X<1000) + 1`, sin ningún lock — carrera de condición clásica en cliente único (mitigado hoy sólo porque es una app de una sola pestaña con `localStorage`, no un sistema concurrente real). Los NPIs semilla usan números ≥1000 (`NPI-1006`, `NPI-1007`) explícitamente para no colisionar con la secuencia 0001+. El backend implementará un objeto `SEQUENCE` de SQL Server (alternativa: tabla de folios con bloqueo pesimista `UPDLOCK, HOLDLOCK`, sin usar `SELECT ... FOR UPDATE`, que es sintaxis de PostgreSQL) con restricción/índice único sobre `NpiNumber`, arrancando en 1, y los NPIs semilla de desarrollo conservarán números ≥1000 fuera de la secuencia real (mismo criterio que ya usa el frontend) para no interferir. Detalle formal en `architecture-decisions.md` ADR-005.

## 9. Adjuntos / almacenamiento
Todo archivo (`Attachment.fileData`, `PrintCardVersion.fileData`, evidencias) se guarda como **data URL base64 embebido directamente en el registro**, incluso dentro de `localStorage` (con lógica de *fallback* `stripHeavyData` cuando se excede la cuota). Exactamente el antipatrón que el pedido prohíbe. El backend implementará `IFileStorage` (local en dev, ampliable a S3/Azure/MinIO) guardando en base de datos sólo metadatos (id, nombre saneado, content-type validado por firma de archivo, tamaño, storage key, hash, usuario, fecha, contexto, versión).

## 10. Cotizaciones y catálogos
`data/quotations.ts`: 7 cotizaciones simuladas (`findQuotation(numero)`), representando datos que en el negocio real ya existen en "otra plataforma" (ver README). Se implementará `IQuotationProvider` con una implementación local (tabla `Quotation` semilla) reemplazable después por integración externa.
`data/catalogs.ts`: 19 catálogos de opciones libres (`Record<string,string[]>`), usados por casi todos los campos "azules" del formulario — se modelan como tablas `Catalog`/`CatalogOption` administrables, no como enums, porque son listas de negocio que cambian sin tocar código (a diferencia de `NPIStatus`/`AreaType`/roles, que sí son enums porque gobiernan lógica de transición).

## 11. Notificaciones
`sendNotification()` (`NPIContext.tsx:267`) sólo dispara un `toast` local — no hay correo/push real. El backend implementará `INotificationService` (abstracción) respaldada inicialmente por una entidad `Notification` consultable (no push en tiempo real; no se pidió SignalR) y un stub de email que loguea en desarrollo.

## 12. Pruebas existentes
- `src/test/example.test.ts`: trivial, no cubre lógica real.
- `e2e/npi_flow.py` (Playwright, manual, no en CI): documenta 14 "candados" del flujo completo Comercial→Diseño→Ingeniería con capturas de pantalla. Es la referencia más rica de comportamiento *esperado* end-to-end, pero **algunos pasos ya no coinciden con el código actual** (ver §7.1 y la nota sobre el orden PT-en-SAP vs PrintCard-cargado-en-sistema en `assumptions-and-ambiguities.md`) — se usa como documentación histórica, no como oráculo de verdad absoluta.

## 13. Siguiente paso
El único punto bloqueante (§7.1) ya fue resuelto por el propietario: las factibilidades de Ingeniería y Diseño son independientes. El resto de hallazgos no bloqueantes tiene una interpretación conservadora registrada en `docs/assumptions-and-ambiguities.md`. No hay impedimentos restantes para iniciar la Fase 1 (solución, dominio y pruebas de dominio) tras la aprobación del propietario.
