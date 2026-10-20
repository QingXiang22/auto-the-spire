param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDir
)

$ErrorActionPreference = 'Stop'
$taskValeVersion = '3.24.0'
$taskValeSha256 = '3a8884774973b6f2826877e1c6388a5feae4704ef88a9a9c912c77ce186990f3'
$taskValeArchive = Join-Path ([IO.Path]::GetTempPath()) ('vale-' + [guid]::NewGuid().ToString('N') + '.zip')
$taskValeUrl = "https://github.com/vale-cli/vale/releases/download/v$taskValeVersion/vale_${taskValeVersion}_Windows_64-bit.zip"

if (Test-Path -LiteralPath (Join-Path $InstallDir 'vale.exe')) {
    throw 'InstallDir already contains vale.exe; inspect the existing installation before replacing it.'
}
Invoke-WebRequest -Uri $taskValeUrl -OutFile $taskValeArchive
if ((Get-FileHash -LiteralPath $taskValeArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskValeSha256) {
    throw 'Vale archive checksum mismatch.'
}
Expand-Archive -LiteralPath $taskValeArchive -DestinationPath $InstallDir
& (Join-Path $InstallDir 'vale.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'Vale did not start successfully.' }
