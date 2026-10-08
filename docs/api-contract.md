# Contrato de API (Fase 0 — diseño, sin implementación)

## 0. Convenciones generales

- Base: `/api`. Todas las rutas requieren `Authorization: Bearer <access-token>` salvo `POST /api/auth/login` y `POST /api/auth/refresh`.
- Errores: `application/problem+json` (RFC 9457 `ProblemDetails` / `ValidationProblemDetails` para `400` de validación de campos).
- Códigos usados consistentemente: `200` OK, `201` Created (con `Location`), `204` No Content (acciones que no devuelven cuerpo), `400` request inválido, `401` sin autenticación, `403` sin autorización sobre el recurso, `404` recurso inexistente (o fuera del alcance del usuario, ver `authorization-matrix.md` §4.6), `409` transición inválida o conflicto de concurrencia (`RowVersion` desactualizado), `422` no se usa (no se identificó un caso donde `400`/`409` no basten).
- Paginación: query params `page` (1-based, default 1), `pageSize` (default 20, máx. 100); respuesta envuelta en `{ items: [...], page, pageSize, totalCount }`.
- Ningún endpoint de acción de workflow acepta un campo `status` genérico — cada uno tiene su propio DTO específico (ver `workflow.md`).
- Todas las fechas en JSON son ISO-8601 UTC (`DateTimeOffset`).
- **Concurrencia optimista — mecanismo realmente implementado (difiere de lo planeado aquí en Fase 0):** el `rowVersion` **nunca se expone al cliente** — sigue siendo un detalle de persistencia invisible para Domain/Application (ver ADR-004). No hay `If-Match` ni `expectedRowVersion`: cada handler carga el Npi y lo guarda dentro de la misma request: si otra request concurrente lo guardó primero, EF lanza `DbUpdateConcurrencyException`, que `ApiExceptionHandler` traduce a `409` (`ProblemDetails.type = "concurrency-conflict"`). El cliente no reintenta con un token — sólo debe volver a consultar `GET /api/npis/{id}` y reintentar la acción con el estado fresco. Ver `ConcurrencyConflictEndpointTests.cs` (Fase 8).

## 1. Autenticación

| Método/Ruta | Request | Response | Códigos |
|---|---|---|---|
| `POST /api/auth/login` | `{ email, password }` | `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user: {id, fullName, email, role, area} }` | `200`, `400`, `401` (credenciales inválidas o cuenta bloqueada por intentos fallidos) |
| `POST /api/auth/refresh` | `{ refreshToken }` | igual que login (rotación: el refresh token usado queda revocado y se emite uno nuevo) | `200`, `401` (token inválido/expirado/revocado) |
| `POST /api/auth/logout` | `{ refreshToken }` (o se toma del usuario autenticado) | — | `204`, `401` |
| `GET /api/auth/me` | — | `{ id, fullName, email, role, area, mustChangePassword }` | `200`, `401` |
| `POST /api/auth/change-password` | `{ currentPassword, newPassword }` | igual que login: tokens nuevos para esta sesión; cierra las demás y quita la marca de contraseña temporal | `200`, `400` (actual incorrecta o nueva fuera de política), `401` |
| `POST /api/users/{id}/reset-password` (AdminTotal, nunca la propia cuenta) | — | `{ temporaryPassword }` — se devuelve una sola vez (`Cache-Control: no-store`), no se guarda en claro ni se envía por correo. Desbloquea la cuenta, cierra sus sesiones y le envía un aviso sin la contraseña | `200`, `400`, `403`, `404` |

Mientras `user.mustChangePassword` sea `true`, cualquier ruta fuera de `/api/auth` responde `403` con `type: "password-change-required"`.

No existe `POST /api/auth/register` (sin registro público — usuarios se crean vía seed de Development o la operación administrativa de §2).

## 2. Usuarios

| Método/Ruta | Request | Response | Códigos | Autorización |
|---|---|---|---|---|
| `GET /api/users?area=` | query `area` opcional | `[{ id, fullName, email, role, area, isActive }]` | `200`, `403` | Admins de área (su área), `AdminTotal` (todas) |
| `POST /api/users` (crear usuario) | `{ fullName, email, role, area, temporaryPassword? }` | `{ id, ... }` | `201`, `400`, `403` | Sólo `AdminTotal` |

## 3. NPIs — captura y consulta

| Método/Ruta | Request | Response | Códigos |
|---|---|---|---|
| `POST /api/npis` | `CreateNpiRequest` (todos los campos de captura excepto workflow/identidad — ver §3.1) | `NpiDetailResponse` | `201`, `400`, `401` |
| `PATCH /api/npis/{id}` | `UpdateNpiDraftRequest` (subconjunto parcial de campos de captura; **nunca** `status`, `npiNumber`, timers, asignaciones) | `NpiDetailResponse` | `200`, `400`, `403`, `404`, `409` |
| `GET /api/npis` | query: `search`, `lifecycleStatus`, `area`, `assignedToUserId`, `createdByUserId`, `priority`, `page`, `pageSize` | página de `NpiListItemResponse` | `200`, `401` |
| `GET /api/npis/{id}` | — | `NpiDetailResponse` | `200`, `403`, `404` |
| `GET /api/npis/{id}/audit` | paginado | página de `AuditEventResponse` | `200`, `403`, `404` |
| `GET /api/npis/{id}/timer-events` | — | `[TimerEventResponse]` | `200`, `403`, `404` |
| `GET /api/npis/{id}/capture-history` | — | `{ initialCapture?, initialPriority?, firstSubmittedAt?, firstSubmittedByName?, changes: [{ id, field, before, after, changedByName, changedAt, changeRequestId?, wasRequested, requestedByName?, requestedFromArea?, requestedAt?, requestDescription? }] }` — "fase inicial" (foto de la captura al primer envío) y cada campo que cambió después con quién lo modificó y qué solicitud lo pidió (`changeRequestId = null`: iniciativa de Comercial; `wasRequested = false`: se cambió mientras atendía esa solicitud sin que lo marcara). Los borradores no se registran. `initialCapture` es null en NPIs enviados antes del 2026-10-08 | `200`, `404` |
| `GET /api/npis/{id}/assignees` | — | `{ commercial: { userId, fullName }, engineering?, design? }` — mismo alcance que el detalle, para cualquier rol (`/api/users` es sólo para admins) | `200`, `404` |

### 3.1 `CreateNpiRequest` / `UpdateNpiDraftRequest` (forma resumida)
Campos de captura tal como los cataloga `npiFields.ts` del frontend (verde/amarillo/azul/naranja) + `quotationNumber` (para precargar snapshot vía `IQuotationProvider`) + `priority` + `preAssignedEngineeringUserId?`. **No incluye**: `id`, `npiNumber`, `lifecycleStatus`, `createdBy`, timers, `assignedTo*` (salvo el pre-asignado de ingeniería en creación), hitos SAP/PrintCard, comentarios, adjuntos.

### 3.2 `NpiListItemDto` (para tablas/paneles) — forma realmente implementada
`id, npiNumber, priority, lifecycleStatus, engineeringAreaStatus, designAreaStatus, cliente, numeroCotizacion, descripcionProducto, requestDate, createdByUserId, assignedEngineeringUserId?, assignedDesignUserId?, assignedCommercialUserId, globalTimer, commercialTimer, engineeringTimer, designTimer, completedStages` (`completedStages`: etapas completadas de las 8 de la ruta, calculadas en la misma consulta con `NpiStageMilestones` — mismas reglas que `buildStages` del frontend — para pintar el avance sin pedir el detalle de cada fila) (los 4 objetos `TimerDto` — ver §3.3 — se agregaron en Fase 8 para que los paneles pinten cronómetros sin una consulta de detalle por fila; son columnas propias del Npi, no colecciones, así que no violan "nunca traer todo el agregado"). Difiere del diseño de Fase 0: expone IDs de usuario (no nombres resueltos — el frontend los resuelve con `GET /api/users` cuando es admin) y usa camelCase real de la serialización, no los nombres de columna aquí listados originalmente.

### 3.3 `NpiDetailResponse`
Todos los campos de captura + `rowVersion` + `lifecycleStatus` + `terminalReason?` + `engineeringAreaStatus` + `designAreaStatus` + los 4 objetos de timer (`{ elapsedMs, isRunning, isClosed }` — **calculado en el servidor al leer**, nunca un contador persistido) + hitos (`artesValidadasAt`, `printCardLoadedAt`, `codigoPT`, `ptName`, `pedidoSapNumber`, `etCreatedAt`, `etLoadedInSisproAt`) + resúmenes de colecciones (`feasibilityReviews[]`, `printCardVersions[]` con sus `approvals[]`, `changeRequests[]`, `informationRequests[]`, `attachments[]`; los mensajes del chat van aparte, §5) + `assignedEngineering/Design/Commercial { id, fullName }`. Ninguna colección incluye contenido binario — los adjuntos exponen `{ id, fileName, contentType, sizeBytes, uploadedAt, uploadedByName, context, version, downloadUrl }`, donde `downloadUrl` es una ruta autenticada de descarga (§7), nunca un data URL embebido.

## 4. Acciones de workflow

Todas: `POST /api/npis/{id}/{accion}`, responden `200` con `NpiDetailResponse` actualizado (o `409`/`403`/`404`/`400` según `workflow.md`). Ninguna admite cuerpo vacío arbitrario salvo que la acción realmente no requiera datos (p. ej. `assign-engineering` sí requiere `{ userId }`).

| Ruta | Request DTO |
|---|---|
| `POST /api/npis/{id}/submit` | `{}` |
| `POST /api/npis/{id}/assign-engineering` | `{ userId }` |
| `POST /api/npis/{id}/assign-design` | `{ userId }` |
| `POST /api/npis/{id}/reassign-commercial` | `{ userId }` |
| `POST /api/npis/{id}/engineering-feasibility` | `{ note?, attachmentIds? }` |
| `POST /api/npis/{id}/design-feasibility` | `{ note?, attachmentIds? }` |
| `POST /api/npis/{id}/request-changes` | `{ description, fieldKeys?, attachmentIds? }` |
| `POST /api/npis/{id}/reject-no-feasibility` | `{ note? }` |
| `POST /api/npis/{id}/validate-artwork` | `{}` (409 `artwork-rejection-pending` si hay artes rechazadas sin corregir) |
| `POST /api/npis/{id}/request-design-changes` | `{ description, fieldKeys? }` — Diseño rechaza con cambios (no terminal; antes de su factibilidad; `description` obligatoria). Comercial corrige y reenvía con `submit`, que atiende a la vez lo pendiente de Ingeniería y de Diseño. Ingeniería sigue en paralelo |
| `POST /api/npis/{id}/reject-design-no-feasibility` | `{ note }` — terminal, `terminalReason = DesignNoFeasibility`; `note` obligatoria |
| `POST /api/npis/{id}/reject-artwork` | `{ reason, evidenceAttachmentId? }` — Diseño rechaza las artes (antes de validarlas). Queda como `ChangeRequest` con `origin = ArtworkRejected` |
| `POST /api/npis/{id}/resolve-artwork-rejection` | `{ note? }` — Comercial: exige artes nuevas (contexto `Artwork`) subidas después del rechazo |
| `POST /api/npis/{id}/printcards` | `{ code, attachmentId?, note? }` |
| `POST /api/npis/{id}/printcard-approvals` | `{ role: "Design"\|"Engineering"\|"Client", note?, evidenceAttachmentId? }` (obligatorio si `role="Client"`) |
| `POST /api/npis/{id}/send-printcard-to-client` | `{}` |
| `POST /api/npis/{id}/request-client-changes` | `{ description, attachmentIds? }` |
| `POST /api/npis/{id}/reject-client` | `{ comment, evidenceAttachmentId? }` |
| `POST /api/npis/{id}/load-printcard` | `{}` |
| `POST /api/npis/{id}/register-sap-pt` | `{ codigoPT, ptName?, evidenceAttachmentId? }` |
| `POST /api/npis/{id}/register-sap-order` | `{ pedidoSapNumber }` |
| `POST /api/npis/{id}/register-et` | `{}` |
| `POST /api/npis/{id}/load-et-sispro` | `{}` |
| `POST /api/npis/{id}/reject-supplies-temporary` | `{ note? }` |
| `POST /api/npis/{id}/resolve-supplies-agreement` | `{ text, evidenceAttachmentId }` |
| `POST /api/npis/{id}/reject-supplies-total` | `{ note? }` |
| `POST /api/npis/{id}/request-information` | `{ type: "Client"\|"Commercial", text, attachmentId? }` |
| `POST /api/npis/{id}/resolve-client-information` | `{}` |
| `POST /api/npis/{id}/respond-commercial-information` | `{ responseText, responseAttachmentId? }` |
| `POST /api/npis/{id}/cancel` | `{ reason }` |

## 5. Chat del NPI (antes "Comentarios")

Los comentarios se convirtieron en el chat del NPI: misma ruta, misma tabla (`NpiComments`) y mismo historial. Los mensajes ya no forman parte del agregado `Npi` ni de `NpiDetailResponse` (se leen con su propio endpoint) y ya no generan un `AuditEvent` por mensaje.

| Método/Ruta | Request | Response | Códigos |
|---|---|---|---|
| `GET /api/npis/{id}/comments` | `?page&pageSize` (máx. 100) | página de `CommentDto`, del más reciente al más antiguo | `200`, `404` |
| `POST /api/npis/{id}/comments` | `{ text, mentionedUserIds? }` | `CommentDto { id, npiId, authorUserId, authorName, text, createdAt, mentionedUserIds }` | `201`, `400` (vacío, > 4000 caracteres, > 10 menciones; quien escribe no es participante: `chat-not-participant`; mención a alguien inactivo o que no es participante: `chat-invalid-mention`), `404`. Sólo escriben los participantes (creador y responsables de las tres áreas); los admins que no participan sólo leen. Permitido incluso en estado terminal |
| `POST /api/npis/{id}/comments/read` | — | — | `204`, `404`. Marca leído el chat para el usuario actual y su aviso agrupado de la campana |
| `GET /api/npis/{id}/comments/participants` | — | `ChatParticipantDto[] { userId, fullName, area, isAssigned }`: participantes activos del NPI (creador y responsables): autocompletado de @ y lista de quién puede escribir | `200`, `404` |
| `GET /api/chat/unread` | — | `ChatUnreadDto[] { npiId, npiNumber, unreadCount, lastMessageAt }`: sólo NPIs donde el usuario es creador o responsable | `200` |

**Avisos.** Mencionado con @ → campana **y correo**. Resto de participantes (creador y responsables de Comercial, Ingeniería y Diseño) → **un solo** aviso de campana por NPI (`Notifications.GroupKey = "chat:{npiId}"`) que se reescribe con el último mensaje mientras no se lea. Nunca se avisa a quien escribe.

**Tiempo real (SignalR).** Hub en `/hubs/chat` (fuera de `/api`), autenticado con el mismo JWT; por WebSocket el token viaja en `?access_token=` y sólo se acepta en `/hubs`. Métodos del cliente: `JoinNpi(npiId)` (misma autorización que el detalle) y `LeaveNpi(npiId)`. Eventos del servidor: `MessagePosted(CommentDto)` al grupo del NPI y `ChatActivity({ npiId })` a los participantes y mencionados. El hub sólo empuja: toda escritura pasa por el `POST` de arriba. La conexión se cierra al vencer el access token y el cliente reconecta con uno nuevo. En IIS requiere la característica **Protocolo WebSocket** (sin ella SignalR cae a long polling).

## 6. Solicitudes de cambio e información (lectura)

| Método/Ruta | Response |
|---|---|
| `GET /api/npis/{id}/change-requests` | `[ChangeRequestResponse]` |
| `GET /api/npis/{id}/information-requests` | `[InformationRequestResponse]` |
| `GET /api/npis/{id}/printcards` | `[PrintCardVersionResponse]` (cada una con sus `approvals[]`) |
| `GET /api/npis/{id}/feasibility-reviews` | `[FeasibilityReviewResponse]` (0–2 filas) |

## 7. Adjuntos

| Método/Ruta | Request | Response | Códigos |
|---|---|---|---|
| `POST /api/npis/{id}/attachments` | `multipart/form-data`: archivo + `context` + (FK opcional al sub-recurso, p. ej. `changeRequestId`) | `{ id, fileName, contentType, sizeBytes, version }` | `201`, `400` (tipo/tamaño no permitido, o firma de archivo no coincide con la extensión), `403`, `404`, `413` (excede límite configurado) |
| `GET /api/npis/{id}/attachments/{attachmentId}/download` | — | stream del archivo, `Content-Disposition: attachment` | `200`, `403`, `404` |

Los adjuntos se suben **antes** de la acción que los referencia (p. ej. subir evidencia y luego llamar `printcard-approvals` con `evidenceAttachmentId`), evitando acciones "todo en uno" con archivos embebidos en el mismo payload JSON.

## 8. Cotizaciones y catálogos

| Método/Ruta | Response | Autorización |
|---|---|---|
| `GET /api/quotations/{numero}` | `QuotationResponse` (campos "verdes") o `404` | Comercial (admin/usuario) + `AdminTotal` |
| `GET /api/catalogs` | `[{ key, displayName, options: [{ value, isActive }] }]` | Cualquier usuario autenticado |
| `GET /api/catalogs/admin` | `[{ id, key, displayName, hint?, options: [{ id, value, displayOrder, isActive, protectedReason?, usageCount }] }]` | Ingeniería (`AdminTotal`, `AdminIngenieria`, `UsuarioIngenieria`) |
| `POST /api/catalogs/{catalogId}/options` | `{ value }` → `201` con la opción | Ingeniería |
| `PUT /api/catalogs/options/{optionId}` (renombrar) | `{ value }` → `204` | Ingeniería |
| `POST /api/catalogs/options/{optionId}/active` | `{ isActive }` → `204` | Ingeniería |
| `POST /api/catalogs/{catalogId}/order` | `{ optionIds }` (todas, en orden) → `204` | Ingeniería |
| `GET /api/catalogs/changes?catalogKey=` | últimos 200 cambios `{ catalogKey, action, oldValue, newValue, changedByName, changedAt }` | Ingeniería |

Reglas de administración: no se permiten duplicados ignorando mayúsculas, acentos y espacios (`catalog-option-duplicate`); las opciones que usan las reglas del formulario (`TIPO DE PRODUCTO`: BOBINA/BOLSA/POUCH; `ACABADO`: SIN IMPRESION) no se renombran ni desactivan (`catalog-option-protected`, ver `CatalogRules.cs`); renombrar o desactivar sólo afecta capturas nuevas — los NPIs conservan el texto capturado. No se borran opciones: se desactivan. Cada cambio queda en `CatalogChangeLogs`.

## 9. Dashboard y métricas

**Diferencia con el diseño original (Fase 0 → Fase 7):** se planeaban dos endpoints
(`/api/dashboard` general y `/api/dashboard/control` "sólo administradores"). Se implementó un
único `GET /api/dashboard/summary`, por dos razones: (1) el modelo de estatus del backend
(`LifecycleStatus` + `EngineeringAreaStatus`/`DesignAreaStatus`, ver `domain-model.md`) ya es más
rico que el `NPIStatus` plano que mockeaba el frontend, así que no hay dos "vistas" distintas que
construir — hay un único conjunto de agregados; (2) el alcance por rol no necesita un endpoint
aparte "sólo admin": el mismo predicado de autorización de `/api/npis` (`BuildAccessPredicate`)
ya limita automáticamente lo que ve cada usuario — un `UsuarioIngenieria` recibe el resumen de
sus propios NPIs asignados, un `AdminTotal` recibe el de todos. Ver `docs/frontend-integration.md`
§4 para el mapeo completo hacia los componentes `Dashboard.tsx`/`DashboardControl.tsx`.

| Método/Ruta | Response | Autorización |
|---|---|---|
| `GET /api/dashboard/summary` | `DashboardSummaryDto` (abajo) | Cualquier usuario autenticado — alcance limitado por rol, igual que `/api/npis` |

`DashboardSummaryDto`:

```jsonc
{
  "totalCount": 42,
  "countsByLifecycleStatus": { "Draft": 3, "Active": 20, "Completed": 15, "Rejected": 3, "Cancelled": 1 },
  "closedCount": 19,               // GlobalTimer cerrado (Completed + Rejected + Cancelled)
  "inProcessCount": 23,            // Draft + Active
  "closedCountByPriority": { "Baja": 4, "Media": 9, "Alta": 6 },
  "inProcessCountByPriority": { "Baja": 5, "Media": 12, "Alta": 6 },
  "averageGlobalDurationBusinessMs": 187200000,           // null si closedCount == 0
  "averageGlobalDurationBusinessMsByPriority": { "Baja": null, "Media": 190000000, "Alta": 165000000 },
  "averageEngineeringDurationBusinessMs": 54000000,       // null si ningún EngineeringTimer ha cerrado
  "slaCompliancePercent": 84.2,    // % de closedCount con GlobalTimer <= 9 días hábiles (81h); 100 si closedCount == 0
  "countsByClient": { "Cliente A": 12, "Cliente B": 7 }   // top 15 por volumen
}
```

Todas las duraciones están en milisegundos **hábiles** (mismo criterio que `TimerState` —
`MexicoBusinessCalendar`), no en tiempo de reloj — a diferencia del cálculo original del
frontend, que usaba `Date.now() - start` sin descontar fines de semana/festivos (ver
`docs/frontend-integration.md` §4 para la nota completa sobre esta diferencia de unidades).

## 10. Notificaciones

| Método/Ruta | Request | Response |
|---|---|---|
| `GET /api/notifications?unreadOnly=` | — | página de `{ id, message, npiId?, createdAt, readAt? }` |
| `POST /api/notifications/{id}/read` | — | `204` |

## 10.1 Reportes de problemas y sugerencias

Botón "Reportar" de la app. Cualquier usuario autenticado envía reportes y consulta el estado de
los suyos; la bandeja completa, las capturas y el cambio de estado son **exclusivos de AdminTotal**.

| Método/Ruta | Quién | Request | Response |
|---|---|---|---|
| `POST /api/feedback` | autenticado | `multipart/form-data`: `kind` (`Bug`/`Suggestion`/`Question`), `description` (5–4000), `pageUrl?`, `npiId?`, `userAgent?`, `screenSize?`, `technicalDetails?`, `screenshot?` (PNG/JPG, validado por firma binaria, mismo tamaño máximo que los adjuntos) | `201 { id }` |
| `GET /api/feedback/mine` | autenticado | — | `[{ id, kind, status, description, adminResponse?, createdAt, statusChangedAt? }]` |
| `GET /api/feedback?status=` | AdminTotal | `status?` (`New`/`InReview`/`Resolved`/`Dismissed`) | reportes completos, más recientes primero (máx. 500) |
| `GET /api/feedback/{id}/screenshot` | AdminTotal | — | la imagen, o `404` si el reporte no trae captura |
| `POST /api/feedback/{id}/status` | AdminTotal | `{ status, adminResponse? }` | `204` |

- Crear un reporte avisa a los AdminTotal activos: por campana y correo si es `Bug`, sólo campana en los demás tipos.
- Cambiar el estado avisa por campana a quien reportó (salvo que se regrese a `New`).
- `POST /api/feedback` está limitado a 10 reportes cada 10 minutos por usuario (`429`).

## 11. Health checks, Swagger y endurecimiento (Fase 7)

- `GET /health` (liveness, sin dependencias) — sin autenticación, para orquestadores.
- `GET /health/ready` (tag `ready`: conexión a SQL Server + raíz de almacenamiento de adjuntos escribible) — sin autenticación.
- `GET /swagger` — UI de OpenAPI con esquema de seguridad `Bearer` configurado (botón "Authorize"), disponible en Development (y detrás de autenticación adicional si se expone en otros entornos).
- Toda respuesta incluye `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy` restrictiva.
- CORS: sólo los orígenes listados en `Cors:AllowedOrigins` pueden llamar la API desde un navegador (ver `.env.example`); vacío por defecto fuera de Development.
- `POST /api/auth/login` y `POST /api/auth/refresh` están limitados a 10 solicitudes/minuto por IP (`429 Too Many Requests` al exceder) — adicional al bloqueo por cuenta de ASP.NET Core Identity tras 5 intentos fallidos.
- `HSTS` habilitado fuera de Development. Logging de requests limitado a método/ruta/código/duración — nunca encabezados (Authorization) ni cuerpos (credenciales, adjuntos).

## 12. Lo que este contrato **prohíbe explícitamente**

- Ningún endpoint permite `PUT/PATCH /api/npis/{id}` con un campo `status`/`lifecycleStatus` de escritura libre.
- Ningún DTO de respuesta expone entidades de EF Core directamente (todas son proyecciones/DTOs explícitos).
- Ningún endpoint acepta `userId`, `role`, `area`, `createdAt`, o cualquier dato de identidad/tiempo "confiando en el cliente" — el actor siempre sale del token; las fechas de servidor siempre las genera `IClock`.
