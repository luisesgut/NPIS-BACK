<#
.SYNOPSIS
    Prueba de humo del flujo completo de un NPI, de extremo a extremo, contra un backend real
    (local o de demo) por HTTP, sin Docker ni Testcontainers.

.DESCRIPTION
    Corre el mismo recorrido que docs/workflow.md documenta:
    crear borrador -> enviar -> asignar diseno -> factibilidades (independientes) ->
    Artes Validadas -> PrintCard (version + 3 aprobaciones, con evidencia real para el cliente) ->
    cargar PrintCard en sistema -> PT en SAP -> Pedido en SAP -> ET -> ET cargada en SisPro
    -> LifecycleStatus Completed. Al final agrega un comentario y confirma que
    /audit, /timer-events y /api/dashboard/summary respondan.

    Existe porque las pruebas de integracion automatizadas (xUnit + Testcontainers) se OMITEN
    en cualquier maquina sin Docker. Este script cubre el mismo camino end-to-end contra
    cualquier SQL Server real alcanzable (LocalDB, demo, etc.) para poder correrlo antes de una
    demo sin depender de Docker. No sustituye la suite de xUnit, es un complemento para
    entornos donde esa suite no puede ejecutarse.

.PARAMETER BaseUrl
    Raiz de la API (sin barra final). Por defecto, el perfil https local.

.PARAMETER CommercialEmail
    Correo de un usuario con acceso total (AdminTotal recomendado: ve y actua en todas las areas
    sin necesitar asignaciones cruzadas). Ver DevelopmentDataSeeder.cs para la lista de correos.

.PARAMETER Password
    Contrasena de todos los usuarios semilla (la que configuraste en Seed:UsersPassword).
    Nunca se imprime ni se registra en ningun archivo.

.EXAMPLE
    ./scripts/smoke-test-full-flow.ps1 -Password "Demo#2026"
#>
param(
    [string]$BaseUrl = "https://localhost:7086/api",
    [string]$CommercialEmail = "evelazquez@bioflex.mx",
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$script:StepCount = 0
$script:Failed = $false

function Step {
    param([string]$Name, [scriptblock]$Action)
    $script:StepCount++
    Write-Host -NoNewline ("[{0,2}] {1,-45}" -f $script:StepCount, $Name)
    try {
        $result = & $Action
        Write-Host " OK" -ForegroundColor Green
        return $result
    } catch {
        Write-Host " FALLO" -ForegroundColor Red
        Write-Host "     $($_.Exception.Message)" -ForegroundColor Red
        $script:Failed = $true
        throw
    }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{
        Uri             = "$BaseUrl$Path"
        Method          = $Method
        Headers         = $headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
        $params["ContentType"] = "application/json"
    }
    try {
        $response = Invoke-WebRequest @params
    } catch {
        $status = $_.Exception.Response.StatusCode.value__
        $errBody = (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
        throw "HTTP $status en $Method $Path : $errBody"
    }
    if ($ExpectStatus -notcontains $response.StatusCode) {
        throw "Se esperaba $($ExpectStatus -join '/') pero se obtuvo $($response.StatusCode) en $Method $Path : $($response.Content)"
    }
    if ($response.Content) { return $response.Content | ConvertFrom-Json }
    return $null
}

function Upload-Attachment {
    param([string]$NpiId, [string]$Token, [string]$Context)
    $boundary = [System.Guid]::NewGuid().ToString()
    $bodyLines = @(
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"evidencia.pdf`"",
        "Content-Type: application/pdf",
        "",
        "%PDF-1.4 evidencia generada por smoke-test-full-flow.ps1`n%%EOF",
        "--$boundary",
        "Content-Disposition: form-data; name=`"context`"",
        "",
        $Context,
        "--$boundary--",
        ""
    ) -join "`r`n"
    $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/npis/$NpiId/attachments" -Method Post `
        -Headers @{Authorization = "Bearer $Token" } `
        -ContentType "multipart/form-data; boundary=$boundary" -Body $bodyLines
    return ($response.Content | ConvertFrom-Json)
}

Write-Host "=== Prueba de flujo completo - $BaseUrl ===" -ForegroundColor Cyan

$token = Step "Login ($CommercialEmail)" {
    (Invoke-Api -Method Post -Path "/auth/login" -Body @{ email = $CommercialEmail; password = $Password }).accessToken
}

$engineerId = Step "Listar ingenieros asignables" {
    $list = Invoke-Api -Method Get -Path "/users?area=Ingenieria" -Token $token
    if (-not $list -or $list.Count -eq 0) { throw "No hay usuarios en el area Ingenieria. Revisa el seed." }
    $list[0].id
}

$designerId = Step "Listar disenadores asignables" {
    $list = Invoke-Api -Method Get -Path "/users?area=Diseno" -Token $token
    if (-not $list -or $list.Count -eq 0) { throw "No hay usuarios en el area Diseno. Revisa el seed." }
    $list[0].id
}

$capture = @{
    numeroCotizacion = "SMK-$(Get-Date -Format 'yyyyMMddHHmmss')"; cliente = "Smoke Test"; tipoProducto = "OTRO"
    descripcion = "Prueba automatica de flujo completo"; descripcionProducto = "Prueba automatica"
    codigoProdCliente = "SMK"; contactoCompras = "smoke"; contactoClienteDiseno = "smoke"; ejecutivoVenta = "smoke"
    formato = "BOBINA PLANA"; acabado = "SIN IMPRESION"; mercado = "SECUNDARIO"; subMercado = "PANADERIA"
    tipoEntrada = "PRODUCTO NUEVO"; categoriaSAP = "LAMINADOS"; tipoImpresion = "EXTERNA (FRENTE)"
    tipoTinta = "FRENTE + BARNIZ"; tipoEmpaque = "SECUNDARIO"; ancho = "100"; alto = "12"
    fuelleExtendido = ""; solapaPestana = ""; fuelles = "INFERIOR"; piTipoPelicula = "300"; terminadoPI = "BLANCO"
    piCalibre = "100"; formulaLDPE_PI = "LAMINACION"; pplTipoPelicula = ""; terminadoPPL = ""; pplCalibre = ""
    formulaLDPE_PPL = ""; pslTipoPelicula = ""; terminadoPSL = ""; pslCalibre = ""; formulaLDPE_PSL = ""
    colgadera = "RINON"; cierre = "CINTA"; otrosElementos = ""; sello = "FONDO"; muesca = "NO"; precorte = "NO"
    patronPerforado = "NO"; parcheRefuerzo = "NO"; suaje = "NO"; aplicaPieDeCasa = "NO"; figuraEmbob = "N/A"
    diametroBobina = ""; pesoBobina = "100"; bobinasPorTarima = ""; alturaTarimaMaxBobina = ""; maxUniones = ""
    core = ""; taponCore = ""; dimTarimaBobina = ""; piezasCajaBulto = "1000"; cajaBultoTarima = ""
    dimTarimaBolsa = ""; alturaTarimaMaxBolsa = ""; basePantone = "NO"; baseMuestra = "NO"; visitaCliente = "NO"
    pruebaColor = "NO"; archivosEditables = "NO"; muestrasFisicas = "NO"; especificaciones = "NO"; otros = "NO"
    costoPreprensa = ""; observaciones = "Creado por scripts/smoke-test-full-flow.ps1"
}

$npiId = Step "Crear borrador (POST /npis)" {
    Invoke-Api -Method Post -Path "/npis" -Token $token -ExpectStatus @(201) -Body @{
        capture = $capture; priority = "Media"; preAssignedEngineeringUserId = $engineerId
    }
}

Step "Enviar (submit)" { Invoke-Api -Method Post -Path "/npis/$npiId/submit" -Token $token } | Out-Null
Step "Asignar Diseno" { Invoke-Api -Method Post -Path "/npis/$npiId/assign-design" -Token $token -Body @{ userId = $designerId } } | Out-Null
Step "Factibilidad de Diseno" { Invoke-Api -Method Post -Path "/npis/$npiId/design-feasibility" -Token $token -Body @{ note = "ok" } } | Out-Null
Step "Factibilidad de Ingenieria" { Invoke-Api -Method Post -Path "/npis/$npiId/engineering-feasibility" -Token $token -Body @{ note = "ok" } } | Out-Null
Step "Artes Validadas" { Invoke-Api -Method Post -Path "/npis/$npiId/validate-artwork" -Token $token } | Out-Null
Step "Crear version de PrintCard" { Invoke-Api -Method Post -Path "/npis/$npiId/printcards" -Token $token -Body @{ code = "PC-SMOKE" } } | Out-Null
Step "Aprobacion PrintCard - Diseno" { Invoke-Api -Method Post -Path "/npis/$npiId/printcard-approvals" -Token $token -Body @{ role = "Design" } } | Out-Null
Step "Aprobacion PrintCard - Ingenieria" { Invoke-Api -Method Post -Path "/npis/$npiId/printcard-approvals" -Token $token -Body @{ role = "Engineering" } } | Out-Null
Step "Enviar PrintCard al Cliente" { Invoke-Api -Method Post -Path "/npis/$npiId/send-printcard-to-client" -Token $token } | Out-Null

$evidenceId = Step "Subir evidencia de aprobacion del Cliente" { (Upload-Attachment -NpiId $npiId -Token $token -Context "ClientApproval").id }

Step "Aprobacion PrintCard - Cliente (con evidencia)" {
    Invoke-Api -Method Post -Path "/npis/$npiId/printcard-approvals" -Token $token -Body @{ role = "Client"; evidenceAttachmentId = $evidenceId }
} | Out-Null
Step "Cargar PrintCard en Sistema" { Invoke-Api -Method Post -Path "/npis/$npiId/load-printcard" -Token $token } | Out-Null
Step "Registrar PT en SAP" { Invoke-Api -Method Post -Path "/npis/$npiId/register-sap-pt" -Token $token -Body @{ codigoPT = "PT-SMOKE"; ptName = "Smoke Test" } } | Out-Null
Step "Registrar Pedido en SAP" { Invoke-Api -Method Post -Path "/npis/$npiId/register-sap-order" -Token $token -Body @{ pedidoSapNumber = "4500000000" } } | Out-Null
Step "Registrar ET" { Invoke-Api -Method Post -Path "/npis/$npiId/register-et" -Token $token } | Out-Null

$final = Step "Cargar ET en SisPro (cierra el ciclo)" { Invoke-Api -Method Post -Path "/npis/$npiId/load-et-sispro" -Token $token }

Step "Verificar LifecycleStatus = Completed" {
    if ($final.lifecycleStatus -ne "Completed") { throw "LifecycleStatus fue '$($final.lifecycleStatus)', se esperaba 'Completed'." }
}

Step "Agregar comentario" { Invoke-Api -Method Post -Path "/npis/$npiId/comments" -Token $token -ExpectStatus @(201) -Body @{ text = "Cerrado por smoke-test-full-flow.ps1" } } | Out-Null
Step "Consultar auditoria" { Invoke-Api -Method Get -Path "/npis/$npiId/audit" -Token $token } | Out-Null
Step "Consultar eventos de cronometro" { Invoke-Api -Method Get -Path "/npis/$npiId/timer-events" -Token $token } | Out-Null
Step "Consultar dashboard/summary" { Invoke-Api -Method Get -Path "/dashboard/summary" -Token $token } | Out-Null

Write-Host ""
if ($script:Failed) {
    Write-Host "RESULTADO: hubo fallas, ver arriba." -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: flujo completo OK. NPI $($final.npiNumber) ($npiId) llego a Completed." -ForegroundColor Green
    exit 0
}
