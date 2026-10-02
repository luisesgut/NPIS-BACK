# Matriz de autorización

> Todas las verificaciones de acceso a un NPI concreto se hacen **por recurso** (comprobando `AssignedEngineeringUserId`, `AssignedDesignUserId`, `AssignedCommercialUserId`, `CreatedByUserId` del NPI consultado contra el usuario del token), nunca sólo con `[Authorize(Roles=...)]`. Las consultas de listado filtran desde la base de datos (no se carga todo y se filtra en memoria). Recurso inexistente → `404`. Recurso existente pero sin autorización → `403`. Esto corrige el hallazgo de IDOR documentado en `backend-analysis.md` §7.6 (el frontend actual no aplicaba esta regla en la vista de detalle).

**Leyenda de columnas de rol**: AT=`AdminTotal`, AC=`AdminComercial`, AI=`AdminIngenieria`, AD=`AdminDiseno`, UC=`UsuarioComercial`, UI=`UsuarioIngenieria`, UD=`UsuarioDiseno`.

**Leyenda de celdas**: `Full` = sin restricción adicional dentro del alcance indicado · `Área` = todos los NPIs relevantes para esa área, asignados o no · `Propio/Asig.` = sólo si es creador, asignado al recurso, o (en Comercial) coincide como ejecutivo de venta · `Asignado` = únicamente si el recurso está asignado a ese usuario · `—` = no permitido.

## 1. Lectura de NPIs (decisión 11 aplicada a todas las superficies de lectura, no sólo a los paneles)

| Recurso/consulta | AT | AC | AI | AD | UC | UI | UD |
|---|---|---|---|---|---|---|---|
| Listar NPIs (`GET /api/npis`, filtros) | Full | Full (todos — Comercial es el área que da seguimiento a todo el ciclo, igual que hoy en `ComercialPanel`) | Área | Área | Propio/Asig. | Asignado | Asignado |
| Ver detalle de un NPI (`GET /api/npis/{id}`) | Full | Full | Área | Área | Propio/Asig. | Asignado | Asignado |
| Historial/auditoría del NPI (`GET /api/npis/{id}/audit`) | Full | Full | Área | Área | Propio/Asig. | Asignado | Asignado |
| Comentarios/adjuntos/timeline del NPI | igual que el detalle del NPI en todas las columnas | | | | | | |
| Dashboard general (`GET /api/dashboard`) | Full | Full | Área | Área | **Sólo agregados sobre sus propios/asignados** (aplicando decisión 11 — el frontend actual no filtraba esta vista, se corrige) | Sólo agregados sobre sus asignados | Sólo agregados sobre sus asignados |
| Dashboard de Control (`GET /api/dashboard/control`) | Full (todas las áreas — herramienta de trazabilidad cruzada) | Full | Full | Full | — (sin acceso; sólo administradores, igual que hoy en `AppSidebar`) | — | — |
| Notificaciones propias (`GET /api/notifications`) | Propias | Propias | Propias | Propias | Propias | Propias | Propias |

## 2. Acciones de workflow (ver `workflow.md` para el detalle de cada una)

| # | Acción | AT | AC | AI | AD | UC | UI | UD | Condición de recurso |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Crear borrador | ✔ | ✔ | — | — | ✔ | — | — | Creador = usuario actual (del token) |
| 2 | Editar borrador | ✔ | ✔ (cualquiera) | — | — | ✔ | — | — | Propio o `AssignedCommercialUserId` |
| 3 | Enviar (`submit`) | ✔ | ✔ | — | — | ✔ | — | — | Propio o `AssignedCommercialUserId` |
| 4 | Asignar a Ingeniería | ✔ | — | ✔ | — | — | — | — | Ninguna (asignación inicial es exclusiva de admins) |
| 5 | Asignar a Diseño | ✔ | — | — | ✔ | — | — | — | ídem |
| 6 | Reasignar Comercial | ✔ | ✔ | — | — | — | — | — | Ninguna |
| 7 | Factibilidad de Ingeniería | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si `AssignedEngineeringUserId == self` |
| 8 | Factibilidad de Diseño | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si `AssignedDesignUserId == self` |
| 9 | Rechazar con cambios (Ing→Com) | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 10 | Rechazar por no factibilidad | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 11 | Artes Validadas | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 12 | Crear versión de PrintCard | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 13a | Aprobar PrintCard — rol `Design` | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 13b | Aprobar PrintCard — rol `Engineering` | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 13c | Aprobar PrintCard — rol `Client` (decisión 8: siempre lo ejecuta Comercial en nombre del cliente) | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 14 | Enviar PrintCard a Cliente | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 15 | Cliente solicita cambios | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 16 | Rechazo definitivo del cliente | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 17 | PrintCard cargado en Sistema | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 18 | Registrar PT en SAP | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 19 | Registrar Pedido en SAP | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 20 | Registrar ET Creada | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 21 | Registrar ET cargada en SisPro | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 22 | Rechazo temporal por insumos | ✔ | — | ✔ | — | — | ✔ | — | UI sólo si asignado |
| 23 | Acuerdo para solventar insumos | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 24 | Rechazo total por insumos | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 25 | Solicitar información (a cliente o a comercial) | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 26 | Resolver info del cliente | ✔ | — | — | ✔ | — | — | ✔ | UD sólo si asignado |
| 27 | Responder info a Diseño (Comercial) | ✔ | ✔ | — | — | ✔ | — | — | UC sólo si propio/asignado |
| 28 | Agregar comentario | ✔ | ✔ | Área | Área | Propio/Asig. | Asignado | Asignado | Igual que la matriz de lectura §1 |
| 29 | Cancelar NPI | ✔ | ✔ | — | — | — | — | — | Ninguna (sólo administrativo) |

## 3. Recursos independientes

| Recurso/acción | AT | AC | AI | AD | UC | UI | UD |
|---|---|---|---|---|---|---|---|
| Listar usuarios por área (para asignar) | Full | Área Comercial | Área Ingeniería | Área Diseño | — | — | — |
| Consultar catálogos (lectura) | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Administrar catálogos (crear/editar/desactivar opciones) | ✔ | — | — | — | — | — | — |
| Buscar/consultar cotizaciones (por número) | ✔ | ✔ | ✔ (lectura) | ✔ (lectura) | ✔ | — | — |
| Crear usuarios (operación administrativa protegida; sin registro público) | ✔ | — | — | — | — | — | — |
| Login / refresh / logout | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |

## 4. Reglas generales que rigen toda la tabla

1. `AdminTotal` tiene acceso total a todo — no requiere condición de recurso en ninguna fila.
2. Un administrador de área (`AdminComercial`/`AdminIngenieria`/`AdminDiseno`) puede **consultar y administrar** todo lo de su área, esté o no asignado — coherente con "el Administrador podrá ver todos los registros creados de lo que corresponda a su área y asignar quien debe atender cada registro" (README).
3. Un usuario normal (`UsuarioComercial`/`UsuarioIngenieria`/`UsuarioDiseno`) **sólo** consulta y opera lo propio/asignado (decisión 11, aplicada de forma simétrica a Ingeniería y Diseño — corrige la inconsistencia detectada en el frontend donde Diseño exponía además los NPIs sin asignar).
4. Las asignaciones (acciones 4, 5, 6) sólo las ejecutan administradores de esa área o `AdminTotal` — nunca un usuario normal se autoasigna.
5. Ninguna acción de workflow acepta el rol/área/usuario "desde el cliente" (body del request) para decidir autorización — el usuario actual siempre se obtiene del token (`ClaimTypes.NameIdentifier` + rol vía Identity), nunca de un campo del payload.
6. Toda acción no autorizada por esta matriz responde `403`; todo recurso inexistente responde `404`, incluso si el usuario no tendría permiso de verlo igualmente (para no filtrar existencia de recursos a usuarios sin acceso, se evalúa primero existencia+alcance de forma combinada: un NPI fuera del alcance del usuario responde `404`, no `403`, para no confirmar su existencia a quien no debería saberlo — política de "denegar por inexistencia" aplicada a los roles de usuario normal; los administradores de área/total sí reciben `403` cuando corresponda porque su alcance ya cubre la existencia del recurso).
