# Preparar la demo (Fase 8 — corrección)

Instrucciones para levantar el backend y el frontend contra una base de datos **dedicada a la
demo** (`NpiManagement_Demo`), separada de la base de desarrollo habitual
(`NpiManagement.Dev`), con migraciones aplicadas **a mano** — nunca automáticamente — y sin
ningún secreto en archivos versionados ni en logs.

## 0. Requisitos

- .NET SDK 10.0
- SQL Server accesible: LocalDB (Windows, más simple para una demo local) o cualquier instancia
  de SQL Server/Docker a la que tengas acceso
- Node.js 18+ y npm (para el frontend)

## 1. Crear la base de datos `NpiManagement_Demo`

No hace falta crearla con una herramienta aparte — `dotnet ef database update` (paso 4) la crea
sola si no existe, siempre que la cadena de conexión apunte a un servidor accesible y el usuario
tenga permiso de crear bases. Si prefieres crearla explícitamente antes:

**LocalDB:**
```powershell
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE NpiManagement_Demo"
```

**SQL Server / Docker:**
```bash
sqlcmd -S localhost -U sa -P "<tu-contraseña>" -Q "CREATE DATABASE NpiManagement_Demo"
```

## 2. Configurar la cadena de conexión (User Secrets, no en archivos versionados)

Desde `src/NpiManagement.Api`:

```bash
cd src/NpiManagement.Api
dotnet user-secrets init   # ya está inicializado (ver UserSecretsId en el .csproj) — no falla si se repite

# LocalDB:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=NpiManagement_Demo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

# SQL Server con usuario/contraseña:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=NpiManagement_Demo;User Id=<usuario>;Password=<contraseña>;TrustServerCertificate=True"
```

Esto **sobreescribe** (sólo en tu máquina, fuera del repositorio) el valor de
`appsettings.Development.json`, que sigue apuntando a `NpiManagement.Dev` para el trabajo diario —
así nunca mezclas datos de demo con datos de desarrollo normales.

## 3. Configurar los secretos obligatorios (User Secrets — nunca en appsettings ni en logs)

La aplicación **no arranca** sin `Jwt:SigningKey`/`Jwt:Issuer`/`Jwt:Audience`, y el seed de
Development **no crea usuarios** sin `Seed:UsersPassword` (falla con un error explícito en vez de
generar o mostrar una contraseña — ver `DevelopmentDataSeeder.cs`):

```bash
dotnet user-secrets set "Jwt:Issuer" "NpiManagement"
dotnet user-secrets set "Jwt:Audience" "NpiManagement.Clients"
dotnet user-secrets set "Jwt:SigningKey" "<genera-una-clave-aleatoria-de-al-menos-32-caracteres>"
dotnet user-secrets set "Seed:UsersPassword" "<elige-una-contraseña-que-cumpla-la-política-de-abajo>"
```

**`Seed:UsersPassword` debe cumplir la política por defecto de ASP.NET Core Identity** (se aplica
tal cual a los 10 usuarios semilla): al menos 8 caracteres, una mayúscula, una minúscula, un
dígito y un carácter no alfanumérico. `12345678` o `password123` **no sirven** — Identity las
rechaza y el arranque falla con un error explícito (antes de esta corrección fallaba en
silencio y el síntoma era un confuso "credenciales inválidas" al hacer login). Ejemplo válido:
`Demo#2026`.

Sugerencia para generar una clave de firma JWT aleatoria (criptográficamente segura, sin depender
de nada externo):
```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```
No uses una clave "a mano" ni repitas el mismo carácter — una clave predecible permite falsificar
tokens válidos.

Verifica lo que quedó guardado (nunca imprime nada por sí solo salvo que lo pidas):
```bash
dotnet user-secrets list
```

Ninguno de estos valores debe copiarse jamás a `appsettings.json`, `appsettings.Development.json`,
`.env`, ni a ningún commit. `dotnet user-secrets` los guarda fuera del repositorio (en tu perfil
de usuario de Windows).

## 4. Aplicar migraciones manualmente

Por defecto, `Database:ApplyMigrationsOnStartup=false` — el backend **no** migra la base de datos
al arrancar (ver `appsettings.json`), en ningún entorno salvo que lo actives explícitamente, y
nunca fuera de Development sin importar ese valor. Aplícalas tú mismo antes del primer arranque:

```bash
cd NpisBack/NpisBack   # raíz de la solución
dotnet tool restore    # instala dotnet-ef fijado en dotnet-tools.json (no se instala global)
dotnet tool run dotnet-ef database update `
  --project src/NpiManagement.Infrastructure `
  --startup-project src/NpiManagement.Api
```
(En bash, usa `\` en vez de la comilla invertida `` ` `` para continuar la línea.)

Esto crea todas las tablas en `NpiManagement_Demo` usando la cadena de conexión configurada en el
paso 2. Vuelve a ejecutar el mismo comando cada vez que haya una migración nueva — nunca ocurre
solo.

## 5. Iniciar el backend

```bash
cd src/NpiManagement.Api
dotnet run --launch-profile https
```

Al arrancar en Development, el backend siembra roles/usuarios/catálogos/cotizaciones mínimos en
`NpiManagement_Demo` (usando la contraseña del paso 3 — su valor nunca aparece en la consola).
Queda disponible en `https://localhost:7086` (Swagger en `/swagger`, salud en `/health` y
`/health/ready`).

## 6. Iniciar el frontend

```bash
cd Npis-Front/npi-launchpad
cp .env.example .env.local   # ya apunta a https://localhost:7086/api; ajusta el puerto si usaste otro
npm install
npm run dev
```

Abre `http://localhost:8080` e inicia sesión con cualquiera de los correos semilla (ver
`src/NpiManagement.Infrastructure/Seed/DevelopmentDataSeeder.cs` para la lista completa, p. ej.
`evelazquez@bioflex.mx` para Admin Total) y la contraseña que configuraste en el paso 3.

## 7. Almacenamiento de adjuntos en el servidor (Production)

`appsettings.Production.json` ya apunta `FileStorage:RootPath` a
`C:\Users\DESARROLLOS\Documents\NpisAttachment` — esa carpeta se creó en el disco local del
servidor donde corre el backend (no una ruta de red; el backend accede a ella localmente). Solo
tiene efecto cuando la app corre con `ASPNETCORE_ENVIRONMENT=Production`; en Development (tu
máquina, `dotnet run`) sigue usando la ruta por defecto bajo el temporal de Windows, sin cambios.

Antes de publicar y arrancar en ese servidor:
- La identidad con la que corre el Application Pool de IIS necesita permiso de lectura/escritura
  NTFS sobre esa carpeta — por defecto, la carpeta `Documents` de un usuario **no** es accesible
  para otras identidades (incluida `ApplicationPoolIdentity`), así que hay que agregar el permiso
  explícitamente (botón derecho → Propiedades → Seguridad → Editar → agregar `IIS AppPool\<nombre
  del pool>` o la cuenta de servicio real, con Modificar/Escritura).
- El recurso compartido de red (`\\LEX\NpisAttachment`) que también se creó sobre esa misma
  carpeta es solo para acceso manual/administrativo (revisar archivos a mano desde otra máquina);
  la aplicación nunca lee ni escribe a través de esa ruta de red, siempre usa la ruta local
  `C:\Users\DESARROLLOS\Documents\NpisAttachment` directamente en el mismo servidor.
- Los archivos se guardan con un nombre generado por el servidor (GUID) dentro de subcarpetas de
  2 caracteres — no son legibles a simple vista ni corresponden al nombre original del archivo;
  identificar a qué NPI/aprobación pertenece cada uno solo es posible desde la aplicación
  (tabla `Attachment` en la base de datos), nunca navegando la carpeta a mano.

## Notas de seguridad para la demo

- Nunca compartas capturas de pantalla de `dotnet user-secrets list` ni del archivo
  `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` — aunque vive fuera del
  repositorio, sigue siendo texto plano en tu disco.
- Si la demo se hace en una máquina distinta a la tuya, repite los pasos 2–3 ahí — los User
  Secrets no viajan con el repositorio ni con `git clone`.
- Al terminar la demo, si quieres limpiar: `sqlcmd -S ... -Q "DROP DATABASE NpiManagement_Demo"`.
  No se borra automáticamente.
