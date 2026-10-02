# Workflow del NPI — catálogo de acciones (Fase 0)

> Cada acción es un comando de negocio independiente, transaccional, con autorización propia (nunca un `PATCH` genérico de `status`). Los nombres de acción no se muestran como endpoints en la Fase 0 — la forma exacta de ruta se confirma en `api-contract.md`. Todas las fechas son UTC (`DateTimeOffset`) obtenidas de `IClock`, nunca de `DateTime.UtcNow` directo en dominio. Todas las acciones se ejecutan dentro de una transacción (ver `domain-model.md` §6) y son verificadas por concurrencia optimista (`RowVersion`); un conflicto de versión responde `409`.

**Convención de "repetición de la misma solicitud"**: salvo que se indique lo contrario, reenviar el mismo comando cuando el NPI **ya está** en el estado resultante devuelve `409 Conflict` con un `ProblemDetails` que indica la causa (no se reprocesa ni se duplica el efecto) — esto es un `409` de "transición inválida", no un error genérico. Las excepciones (idempotencia real, sin error) se marcan explícitamente por acción.

---

## Grupo 1 — Captura y envío (Comercial)

#### 1. Crear borrador
- **Actor autorizado:** `UsuarioComercial`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** N/A (creación).
- **Precondiciones:** ninguna de negocio; validación de campos vía FluentValidation (tipos, longitudes, catálogos válidos si se envían).
- **Mutaciones:** crea `Npi` con `LifecycleStatus=Draft`, `CreatedByUserId=usuario actual (del token)`, `AssignedCommercialUserId=CreatedByUserId`, sin `NpiNumber` todavía (se asigna en `submit`, ver ADR de numeración) o con un número provisional reservado — decisión técnica en Fase 2.
- **Estado resultante:** `Draft`.
- **Cronómetros:** ninguno inicia.
- **Auditoría:** `AuditEvent(Action=NpiCreated)`.
- **Notificaciones:** ninguna.
- **Errores posibles:** `400` validación; `401` sin autenticación.
- **Repetición:** no aplica (cada llamada crea un recurso nuevo — no idempotente por diseño, como cualquier `POST` de creación).

#### 2. Editar borrador
- **Actor autorizado:** creador, o `AssignedCommercialUserId`, o `AdminComercial`/`AdminTotal`.
- **Estado/condiciones de origen:** `Draft`, o `Active` con `EngineeringAreaStatus=ChangesRequested` (reenvío tras solicitud de cambios).
- **Precondiciones:** el NPI no está en estado terminal.
- **Mutaciones:** actualiza campos de captura enviados (parcial); nunca acepta `LifecycleStatus`, `NpiNumber`, timers ni asignaciones en este comando.
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** sin cambio.
- **Auditoría:** `AuditEvent(Action=NpiDraftUpdated, Fields=[...])`.
- **Notificaciones:** ninguna.
- **Errores posibles:** `400`, `403` (no es propietario/asignado/admin), `404`, `409` si el NPI ya es terminal.
- **Repetición:** idempotente en el sentido normal de un `PATCH` (mismo payload, mismo resultado); no genera error por sí sola.

#### 3. Enviar NPI (`submit`)
- **Actor autorizado:** mismo que "Editar borrador".
- **Estado/condiciones de origen:** `Draft` (primer envío) o `Active` con `EngineeringAreaStatus=ChangesRequested` (reenvío).
- **Precondiciones:** campos obligatorios completos (validados por FluentValidation); si es primer envío, no existe `NpiNumber` aún.
- **Mutaciones:** si `Draft` → asigna `NpiNumber` atómicamente (ver `architecture-decisions.md`), `LifecycleStatus=Active`, `EngineeringAreaStatus=Requested` (o `Assigned` si se preasignó ingeniero en la captura), arranca `GlobalTimer` y `EngineeringTimer`. Si reenvío tras cambios: `EngineeringAreaStatus=Requested/Assigned` de nuevo, reanuda `EngineeringTimer`, pausa `CommercialTimer`.
- **Estado resultante:** `Active`.
- **Cronómetros:** primer envío → `GlobalTimer.Start`, `EngineeringTimer.Start`; `DesignTimer` **no** arranca. Reenvío → `EngineeringTimer.Resume`, `CommercialTimer.Pause`.
- **Auditoría:** `AuditEvent(Action=NpiSubmitted | NpiResubmittedAfterChanges)`.
- **Notificaciones:** a `AdminIngenieria` (si no hay ingeniero preasignado) y/o al ingeniero preasignado; a `AdminDiseno` (Diseño se habilita en paralelo desde el envío).
- **Errores posibles:** `400` (campos incompletos), `403`, `404`, `409` (ya `Active`/terminal).
- **Repetición:** `409` si ya está `Active` y no hay cambios pendientes que reenviar.

---

## Grupo 2 — Asignaciones

#### 4. Asignar a Ingeniería
- **Actor autorizado:** `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `AssignedEngineeringUserId == null` (o ya asignado, para **reasignar** — mismo comando, distinto efecto, ver "Repetición").
- **Precondiciones:** el usuario destino existe, tiene rol `UsuarioIngenieria` y `Area=Ingenieria`.
- **Mutaciones:** `AssignedEngineeringUserId`, `EngineeringAreaStatus=Assigned`.
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** sin cambio (ya corre desde `submit`).
- **Auditoría:** `AuditEvent(Action=EngineeringAssigned|EngineeringReassigned, FromUserId, ToUserId)`.
- **Notificaciones:** al usuario asignado.
- **Errores posibles:** `400` (usuario no válido para el área), `403`, `404`, `409` (NPI terminal).
- **Repetición:** llamar de nuevo con **el mismo** usuario devuelve `200` idempotente (sin nuevo evento de auditoría duplicado más allá de lo ya registrado); con **otro** usuario, es una reasignación válida (no error) sólo si el actor es admin.

#### 5. Asignar a Diseño
- Igual forma que "Asignar a Ingeniería", con `AssignedDesignUserId`, `EngineeringAreaStatus` → `DesignAreaStatus=Assigned`, actor `AdminDiseno`/`AdminTotal`. No depende de que Ingeniería ya esté asignada (Diseño participa en paralelo desde el envío, README).

#### 6. Reasignar Comercial
- **Actor autorizado:** `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** cualquiera no terminal.
- **Precondiciones:** usuario destino con `Area=Comercial`.
- **Mutaciones:** `AssignedCommercialUserId`.
- **Estado resultante:** sin cambio.
- **Cronómetros:** sin cambio.
- **Auditoría:** `AuditEvent(Action=CommercialReassigned)`.
- **Notificaciones:** al nuevo propietario comercial.
- **Errores posibles:** `403`, `404`, `409` (terminal).
- **Repetición:** idempotente con el mismo usuario.

---

## Grupo 3 — Factibilidades (paralelas e independientes — decisión 1)

#### 7. Factibilidad de Ingeniería
- **Actor autorizado:** `AssignedEngineeringUserId` actual, o `AdminIngenieria`/`AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `AssignedEngineeringUserId != null`, sin `FeasibilityReview[Engineering]` previa.
- **Precondiciones:** **ninguna** relacionada con Diseño (decisión 1 — evaluación independiente). No depende de `FeasibilityReview[Design]`.
- **Mutaciones:** crea `FeasibilityReview(Area=Engineering, ApprovedBy, ApprovedAt, Note?, Attachments?)`; `EngineeringAreaStatus=FeasibilityApproved`. Si además ya existe `ArtesValidadasAt` en Diseño → arranca `DesignTimer` (invariante 6).
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** ninguno se cierra aquí (el frontend pausaba `EngineeringTimer`; en el backend permanece corriendo hasta que se registre el PT en SAP, para reflejar que Ingeniería sigue con trabajo pendiente — ver nota en `assumptions-and-ambiguities.md`).
- **Auditoría:** `AuditEvent(Action=EngineeringFeasibilityApproved)`.
- **Notificaciones:** a `AssignedDesignUserId` (Diseño puede continuar con el PrintCard si ya tiene su propia factibilidad y Artes Validadas).
- **Errores posibles:** `403` (no asignado/no admin), `404`, `409` (ya existe factibilidad, o NPI terminal).
- **Repetición:** `409` si ya existe `FeasibilityReview[Engineering]`.

#### 8. Factibilidad de Diseño
- Misma forma, `Area=Design`, actor `AssignedDesignUserId`/`AdminDiseno`/`AdminTotal`, `DesignAreaStatus=FeasibilityApproved`. Tampoco depende de la de Ingeniería (decisión 1).

#### 9. Rechazar con solicitud de cambios (Ingeniería → Comercial)
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, sin `FeasibilityReview[Engineering]` aprobada aún.
- **Precondiciones:** debe incluir `Description` o al menos un campo marcado en `FieldKeys`.
- **Mutaciones:** crea `ChangeRequest(FromArea=Engineering, ToArea=Commercial, FieldKeys, Description, Attachments?)`; `EngineeringAreaStatus=ChangesRequested`.
- **Estado resultante:** `Active` (no terminal — es un ciclo, no un rechazo definitivo).
- **Cronómetros:** `EngineeringTimer.Pause`, `CommercialTimer.Start/Resume`.
- **Auditoría:** `AuditEvent(Action=EngineeringRequestedChanges)`.
- **Notificaciones:** a `AssignedCommercialUserId`/creador.
- **Errores posibles:** `400` (sin descripción ni campos), `403`, `404`, `409`.
- **Repetición:** cada solicitud adicional (tras un reenvío de Comercial) crea un nuevo `ChangeRequest` — no es un error, es el ciclo normal.

#### 10. Rechazar por no factibilidad (terminal)
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`.
- **Precondiciones:** ninguna adicional (Ingeniería puede declarar no-factibilidad en cualquier momento antes de su propia aprobación).
- **Mutaciones:** `LifecycleStatus=Rejected`, `TerminalReason=NoFeasibility`, `EngineeringAreaStatus=NoFeasibility`.
- **Estado resultante:** `Rejected` (terminal).
- **Cronómetros:** cierra `EngineeringTimer`, `DesignTimer` (si corría), `CommercialTimer` (si corría), y `GlobalTimer` (invariante 7 — único junto con "completar exitosamente").
- **Auditoría:** `AuditEvent(Action=NpiRejectedNoFeasibility)`.
- **Notificaciones:** a `AssignedCommercialUserId`/creador.
- **Errores posibles:** `403`, `404`, `409` (ya terminal).
- **Repetición:** `409`.

---

## Grupo 4 — Artes Validadas y PrintCard

#### 11. Artes Validadas (Diseño)
- **Actor autorizado:** `AssignedDesignUserId`, `AdminDiseno`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `AssignedDesignUserId != null`, `ArtesValidadasAt == null`.
- **Precondiciones:** ninguna respecto a factibilidades (puede marcarse antes o después de cualquiera de las dos factibilidades).
- **Mutaciones:** `ArtesValidadasAt/ByUserId`; `DesignAreaStatus=ArtesValidadas`. Si `FeasibilityReview[Engineering]` ya existe → arranca `DesignTimer` (invariante 6).
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** arranca `DesignTimer` sólo si la factibilidad de Ingeniería ya estaba aprobada; si no, queda pendiente de que ésta llegue.
- **Auditoría:** `AuditEvent(Action=ArtworkValidated)`.
- **Notificaciones:** ninguna obligatoria (evento interno de Diseño).
- **Errores posibles:** `403`, `404`, `409` (ya marcado, o NPI terminal).
- **Repetición:** `409` si ya existe `ArtesValidadasAt`.

#### 12. Crear versión de PrintCard
- **Actor autorizado:** `AssignedDesignUserId`, `AdminDiseno`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`.
- **Precondiciones (decisión 2 — las 4 deben cumplirse):** `AssignedDesignUserId != null` **y** `FeasibilityReview[Engineering].Approved` **y** `FeasibilityReview[Design].Approved` **y** `ArtesValidadasAt != null`.
- **Mutaciones:** crea `PrintCardVersion(VersionNumber=max+1, Code, Attachment?, Note?)`; invalida aprobaciones de la versión anterior (no las borra: quedan históricas, ligadas a su propia `PrintCardVersionId`); `DesignAreaStatus=PrintCardInApproval`.
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** `CommercialTimer.Start/Resume` (Comercial debe gestionar el envío a cliente), `DesignTimer.Pause` (Diseño entrega y espera aprobaciones).
- **Auditoría:** `AuditEvent(Action=PrintCardVersionCreated, VersionNumber)`.
- **Notificaciones:** a `AssignedCommercialUserId` y `AssignedEngineeringUserId`.
- **Errores posibles:** `400` (código faltante), `403`, `404`, `409` (alguna de las 4 precondiciones no cumplida — mensaje específico de cuál falta).
- **Repetición:** cada llamada exitosa crea una nueva versión (no es "repetir la misma solicitud", cada una es intencional); si se reenvía el mismo `Code` sin cambios reales, igual crea nueva versión (el negocio no pidió deduplicar por código).

#### 13. Aprobar PrintCard vigente
- **Actor autorizado:** según `role` del comando — `Design`: `AssignedDesignUserId`/`AdminDiseno`/`AdminTotal`; `Engineering`: `AssignedEngineeringUserId`/`AdminIngenieria`/`AdminTotal`; `Client`: **siempre `AssignedCommercialUserId`/`AdminComercial`/`AdminTotal`** (decisión 8 — el cliente no es un usuario del sistema; Comercial registra la decisión en su nombre).
- **Estado/condiciones de origen:** existe `PrintCardVersion` vigente sin aprobación de ese rol.
- **Precondiciones:** si `role=Client`, `EvidenceAttachmentId` es **obligatorio** (invariante 5); si `role=Client`, requiere que exista un registro previo de "enviado a cliente" (acción 15).
- **Mutaciones:** crea `PrintCardApproval(PrintCardVersionId, Role, ByUserId, At, Note?, Evidence?)`. Si al agregar ésta ya existen las 3 (`Design`+`Engineering`+`Client`) → dispara automáticamente **solicitud de Alta SAP** (evento interno, no cambia `LifecycleStatus`; sólo marca que Ingeniería puede proceder a registrar PT en SAP).
- **Estado resultante:** sin cambio de `LifecycleStatus`.
- **Cronómetros:** si `role=Client` y la aprobación queda completa: `CommercialTimer.Pause`, `DesignTimer.Resume` (si ya existía).
- **Auditoría:** `AuditEvent(Action=PrintCardApproved, Role)`; si se completan las 3: `AuditEvent(Action=PrintCardFullyApproved)`.
- **Notificaciones:** al completar las 3 aprobaciones, a `AssignedEngineeringUserId` (puede iniciar registro de PT en SAP).
- **Errores posibles:** `400` (falta evidencia obligatoria para `Client`), `403`, `404`, `409` (ya existe aprobación de ese rol para la versión vigente, o NPI terminal).
- **Repetición:** `409` si el rol ya aprobó esa versión.

#### 14. Enviar PrintCard a Cliente
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** existe `PrintCardVersion` vigente, sin aprobación de `Client` todavía.
- **Precondiciones:** ninguna adicional.
- **Mutaciones:** registra `PrintCardSentToClientAt/By` (columna simple en `Npi`, o evento de auditoría — sin entidad propia, es informativo).
- **Estado resultante:** sin cambio.
- **Cronómetros:** sin cambio directo.
- **Auditoría:** `AuditEvent(Action=PrintCardSentToClient)`.
- **Notificaciones:** ninguna al sistema (el envío es externo, fuera de la plataforma — el "cliente" no tiene cuenta).
- **Errores posibles:** `403`, `404`, `409`.
- **Repetición:** reenviar actualiza sólo la fecha (no es un error — el negocio puede reenviar el mismo PrintCard más de una vez antes de recibir respuesta).

#### 15. Cliente solicita cambios (registrado por Comercial, no terminal)
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal` (decisión 8).
- **Estado/condiciones de origen:** existe `PrintCardVersion` vigente enviada a cliente, sin aprobación `Client` completa.
- **Precondiciones:** `Description` obligatoria.
- **Mutaciones:** crea `ChangeRequest(FromArea=Commercial, ToArea=Design, Origin=ClientReported, Description, Attachments?)`; `DesignAreaStatus` vuelve a un estado previo apropiado (p. ej. `FeasibilityApproved` o `ArtesValidadas`, según lo que ya existiera) para permitir una nueva versión de PrintCard.
- **Estado resultante:** `Active` (ciclo, no terminal).
- **Cronómetros:** sin cambio automático (Diseño retoma trabajo cuando cree la siguiente versión).
- **Auditoría:** `AuditEvent(Action=ClientChangesRequested)`.
- **Notificaciones:** a `AssignedDesignUserId`.
- **Errores posibles:** `400`, `403`, `404`, `409`.
- **Repetición:** cada llamada crea una nueva ronda de cambios — parte normal del ciclo iterativo.

#### 16. Rechazo definitivo del cliente (terminal — **nueva acción, decisión 7**)
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal` (decisión 8 — Comercial registra la decisión del cliente).
- **Estado/condiciones de origen:** `Active`, con al menos una `PrintCardVersion` enviada a cliente (opcionalmente sin aprobación completa).
- **Precondiciones:** `Comment` **obligatorio**; `EvidenceAttachmentId` opcional.
- **Mutaciones:** `LifecycleStatus=Rejected`, `TerminalReason=ClientRejection`; `Npi.ClientRejection = {At, ByUserId, Comment, EvidenceAttachmentId?}`.
- **Estado resultante:** `Rejected` (terminal).
- **Cronómetros:** cierra los 4 timers (invariante 7 — rechazo terminal).
- **Auditoría:** `AuditEvent(Action=NpiRejectedByClient)`.
- **Notificaciones:** a `AssignedEngineeringUserId` y `AssignedDesignUserId`.
- **Errores posibles:** `400` (sin comentario), `403`, `404`, `409` (ya terminal).
- **Repetición:** `409`.

#### 17. PrintCard cargado en Sistema
- **Actor autorizado:** `AssignedDesignUserId`, `AdminDiseno`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `PrintCardApproval[Client]` existe para la versión vigente (decisión: se mantiene el requisito actual del frontend de que el cliente haya aprobado).
- **Precondiciones:** `PrintCardLoadedAt == null`.
- **Mutaciones:** `PrintCardLoadedAt/ByUserId/VersionId`; `DesignAreaStatus=PrintCardLoaded`.
- **Estado resultante:** **`Active` — no terminal, no completa el ciclo** (decisión 3, invariante explícita).
- **Cronómetros:** **cierra únicamente `DesignTimer`** (decisión 3). No toca `EngineeringTimer`, `CommercialTimer` ni `GlobalTimer`.
- **Auditoría:** `AuditEvent(Action=PrintCardLoadedInSystem)`.
- **Notificaciones:** a `AssignedEngineeringUserId` (puede proceder a registrar PT en SAP).
- **Errores posibles:** `403`, `404`, `409` (ya cargado, aprobación de cliente ausente, o NPI terminal).
- **Repetición:** `409`.

---

## Grupo 5 — SAP y ET (decisión 9: hitos separados)

#### 18. Registrar PT en SAP
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `PrintCardLoadedAt != null`.
- **Precondiciones:** `CodigoPT` obligatorio; `PtName`, evidencia — opcionales.
- **Mutaciones:** `CodigoPT`, `PtName?`, `PtRegisteredAt/ByUserId`; `EngineeringAreaStatus=PtRegistered`.
- **Estado resultante:** `Active`.
- **Cronómetros:** sin cambio (se decidió no cerrar `EngineeringTimer` aquí; permanece corriendo hasta la ET — ver nota en assumptions).
- **Auditoría:** `AuditEvent(Action=SapPtRegistered)`.
- **Notificaciones:** a `AssignedCommercialUserId` (debe registrar el Pedido en SAP).
- **Errores posibles:** `400` (sin código), `403`, `404`, `409` (`PrintCardLoadedAt` ausente, ya registrado, o terminal).
- **Repetición:** `409` si ya existe `PtRegisteredAt`.

#### 19. Registrar Pedido en SAP
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `CodigoPT != null`.
- **Precondiciones:** `PedidoSapNumber` obligatorio.
- **Mutaciones:** `PedidoSapNumber`, `PedidoSapRegisteredAt/ByUserId`.
- **Estado resultante:** `Active`.
- **Cronómetros:** sin cambio.
- **Auditoría:** `AuditEvent(Action=SapOrderRegistered)`.
- **Notificaciones:** a `AssignedEngineeringUserId` (puede crear la ET).
- **Errores posibles:** `400`, `403`, `404`, `409` (`CodigoPT` ausente, ya registrado, o terminal).
- **Repetición:** `409` si ya existe `PedidoSapRegisteredAt`.

#### 20. Registrar ET Creada
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `CodigoPT != null` **y** `PedidoSapNumber != null`.
- **Precondiciones:** `EtCreatedAt == null`.
- **Mutaciones:** `EtCreatedAt/ByUserId`; `EngineeringAreaStatus=EtCreated`.
- **Estado resultante:** `Active` (**no** completa el ciclo por sí sola — decisión 9).
- **Cronómetros:** sin cambio.
- **Auditoría:** `AuditEvent(Action=EtCreated)`.
- **Notificaciones:** ninguna obligatoria (paso intermedio interno de Ingeniería).
- **Errores posibles:** `403`, `404`, `409` (precondiciones no cumplidas, ya creada, o terminal).
- **Repetición:** `409`.

#### 21. Registrar ET cargada en SisPro (dispara el cierre del ciclo si corresponde)
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, `EtCreatedAt != null`.
- **Precondiciones:** `EtLoadedInSisproAt == null`.
- **Mutaciones:** `EtLoadedInSisproAt/ByUserId`; `EngineeringAreaStatus=EtLoadedInSispro`; **evalúa la condición de éxito del ciclo** (decisión 4): si `PrintCardLoadedAt`, `PtRegisteredAt`, `PedidoSapRegisteredAt` y `EtLoadedInSisproAt` están todos presentes → `LifecycleStatus=Completed` en la misma transacción.
- **Estado resultante:** `Active` (si por alguna razón faltara alguno de los otros 3 hitos, lo cual no debería ocurrir dado el orden de precondiciones, pero se revalida por seguridad) o `Completed`.
- **Cronómetros:** cierra `EngineeringTimer`; si el ciclo se completa, cierra también `GlobalTimer` (invariante 7 — único cierre de `GlobalTimer` junto con el rechazo terminal). `CommercialTimer` se cierra igualmente si seguía abierto, como parte del cierre de ciclo.
- **Auditoría:** `AuditEvent(Action=EtLoadedInSispro)`; si completa: `AuditEvent(Action=NpiCycleCompleted)`.
- **Notificaciones:** a `AssignedCommercialUserId` y creador (ciclo completado).
- **Errores posibles:** `403`, `404`, `409` (`EtCreatedAt` ausente, ya cargada, o terminal).
- **Repetición:** `409`.

---

## Grupo 6 — Rechazo temporal/definitivo por falta de insumos

#### 22. Rechazo temporal por falta de insumos
- **Actor autorizado:** `AssignedEngineeringUserId`, `AdminIngenieria`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, sin `FeasibilityReview[Engineering]` aprobada, sin `CodigoPT`, sin `EtLoadedInSisproAt`.
- **Precondiciones:** ninguna obligatoria adicional (`Note` opcional).
- **Mutaciones:** `Npi.SuppliesTemporaryRejection={At, ByUserId, Note?}`; `EngineeringAreaStatus=SuppliesTemporaryRejection`.
- **Estado resultante:** `Active` (no terminal — es reversible vía acuerdo, o terminal vía rechazo total, acción 24).
- **Cronómetros:** `EngineeringTimer.Pause`, `DesignTimer.Pause` (si corría). `CommercialTimer`/`GlobalTimer` siguen corriendo.
- **Auditoría:** `AuditEvent(Action=SuppliesTemporaryRejection)`.
- **Notificaciones:** a `AssignedCommercialUserId`/creador.
- **Errores posibles:** `403`, `404`, `409` (ya en rechazo temporal, o terminal).
- **Repetición:** `409`.

#### 23. Acuerdo para solventar insumos
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** `EngineeringAreaStatus=SuppliesTemporaryRejection`.
- **Precondiciones:** `Text` **y** evidencia (`EvidenceAttachmentId`) obligatorios.
- **Mutaciones:** `Npi.SuppliesAgreement={Text, ResolvedAt, ResolvedByUserId, EvidenceAttachmentId}`; limpia `SuppliesTemporaryRejection`; `EngineeringAreaStatus` regresa a `Assigned` (o al estado previo apropiado).
- **Estado resultante:** `Active`.
- **Cronómetros:** `CommercialTimer.Pause`, `EngineeringTimer.Resume`.
- **Auditoría:** `AuditEvent(Action=SuppliesAgreementRegistered)`.
- **Notificaciones:** a `AssignedEngineeringUserId`.
- **Errores posibles:** `400` (falta texto o evidencia), `403`, `404`, `409` (no está en rechazo temporal).
- **Repetición:** `409` (el rechazo temporal ya fue resuelto).

#### 24. Rechazo total por falta de insumos (terminal)
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** `EngineeringAreaStatus=SuppliesTemporaryRejection`.
- **Precondiciones:** ninguna adicional obligatoria (se recomienda nota, no exigida hoy por el frontend — se mantiene igual).
- **Mutaciones:** `LifecycleStatus=Rejected`, `TerminalReason=SuppliesRejectionTotal`.
- **Estado resultante:** `Rejected` (terminal).
- **Cronómetros:** cierra los 4 timers.
- **Auditoría:** `AuditEvent(Action=NpiRejectedSuppliesTotal)`.
- **Notificaciones:** a `AssignedEngineeringUserId`, `AssignedDesignUserId`.
- **Errores posibles:** `403`, `404`, `409` (no está en rechazo temporal, o ya terminal).
- **Repetición:** `409`.

---

## Grupo 7 — Solicitudes de información (Diseño ⇄ Cliente/Comercial)

#### 25. Solicitar información
- **Actor autorizado:** `AssignedDesignUserId`, `AdminDiseno`, `AdminTotal`.
- **Estado/condiciones de origen:** `Active`, sin `InformationRequest` pendiente del mismo `Type`.
- **Precondiciones:** `Text` obligatorio; `Type ∈ {Client, Commercial}`.
- **Mutaciones:** crea `InformationRequest(Type, Status=Pending, Text, Attachment?)`; `DesignAreaStatus=AwaitingInformation`.
- **Estado resultante:** `Active`.
- **Cronómetros:** `DesignTimer.Pause`; si `Type=Commercial`, además `CommercialTimer.Start/Resume`.
- **Auditoría:** `AuditEvent(Action=InformationRequested, Type)`.
- **Notificaciones:** si `Type=Commercial`, a `AssignedCommercialUserId`/creador; si `Type=Client`, ninguna al sistema (el cliente no tiene cuenta).
- **Errores posibles:** `400`, `403`, `404`, `409` (ya hay una pendiente del mismo tipo, o terminal).
- **Repetición:** `409` si ya hay una `Pending` del mismo tipo (debe resolverse primero).

#### 26. Resolver solicitud (info del cliente recibida)
- **Actor autorizado:** `AssignedDesignUserId`, `AdminDiseno`, `AdminTotal`.
- **Estado/condiciones de origen:** existe `InformationRequest(Type=Client, Status=Pending)`.
- **Precondiciones:** ninguna adicional (registro manual de que la información llegó por un canal externo).
- **Mutaciones:** `InformationRequest.Status=Resolved, ResolvedAt/By`.
- **Estado resultante:** `Active`.
- **Cronómetros:** `DesignTimer.Resume`.
- **Auditoría:** `AuditEvent(Action=ClientInformationReceived)`.
- **Notificaciones:** ninguna.
- **Errores posibles:** `403`, `404`, `409` (no hay solicitud pendiente de tipo `Client`).
- **Repetición:** `409`.

#### 27. Responder solicitud (Comercial → Diseño)
- **Actor autorizado:** `AssignedCommercialUserId`, `AdminComercial`, `AdminTotal`.
- **Estado/condiciones de origen:** existe `InformationRequest(Type=Commercial, Status=Pending)`.
- **Precondiciones:** `ResponseText` obligatorio.
- **Mutaciones:** `InformationRequest.Status=Resolved, ResolvedAt/By, ResponseText, ResponseAttachment?`.
- **Estado resultante:** `Active`.
- **Cronómetros:** `CommercialTimer.Pause`, `DesignTimer.Resume`.
- **Auditoría:** `AuditEvent(Action=CommercialInformationSent)`.
- **Notificaciones:** a `AssignedDesignUserId`.
- **Errores posibles:** `400` (sin respuesta), `403`, `404`, `409` (no hay solicitud pendiente de tipo `Commercial`).
- **Repetición:** `409`.

---

## Grupo 8 — Transversales

#### 28. Agregar comentario
- **Actor autorizado:** cualquier usuario con acceso de lectura al NPI (ver `authorization-matrix.md`), **incluyendo NPIs en estado terminal** (registro histórico).
- **Estado/condiciones de origen:** cualquiera.
- **Precondiciones:** `Comment` o al menos un adjunto.
- **Mutaciones:** crea `NpiComment` (+ `Attachment`s opcionales, `Context=Comment`).
- **Estado resultante:** sin cambio de `LifecycleStatus`/hitos/timers.
- **Cronómetros:** sin cambio.
- **Auditoría:** `AuditEvent(Action=CommentAdded)`.
- **Notificaciones:** ninguna por defecto.
- **Errores posibles:** `400`, `403` (sin acceso de lectura al recurso), `404`.
- **Repetición:** cada llamada crea un comentario nuevo — no hay noción de "repetir" que deba bloquearse.

#### 29. Cancelar NPI (administrativo — **acción añadida para completar `LifecycleStatus.Cancelled`, decisión 10; ver nota de alcance pendiente en `assumptions-and-ambiguities.md`**)
- **Actor autorizado:** `AdminComercial` (dueño del proceso) o `AdminTotal`.
- **Estado/condiciones de origen:** `Draft` o `Active`.
- **Precondiciones:** `Reason` obligatorio.
- **Mutaciones:** `LifecycleStatus=Cancelled`.
- **Estado resultante:** `Cancelled` (terminal).
- **Cronómetros:** cierra los 4 timers que estuvieran abiertos (aplicado por higiene de datos — no se pierde información de KPI; ver assumptions).
- **Auditoría:** `AuditEvent(Action=NpiCancelled, Reason)`.
- **Notificaciones:** a todos los asignados.
- **Errores posibles:** `400` (sin razón), `403`, `404`, `409` (ya terminal).
- **Repetición:** `409`.

---

## Notas transversales sobre notificaciones y auditoría

- Toda fila de `AuditEvent` incluye: `ActorUserId`, `ActorRole` (snapshot del rol usado, tomado del token, no reconsultado después), `OccurredAt (IClock)`, `NpiId`, `Action`, `Details` (JSON con los campos relevantes del comando, nunca datos sensibles).
- Toda `Notification` se escribe en la misma transacción que la acción que la origina (ver `domain-model.md` §6); es de solo lectura vía polling (`GET /api/notifications`), no hay push en tiempo real en el alcance actual.
- Ningún comando de este catálogo acepta `status` como parámetro de entrada — cada uno tiene su propio contrato de request específico (ver `api-contract.md`).
