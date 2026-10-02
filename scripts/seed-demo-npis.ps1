<#
.SYNOPSIS
    Crea 3 NPIs de ejemplo en distintas etapas del proceso, para usar como atajos en una demo en
    vivo, sin tener que esperar los tiempos reales de aprobacion.

.DESCRIPTION
    NPI A - recien enviado, esperando factibilidad de Ingenieria y Diseno.
    NPI B - PrintCard enviado al cliente y el cliente pidio cambios (muestra el panel nuevo).
    NPI C - ciclo completo de principio a fin (Completed).

.EXAMPLE
    ./scripts/seed-demo-npis.ps1 -BaseUrl "http://172.16.10.31:7086/api" -Password "Demo#2026"
#>
param(
    [string]$BaseUrl = "http://172.16.10.31:7086/api",
    [string]$CommercialEmail = "evelazquez@bioflex.mx",
    [Parameter(Mandatory = $true)]
    [string]$Password
)

$ErrorActionPreference = "Stop"
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host -NoNewline ("  {0,-50}" -f $Name)
    try {
        $result = & $Action
        Write-Host " OK" -ForegroundColor Green
        return $result
    } catch {
        Write-Host " FALLO" -ForegroundColor Red
        Write-Host "     $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body, [int[]]$ExpectStatus = @(200, 201, 204))
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Uri = "$BaseUrl$Path"; Method = $Method; Headers = $headers; UseBasicParsing = $true }
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
        "%PDF-1.4 evidencia demo`n%%EOF",
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

function New-CaptureBase {
    return @{
        numeroCotizacion = ""; cliente = ""; tipoProducto = ""; descripcion = ""; descripcionProducto = ""
        codigoProdCliente = ""; contactoCompras = ""; contactoClienteDiseno = ""; ejecutivoVenta = ""
        formato = ""; acabado = ""; mercado = ""; subMercado = ""; unidadVenta = ""; tipoEntrada = ""
        categoriaSAP = ""; tipoImpresion = ""; tipoTinta = ""; tipoEmpaque = ""
        ancho = ""; alto = ""; fuelleExtendido = ""; solapaPestana = ""; fuelles = ""
        piTipoPelicula = ""; terminadoPI = ""; piCalibre = ""; formulaLDPE_PI = ""
        pplTipoPelicula = ""; terminadoPPL = ""; pplCalibre = ""; formulaLDPE_PPL = ""
        pslTipoPelicula = ""; terminadoPSL = ""; pslCalibre = ""; formulaLDPE_PSL = ""
        colgadera = ""; cierre = ""; otrosElementos = ""; sello = ""; perforaciones = ""
        numeroPerforaciones = ""; diametroPerforaciones = ""
        muesca = "NO"; precorte = "NO"; patronPerforado = "NO"; parcheRefuerzo = "NO"; suaje = "NO"; aplicaPieDeCasa = "NO"
        figuraEmbob = "N/A"; diametroBobina = ""; pesoBobina = ""; bobinasPorTarima = ""; alturaTarimaMaxBobina = ""
        maxUniones = ""; core = ""; taponCore = "NO"; destareCore = "NO"; dimTarimaBobina = ""
        piezasCajaBulto = ""; ligas = "NO"; cajaBultoTarima = ""; dimTarimaBolsa = ""; alturaTarimaMaxBolsa = ""
        notasEmpaqueEmbalaje = ""
        basePantone = "NO"; baseMuestra = "NO"; visitaCliente = "NO"; pruebaColor = "NO"
        archivosEditables = "NO"; muestrasFisicas = "NO"; especificaciones = "NO"; otros = "NO"
        costoPreprensa = ""; observaciones = ""
    }
}

Write-Host "=== Creando 3 NPIs de ejemplo para demo - $BaseUrl ===" -ForegroundColor Cyan

$token = Step "Login ($CommercialEmail)" {
    (Invoke-Api -Method Post -Path "/auth/login" -Body @{ email = $CommercialEmail; password = $Password }).accessToken
}
$engineerId = Step "Listar ingenieros asignables" { (Invoke-Api -Method Get -Path "/users?area=Ingenieria" -Token $token)[0].id }
$designerId = Step "Listar disenadores asignables" { (Invoke-Api -Method Get -Path "/users?area=Diseno" -Token $token)[0].id }

# ============================================================
# NPI A - recien enviado, esperando factibilidad
# ============================================================
Write-Host "`n--- NPI A: recien enviado (CAYEF / POUCH cafe) ---" -ForegroundColor Yellow
$capA = New-CaptureBase
$capA.numeroCotizacion = "1001"; $capA.cliente = "CAYEF"; $capA.tipoProducto = "POUCH"
$capA.descripcion = "Cafe instantaneo gourmet - empaque premium"
$capA.descripcionProducto = "Bolsa POUCH para cafe instantaneo, presentacion individual"
$capA.ejecutivoVenta = "LUIS VEGA"; $capA.formato = "STANDUP POUCH (SELLO K)"
$capA.acabado = "IMPRESO"; $capA.mercado = "ALIMENTO SECO"; $capA.subMercado = "REPOSTERIA"
$capA.unidadVenta = "PZAS"; $capA.tipoEntrada = "PRODUCTO NUEVO"
$capA.categoriaSAP = "LAMINADOS BOLSA STAND UP POUCH"
$capA.tipoImpresion = "EXTERNA (FRENTE)"; $capA.tipoTinta = "FRENTE + BARNIZ"; $capA.tipoEmpaque = "PRIMARIO"
$capA.ancho = "273"; $capA.alto = "180"
$capA.piTipoPelicula = "BOPP"; $capA.terminadoPI = "METALIZADO"; $capA.piCalibre = "37"; $capA.formulaLDPE_PI = "COMERCIAL"
$capA.observaciones = "Ejemplo para demo - recien enviado, en espera de factibilidad."

$npiA = Step "Crear borrador NPI A" {
    Invoke-Api -Method Post -Path "/npis" -Token $token -ExpectStatus @(201) -Body @{ capture = $capA; priority = "Alta"; preAssignedEngineeringUserId = $engineerId }
}
Step "Enviar NPI A" { Invoke-Api -Method Post -Path "/npis/$npiA/submit" -Token $token } | Out-Null
$npiADetail = Step "Leer NPI A" { Invoke-Api -Method Get -Path "/npis/$npiA" -Token $token }

# ============================================================
# NPI B - PrintCard enviado, cliente pide cambios
# ============================================================
Write-Host "`n--- NPI B: cliente pide cambios (BABY CAT / BOLSA) ---" -ForegroundColor Yellow
$capB = New-CaptureBase
$capB.numeroCotizacion = "1002"; $capB.cliente = "BABY CAT"; $capB.tipoProducto = "BOLSA"
$capB.descripcion = "Bolsa para aditamento Baby Cat"
$capB.descripcionProducto = "Bolsa impresa para aditamento de arena para gato"
$capB.ejecutivoVenta = "SANDRA GOMEZ"; $capB.formato = "BOLSA ZIPPER"
$capB.acabado = "IMPRESO"; $capB.mercado = "NO ALIMENTO SECO"; $capB.subMercado = "OTRO"
$capB.unidadVenta = "PZAS"; $capB.tipoEntrada = "PRODUCTO NUEVO"
$capB.categoriaSAP = "PE BOLSA ZIPPER"
$capB.tipoImpresion = "EXTERNA (FRENTE)"; $capB.tipoTinta = "OTRA"; $capB.tipoEmpaque = "PRIMARIO"
$capB.ancho = "243"; $capB.alto = "300"
$capB.piTipoPelicula = "CPP"; $capB.terminadoPI = "BLANCO"; $capB.piCalibre = "37.5"; $capB.formulaLDPE_PI = "COMERCIAL"
$capB.cierre = "ZIPPER"; $capB.otrosElementos = "WICKET"; $capB.sello = "LATERAL"
$capB.observaciones = "Ejemplo para demo - cliente pidio cambios al PrintCard."

$npiB = Step "Crear borrador NPI B" {
    Invoke-Api -Method Post -Path "/npis" -Token $token -ExpectStatus @(201) -Body @{ capture = $capB; priority = "Media"; preAssignedEngineeringUserId = $engineerId }
}
Step "Enviar NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/submit" -Token $token } | Out-Null
Step "Asignar Diseno NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/assign-design" -Token $token -Body @{ userId = $designerId } } | Out-Null
Step "Factibilidad Ingenieria NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/engineering-feasibility" -Token $token -Body @{ note = "Factible, sin observaciones" } } | Out-Null
Step "Factibilidad Diseno NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/design-feasibility" -Token $token -Body @{ note = "Factible" } } | Out-Null
Step "Artes Validadas NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/validate-artwork" -Token $token } | Out-Null
Step "Crear PrintCard NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/printcards" -Token $token -Body @{ code = "PC-1002" } } | Out-Null
Step "Aprobar PrintCard Diseno NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/printcard-approvals" -Token $token -Body @{ role = "Design" } } | Out-Null
Step "Aprobar PrintCard Ingenieria NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/printcard-approvals" -Token $token -Body @{ role = "Engineering" } } | Out-Null
Step "Enviar PrintCard a Cliente NPI B" { Invoke-Api -Method Post -Path "/npis/$npiB/send-printcard-to-client" -Token $token } | Out-Null
Step "Cliente pide cambios NPI B" {
    Invoke-Api -Method Post -Path "/npis/$npiB/request-client-changes" -Token $token -Body @{
        description = "El cliente pide cambiar el tono de verde del logo y mover el codigo de barras a la parte trasera de la bolsa."
    }
} | Out-Null
$npiBDetail = Step "Leer NPI B" { Invoke-Api -Method Get -Path "/npis/$npiB" -Token $token }

# ============================================================
# NPI C - ciclo completo
# ============================================================
Write-Host "`n--- NPI C: ciclo completo (GRUPO BAZSTO / BOLSA) ---" -ForegroundColor Yellow
$capC = New-CaptureBase
$capC.numeroCotizacion = "1005"; $capC.cliente = "GRUPO BAZSTO"; $capC.tipoProducto = "BOLSA"
$capC.descripcion = "KER-092 1LB Natures Gold"
$capC.descripcionProducto = "Bolsa de camiseta para producto natural 1LB"
$capC.ejecutivoVenta = "GODOFREDO QUESADA"; $capC.formato = "BOLSA SELLO LATERAL"
$capC.acabado = "IMPRESO"; $capC.mercado = "ALIMENTO SECO"; $capC.subMercado = "GRANOS Y SEMILLAS"
$capC.unidadVenta = "KG"; $capC.tipoEntrada = "PRODUCTO NUEVO"
$capC.categoriaSAP = "PE BOLSA SELLO LATERAL"
$capC.tipoImpresion = "EXTERNA (FRENTE)"; $capC.tipoTinta = "LAMINACION"; $capC.tipoEmpaque = "PRIMARIO"
$capC.ancho = "230"; $capC.alto = "350"
$capC.piTipoPelicula = "CPP"; $capC.terminadoPI = "TRANSPARENTE"; $capC.piCalibre = "32"; $capC.formulaLDPE_PI = "COMERCIAL"
$capC.cierre = "ZIPPER"; $capC.otrosElementos = "OTRO"; $capC.sello = "LATERAL"
$capC.observaciones = "Ejemplo para demo - ciclo completo."

$npiC = Step "Crear borrador NPI C" {
    Invoke-Api -Method Post -Path "/npis" -Token $token -ExpectStatus @(201) -Body @{ capture = $capC; priority = "Media"; preAssignedEngineeringUserId = $engineerId }
}
Step "Enviar NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/submit" -Token $token } | Out-Null
Step "Asignar Diseno NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/assign-design" -Token $token -Body @{ userId = $designerId } } | Out-Null
Step "Factibilidad Ingenieria NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/engineering-feasibility" -Token $token -Body @{ note = "Factible" } } | Out-Null
Step "Factibilidad Diseno NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/design-feasibility" -Token $token -Body @{ note = "Factible" } } | Out-Null
Step "Artes Validadas NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/validate-artwork" -Token $token } | Out-Null
Step "Crear PrintCard NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/printcards" -Token $token -Body @{ code = "PC-1005" } } | Out-Null
Step "Aprobar PrintCard Diseno NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/printcard-approvals" -Token $token -Body @{ role = "Design" } } | Out-Null
Step "Aprobar PrintCard Ingenieria NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/printcard-approvals" -Token $token -Body @{ role = "Engineering" } } | Out-Null
Step "Enviar PrintCard a Cliente NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/send-printcard-to-client" -Token $token } | Out-Null
$evidenceId = Step "Subir evidencia aprobacion Cliente NPI C" { (Upload-Attachment -NpiId $npiC -Token $token -Context "ClientApproval").id }
Step "Aprobar PrintCard Cliente NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/printcard-approvals" -Token $token -Body @{ role = "Client"; evidenceAttachmentId = $evidenceId } } | Out-Null
Step "Cargar PrintCard en Sistema NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/load-printcard" -Token $token } | Out-Null
Step "Registrar PT en SAP NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/register-sap-pt" -Token $token -Body @{ codigoPT = "PT-DEMO01"; ptName = "Demo Ingenieria" } } | Out-Null
Step "Registrar Pedido en SAP NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/register-sap-order" -Token $token -Body @{ pedidoSapNumber = "4500099999" } } | Out-Null
Step "Registrar ET NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/register-et" -Token $token } | Out-Null
Step "Cargar ET en SisPro NPI C" { Invoke-Api -Method Post -Path "/npis/$npiC/load-et-sispro" -Token $token } | Out-Null
$npiCDetail = Step "Leer NPI C" { Invoke-Api -Method Get -Path "/npis/$npiC" -Token $token }

Write-Host "`n=== Listo ===" -ForegroundColor Cyan
Write-Host ("NPI A (recien enviado):        {0}  [{1}]" -f $npiADetail.npiNumber, $npiA)
Write-Host ("NPI B (cliente pide cambios):  {0}  [{1}]" -f $npiBDetail.npiNumber, $npiB)
Write-Host ("NPI C (ciclo completo):        {0}  [{1}]  Estado: {2}" -f $npiCDetail.npiNumber, $npiC, $npiCDetail.lifecycleStatus)
