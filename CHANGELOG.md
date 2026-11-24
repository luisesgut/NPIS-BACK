# Changelog — Sistema NPI (API)

Versiones con [SemVer](https://semver.org/lang/es/): **MAYOR.MENOR.PARCHE**.
- **MAYOR**: cambio que rompe (el front viejo deja de funcionar con este back, o un flujo cambia de fondo).
- **MENOR**: feature nueva (puede llevar script SQL de migración).
- **PARCHE**: arreglo de un bug sin feature nueva.

El back y el front comparten número de versión y se publican juntos. Si una versión lleva script SQL, se indica.

## [1.4.0] — 2026-11-24

### Agregado
- Adjuntos `.eps` en contextos Artwork, PrintCard y PrintCardAnnex (`EpsFiles`): se decide por la extensión, se guarda como `application/postscript` y se valida la firma real (PostScript `%!PS` o EPS binario para DOS `C5 D0 D3 C6`). Código de error nuevo `attachment-eps-invalid`.

Sin script SQL (el de la 1.3.0 sigue siendo necesario).

## [1.3.0] — 2026-11-24

### Agregado
- Anexos del PrintCard: contexto de adjunto `PrintCardAnnex`, `PrintCardVersion.AnnexAttachmentIds` (máx. 10 por versión), `annexAttachmentIds` en `POST /npis/{id}/printcards` y `POST /npis/{id}/printcard-annexes` (Diseño agrega anexos a la versión vigente no rechazada; no toca aprobaciones). Los anexos no son evidencia: se pueden eliminar y se quitan de su versión. Bitácora `PrintCardAnnexesAdded`.

**Script SQL:** `scripts/migracion-2026-11-24-anexos-printcard.sql` (columna `AnnexAttachmentIds` en `PrintCardVersions`). Correrlo ANTES de publicar: sin él, el detalle de cualquier NPI con PrintCard da 500.

## [1.2.0] — 2026-11-23

### Agregado
- Módulo PrintCards (pedido del admin de Diseño): `GET /api/printcards/report` y `/report/export` (Excel). Resumen del área, indicadores por diseñador e historial con versiones. Tiempo activo = horas hábiles del cronómetro de Diseño hasta el primer envío al cliente; días naturales = de la asignación a ese envío. Política `PrintCardsReport`: AdminTotal, AdminDiseno, UsuarioDiseno (sólo lo suyo) y Director.

Sin script SQL.

## [1.1.0] — 2026-11-20

### Agregado
- `GET /api/version` (abierto): versión publicada del API, para que el front la compare con la suya.

Sin script SQL.

## [1.0.0] — 2026-11-20

Línea base del versionado: todo lo que estaba en `main` ("4ta version") más lo siguiente.

### Agregado
- Partir de un NPI anterior: liga "Basado en" y campos que cambiaron (script `migracion-2026-11-11-npi-basado-en.sql`).
- Catálogo "COLOR CINTA UNIONES" (script `migracion-2026-11-12-catalogo-color-cinta.sql`).
- Automatización pasos 6-8: sugerencias de orden de venta y PT desde SAP y documentos de Advance (apagado por omisión).
- Cambio del cliente tras el PT (Ingeniería decide el PT, Diseño el PC).
- Reemplazar PrintCard aprobado y actualizar PC final.
- Mi equipo de Comercial: director ↔ vendedores (script `migracion-2026-11-18-director-comercial.sql`).
- Tipo de wicket (script `migracion-2026-11-18-tipo-wicket.sql`).
- Archivos (fotos/PDF) en el chat del NPI (script `migracion-2026-11-19-archivos-en-chat.sql`).
- Admin de Ingeniería también decide cierres por no factibilidad de Diseño.
- El vendedor puede partir de NPIs enviados de su director comercial (`GET /npis?copySources=true`).
- Búsqueda por folio en los buscadores de NPIs ("NPI-0080", "0080" u "80").
- Archivos de Illustrator en adjuntos.

### Corregido
- Solicitud de Ingeniería abierta tras la factibilidad bloqueaba el PrintCard (NPI-0037).
- Rechazo de artes abierto tras la factibilidad de Diseño bloqueaba el PrintCard (NPI-0080): Diseño cierra su rechazo y valida artes en un paso.
- La liga "Basado en" se perdía al partir de un NPI del director.

### Scripts SQL de esta versión (correr en producción ANTES de publicar, los que falten)
- `scripts/migracion-2026-11-11-npi-basado-en.sql`
- `scripts/migracion-2026-11-12-catalogo-color-cinta.sql`
- `scripts/migracion-2026-11-18-director-comercial.sql`
- `scripts/migracion-2026-11-18-tipo-wicket.sql`
- `scripts/migracion-2026-11-19-archivos-en-chat.sql`
