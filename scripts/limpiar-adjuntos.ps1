<#
  Vacía la carpeta de adjuntos (archivos de NPIs y capturas de reportes) después de limpiar la base
  con reset-datos-produccion.sql. Se corre en el servidor de la API (172.16.10.31).

  Por defecto sólo muestra cuántos archivos hay; no borra nada. Con -Confirmar hace primero una
  copia comprimida en el Escritorio y luego vacía la carpeta, sin borrar la carpeta en sí (la API
  la sigue usando y su verificación de salud comprueba que exista y se pueda escribir).

  Uso:
    .\limpiar-adjuntos.ps1              # ensayo
    .\limpiar-adjuntos.ps1 -Confirmar   # respaldo + borrado
#>
param(
    [string]$Carpeta = 'C:\Users\DESARROLLOS\Documents\NpisAttachment',
    [switch]$Confirmar
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Carpeta -PathType Container)) {
    throw "No existe la carpeta $Carpeta. Revisa FileStorage:RootPath en la configuración de la API."
}

$archivos = @(Get-ChildItem -LiteralPath $Carpeta -Recurse -File -Force)
$bytes = ($archivos | Measure-Object -Property Length -Sum).Sum
$megas = [math]::Round(([double]$bytes) / 1MB, 2)
Write-Host "$($archivos.Count) archivos ($megas MB) en $Carpeta"

if (-not $Confirmar) {
    Write-Host 'ENSAYO: no se borró nada. Vuelve a ejecutar con -Confirmar para respaldar y vaciar la carpeta.'
    return
}

if ($archivos.Count -gt 0) {
    $respaldo = Join-Path ([Environment]::GetFolderPath('Desktop')) ('NpisAttachment-respaldo-{0:yyyyMMdd-HHmm}.zip' -f (Get-Date))
    Compress-Archive -Path (Join-Path $Carpeta '*') -DestinationPath $respaldo
    Write-Host "Respaldo creado: $respaldo"
}

Get-ChildItem -LiteralPath $Carpeta -Force | Remove-Item -Recurse -Force
Write-Host 'Carpeta vacía. No hace falta reiniciar la API.'
