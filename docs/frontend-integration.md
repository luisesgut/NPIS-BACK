# Integración con el frontend (Fase 7)

> Este documento es una guía de referencia para quien conecte `Npis-Front/npi-launchpad` con esta
> API. **El código del frontend no fue modificado en ningún momento de este proyecto** — todo lo
> descrito aquí es lo que haría falta para reemplazar sus datos mock (`localStorage`/arrays en
> memoria, ver `NPIContext.tsx`/`AuthContext.tsx`) por llamadas reales. No es un plan de trabajo
> comprometido, sólo el mapa de la brecha.

## 1. Cómo apuntar el frontend a esta API

El frontend no tiene hoy ninguna variable de entorno para una base URL de API (`NPIContext`
guarda todo en memoria/`localStorage`). Al integrarlo, se recomienda:

```
# Npis-Front/npi-launchpad/.env.local (no existe todavía)
VITE_API_BASE_URL=https://localhost:5443
```

y un cliente HTTP central (`fetch`/`axios`) que:
1. Adjunte `Authorization: Bearer <accessToken>` en cada llamada.
2. Ante un `401`, intente una vez `POST /api/auth/refresh` con el refresh token guardado y
   reintente la llamada original; si el refresh también falla, cierre sesión localmente.
3. Nunca guarde el access/refresh token en `localStorage` en texto plano si se puede evitar XSS
   con `httpOnly` — está fuera del alcance de este backend (decisión de frontend), pero se
   documenta como recomendación.

CORS ya está configurado para aceptar el origen de desarrollo de Vite (`http://localhost:8080`,
puerto confirmado en `vite.config.ts`) — ver `Cors:AllowedOrigins` en `.env.example`.

## 2. Autenticación

| Frontend (mock actual) | Backend real |
|---|---|
| `AuthContext` valida contra `data/users.ts` en memoria, sin expiración | `POST /api/auth/login` → `{ accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, user }` |
| Sesión "para siempre" mientras la pestaña esté abierta | Access token de vida corta (`Jwt:AccessTokenMinutes`) + `POST /api/auth/refresh` para rotarlo; reutilizar un refresh token ya rotado revoca toda la sesión (ver `architecture-decisions.md`) |
| No hay logout real (sólo limpia estado local) | `POST /api/auth/logout` revoca el refresh token en servidor |
| `users.ts` con contraseñas en claro | No hay registro público; los 10 usuarios semilla (mismos correos que `data/users.ts`) existen sólo en Development, con contraseña configurable — ver `Seed:UsersPassword` |

El usuario autenticado (id/rol/área) se obtiene de `GET /api/auth/me` o de los claims del propio
JWT — el frontend nunca debe enviar `role`/`area`/`userId` en el body de otras peticiones, el
backend siempre los toma del token.

## 3. Endpoints por pantalla

Referencia rápida; el contrato completo (request/response/errores) está en `docs/api-contract.md`.

| Pantalla / acción del frontend | Endpoint(s) |
|---|---|
| Login | `POST /api/auth/login` |
| Listado de NPIs (`NPIContext.npis`) | `GET /api/npis?...` (paginado; alcance ya filtrado por rol, no filtrar de nuevo en el cliente) |
| Detalle de NPI | `GET /api/npis/{id}` |
| Crear NPI (`NPIForm.handleSave(false)`) | `POST /api/npis` |
| Editar borrador | `PUT /api/npis/{id}` |
| Enviar NPI | `POST /api/npis/{id}/submit` |
| Cada botón de acción de workflow (asignar, aprobar factibilidad, PrintCard, SAP/ET, insumos, información) | Uno de los 20+26 endpoints de `POST /api/npis/{id}/{accion}` — ver `api-contract.md` §4 y `workflow.md` para el catálogo completo de las 29 acciones |
| Comentarios | `GET/POST /api/npis/{id}/comments` |
| Adjuntos (subir/descargar evidencia, PrintCard, etc.) | `POST /api/npis/{id}/attachments` (multipart), `GET /api/npis/{id}/attachments/{attachmentId}/download` |
| Notificaciones | `GET /api/notifications`, `POST /api/notifications/{id}/read` |
| Cotización (autocompletar campos "verdes") | `GET /api/quotations/{numero}` |
| Catálogos (`data/catalogs.ts`) | `GET /api/catalogs` |
| `Dashboard.tsx` / `DashboardControl.tsx` | `GET /api/dashboard/summary` — ver §4 |

## 4. El modelo de estatus: `NPIStatus` (frontend) vs. el dominio real (backend)

`src/types/npi.ts` modela el estatus como **un solo string plano** de 19 valores
(`NPIStatus`). El dominio del backend lo separa en tres señales independientes, más ricas y
consistentes con cómo realmente avanza cada área (ver `domain-model.md` y la decisión de
"factibilidades independientes" confirmada durante Fase 0):

- `LifecycleStatus`: `Draft | Active | Completed | Rejected | Cancelled` — el macro-estado.
- `EngineeringAreaStatus`: progreso de Ingeniería (`Requested → Assigned → FeasibilityApproved → ... → EtLoadedInSispro`).
- `DesignAreaStatus`: progreso de Diseño (`Requested → Assigned → FeasibilityApproved → ArtesValidadas → PrintCardInApproval → PrintCardLoaded`), más `AwaitingInformation` cuando hay una solicitud de información pendiente.
- `TerminalReason` (sólo si `LifecycleStatus == Rejected`): `NoFeasibility | SuppliesRejectionTotal | ClientRejection`.

**No es una traducción mecánica 1:1** — el backend corrige una simplificación real del mock: el
frontend usa un único valor `rechazado_falta_insumos`, pero el dominio distingue un rechazo
**temporal** por insumos (`RejectSuppliesTemporary`, recuperable vía
`ResolveSuppliesAgreement`) de uno **total y terminal** (`RejectSuppliesTotal`,
`TerminalReason=SuppliesRejectionTotal`) — dos flujos de negocio distintos que el frontend no
distinguía. Referencia aproximada para orientar una futura UI:

| `NPIStatus` (frontend) | `LifecycleStatus` | Señal de área relevante |
|---|---|---|
| `en_creacion` | `Draft` | — |
| `enviado`, `enviado_con_cambios` | `Active` | `EngineeringAreaStatus = Requested/Assigned` |
| `en_revision` | `Active` | `EngineeringAreaStatus = Assigned` (factibilidad aún no aprobada) |
| `rechazado_cambios` | `Active` | `EngineeringAreaStatus = ChangesRequested` |
| `aceptado` | `Active` | ambas factibilidades `FeasibilityApproved` |
| `en_diseno` | `Active` | `DesignAreaStatus = FeasibilityApproved` → `ArtesValidadas` |
| `rechazado_no_factibilidad` | `Rejected` | `TerminalReason = NoFeasibility` |
| `printcard_enviado`, `en_aprobacion_cliente`, `aceptado_cliente` | `Active` | `DesignAreaStatus = PrintCardInApproval` (progreso dentro de las 3 aprobaciones — ver `PrintCardVersion`/`PrintCardApproval`) |
| `rechazado_cliente` | `Rejected` | `TerminalReason = ClientRejection` |
| `informacion_enviada_comercial` | `Active` | `DesignAreaStatus = AwaitingInformation` resuelto (ver `InformationRequest`) |
| `alta_sap_solicitada`, `producto_en_sap`, `pedido_sap_generado`, `creacion_et` | `Active` | `EngineeringAreaStatus = PtRegistered → EtCreated → EtLoadedInSispro` |
| `printcard_cargado_sistema` | `Active` (**no** terminal — decisión de negocio confirmada en Fase 0) | `DesignAreaStatus = PrintCardLoaded` |
| `rechazado_falta_insumos` | `Active` (temporal, recuperable) **o** `Rejected` (total) | ver distinción arriba |

Una UI real debería derivar su propio "badge de estatus" combinando estas tres señales en vez de
esperar un string único del backend — igual que hoy `NPIStatusBadges.tsx` deriva presentación a
partir de varios campos del NPI.

## 5. Dashboard

`Dashboard.tsx` y `DashboardControl.tsx` calculan **todo en el cliente**, iterando el arreglo
completo de NPIs (`npis.filter(...)`, promedios con `Date.now() - start`). Eso no escala ni es
seguro (expondría NPIs completos de otras áreas sólo para que el cliente los descarte al
filtrar). `GET /api/dashboard/summary` (§9 de `api-contract.md`) hace las mismas cuentas con
`GROUP BY`/`AVG` en SQL, ya acotadas al alcance del usuario autenticado — el frontend dejaría de
necesitar el arreglo completo de NPIs sólo para pintar KPIs.

Diferencia de unidades a tener en cuenta al portar los cálculos: el frontend mide duraciones en
milisegundos de **reloj** (`Date.now() - start`, incluye fines de semana/noches); el backend mide
en milisegundos **hábiles** (`MexicoBusinessCalendar`, igual que cada `TimerState` del NPI). Los
números de `averageGlobalDurationBusinessMs`/`slaCompliancePercent` no son comparables
directamente contra los que hoy calcula `DashboardControl.tsx` — son la versión correcta, no un
reemplazo aritmético equivalente.

Correspondencia de campos:

| Frontend | Backend (`DashboardSummaryDto`) |
|---|---|
| `total` (`Dashboard.tsx`) | `totalCount` |
| `aceptados`/`rechazados`/`enProceso` (bucketing manual sobre `NPIStatus`) | derivar de `countsByLifecycleStatus` (`Completed`+`Active` en curso avanzado ≈ aceptados; `Rejected` = rechazados; `Draft`/`Active` inicial ≈ en proceso) — no hay una correspondencia 1:1 exacta, ver §4 |
| `avgHours` (promedio de `timerIngenieria`) | `averageEngineeringDurationBusinessMs` (÷ 3 600 000 para horas) |
| `pieData` (distribución por estatus) | `countsByLifecycleStatus` |
| `barData` (NPIs por cliente) | `countsByClient` |
| `kpis.closed`/`kpis.inProcess` (`DashboardControl.tsx`) | `closedCount`/`inProcessCount` |
| `kpis.closedByPrio`/`inProcessByPrio` | `closedCountByPriority`/`inProcessCountByPriority` |
| `kpis.avgMs`/`avgByPrio` | `averageGlobalDurationBusinessMs`/`averageGlobalDurationBusinessMsByPriority` |
| `slaPct` (cálculo manual de 9 días de reloj) | `slaCompliancePercent` (9 días **hábiles**) |

## 6. Lo que NO cambia con esta integración

- Ningún endpoint acepta `status`/`lifecycleStatus` de escritura libre — cada acción de
  `NPIDetail.tsx` debe mapear a su endpoint de workflow específico, nunca a un `PATCH` genérico.
- El frontend nunca debe construir ni enviar `npiNumber`, `createdAt`, `userId` del actor, o
  cualquier timer — todos los genera el servidor.
- Los archivos (PrintCard, evidencias) se suben como `multipart/form-data` reales — el frontend
  actual no tiene esa lógica (usa placeholders/nombres de archivo simulados en
  `DashboardControl.tsx`), habría que construirla.
