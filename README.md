# NpiManagement — backend de Gestión de NPIs

API en ASP.NET Core (.NET 10) para el sistema de gestión de introducción de nuevos productos
(NPI) de Bioflex, construida en Clean Architecture (Domain → Application → Infrastructure → Api)
para servir al frontend existente en `../Npis-Front/npi-launchpad` (no incluido en este
repositorio). Ver `docs/` para el diseño completo: `backend-analysis.md`, `domain-model.md`,
`workflow.md`, `authorization-matrix.md`, `api-contract.md`, `architecture-decisions.md`,
`frontend-integration.md`, `assumptions-and-ambiguities.md`. Para preparar una demo con una base
de datos y secretos propios (migraciones manuales, `NpiManagement_Demo`), ver `docs/demo-setup.md`.

## Requisitos

- .NET SDK 10.0 (`dotnet --version`)
- SQL Server local para desarrollo: LocalDB (Windows, viene con Visual Studio/SQL Server Express)
  o cualquier instancia de SQL Server accesible
- Docker Desktop (opcional pero recomendado) — habilita las pruebas de integración reales contra
  SQL Server vía Testcontainers; sin Docker, esas pruebas se **omiten** automáticamente (nunca
  fallan ni se sustituyen por una base en memoria)

## Estructura

```
src/
  NpiManagement.Domain          — entidades, value objects, invariantes; cero dependencias externas
  NpiManagement.Application     — casos de uso (comandos/queries), DTOs, autorización, validación
  NpiManagement.Infrastructure  — EF Core + SQL Server, Identity, JWT, storage de archivos
  NpiManagement.Api             — controladores delgados, composición (Program.cs)
tests/
  NpiManagement.Domain.Tests        — 98 pruebas, sin infraestructura
  NpiManagement.Application.Tests   — 64 pruebas, con fakes (sin EF/HTTP)
  NpiManagement.IntegrationTests    — SQL Server real vía Testcontainers (se omiten sin Docker)
docs/                            — diseño y decisiones (ver arriba)
```

## Configuración local

1. **Cadena de conexión** — `src/NpiManagement.Api/appsettings.Development.json` ya apunta a
   LocalDB por defecto (`(localdb)\MSSQLLocalDB`, base `NpiManagement.Dev`). Para usar otra
   instancia, sobreescribe `ConnectionStrings:DefaultConnection` con User Secrets o una variable
   de entorno — nunca la pongas en un archivo versionado.

2. **JWT** (obligatorio, la app no arranca sin esto) — configura `Jwt:Issuer`, `Jwt:Audience` y
   `Jwt:SigningKey` (mínimo 32 caracteres) con User Secrets:
   ```
   cd src/NpiManagement.Api
   dotnet user-secrets set "Jwt:Issuer" "NpiManagement"
   dotnet user-secrets set "Jwt:Audience" "NpiManagement.Clients"
   dotnet user-secrets set "Jwt:SigningKey" "una-clave-aleatoria-de-al-menos-32-caracteres"
   ```

3. **Contraseña de usuarios semilla** (opcional) — sin configurar, el seeder de Development
   genera una contraseña temporal y la imprime una vez en el log de arranque:
   ```
   dotnet user-secrets set "Seed:UsersPassword" "algo-que-tu-elijas"
   ```

4. Ver `.env.example` en la raíz del repo para el listado completo de claves de configuración
   (incluye `FileStorage:*` y `Cors:AllowedOrigins`, ambas con valores por defecto razonables en
   Development).

## Ejecutar

```
dotnet restore
dotnet build NpiManagement.sln
dotnet run --project src/NpiManagement.Api
```

En Development, al arrancar la API se siembran roles/usuarios/catálogos/cotizaciones mínimos
(`NpiManagement.Infrastructure.Seed.DevelopmentDataSeeder`) — esto **nunca** ocurre fuera de
Development. Las migraciones **no** se aplican automáticamente (`Database:ApplyMigrationsOnStartup=false`
por defecto, y nunca se activa fuera de Development sin importar ese valor) — aplícalas a mano
primero (ver "Migraciones" abajo) o activa el flag sólo si sabes lo que haces. Swagger queda
disponible en `/swagger`; health checks en `/health` (liveness) y `/health/ready` (DB +
almacenamiento de adjuntos).

## Migraciones

`dotnet-ef` está fijado como herramienta local (`dotnet-tools.json`), no instalada globalmente:

```
dotnet tool restore
dotnet tool run dotnet-ef migrations add NombreDeLaMigracion --project src/NpiManagement.Infrastructure --startup-project src/NpiManagement.Api
dotnet tool run dotnet-ef database update --project src/NpiManagement.Infrastructure --startup-project src/NpiManagement.Api
```

Nunca se ejecutan migraciones contra una base que no sea local y explícitamente configurada por
quien las corre.

## Pruebas

```
dotnet test NpiManagement.sln
```

Las pruebas de `NpiManagement.IntegrationTests` que requieren SQL Server usan
`Testcontainers.MsSql` y se auto-detectan: si Docker no está disponible, se **omiten** (nunca se
reemplazan por una base en memoria ni hacen fallar la corrida). Con Docker disponible, se
levantan y destruyen automáticamente un contenedor `mssql/server:2022-latest` por ejecución.

Sin Docker (p. ej. preparando una demo), usa `scripts/smoke-test-full-flow.ps1` — recorre el
flujo completo real por HTTP (crear NPI → enviar → asignaciones → factibilidades → PrintCard con
sus 3 aprobaciones → SAP → ET → `Completed`) contra cualquier backend ya corriendo, local o de
demo. No sustituye la suite de xUnit, la complementa donde Docker no está disponible:
```
./scripts/smoke-test-full-flow.ps1 -Password "<tu Seed:UsersPassword>"
```

## Usuarios semilla (sólo Development)

Los mismos 10 correos que `Npis-Front/npi-launchpad/src/data/users.ts`, con los roles nativos de
ASP.NET Core Identity (`AdminTotal`, `AdminComercial`, `AdminIngenieria`, `AdminDiseno`,
`UsuarioComercial`, `UsuarioIngenieria`, `UsuarioDiseno`) — ver `DevelopmentDataSeeder.cs` para el
listado exacto. La contraseña es la que configures en `Seed:UsersPassword` — es obligatoria: si
falta, o si no cumple la política de contraseñas de Identity (8+ caracteres, mayúscula,
minúscula, dígito y carácter no alfanumérico), el arranque falla con un error explícito en vez de
generar o mostrar una contraseña.

## Seguridad — resumen

- Sin registro público; los usuarios se crean por seed (Development) o administrativamente.
- Contraseñas nunca en texto plano (`PasswordHasher` de Identity); bloqueo tras 5 intentos
  fallidos por cuenta + límite de 10 solicitudes/minuto por IP en `/api/auth/login` y `/refresh`.
- JWT de vida corta + rotación de refresh tokens con detección de reuso (revoca toda la sesión).
- Ningún endpoint confía en `role`/`area`/`userId`/fechas enviados por el cliente — siempre
  salen del token validado o de `IClock`.
- Adjuntos: validación de firma binaria real (no sólo `Content-Type`/extensión declarados),
  `StorageKey` siempre generado por el servidor (nunca a partir del nombre de archivo del
  cliente) — path traversal descartado por diseño.
- CORS restringido a orígenes explícitamente configurados; encabezados de endurecimiento en toda
  respuesta; HSTS fuera de Development; logging de requests sin encabezados ni cuerpos.

Ver `docs/architecture-decisions.md` para el razonamiento completo detrás de cada decisión.
