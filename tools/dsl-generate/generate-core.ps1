param([string]$Java = 'java')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$version = '4.13.1'
$cache = Join-Path $repoRoot '.audit/antlr'
$jar = Join-Path $cache "antlr-$version-complete.jar"
$expectedHash = 'BC13A9C57A8DD7D5196888211E5EDE657CB64A3CE968608697E4F668251A8487'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if (!(Test-Path -LiteralPath $jar)) {
    Invoke-WebRequest -Uri "https://www.antlr.org/download/antlr-$version-complete.jar" -OutFile $jar
}
if ((Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'ANTLR tool checksum mismatch; generated files have not been changed.'
}
$sourceDir = Join-Path $repoRoot 'core-the-spire/Core.TheSpire/Engine/Compilation'
$output = Join-Path $sourceDir 'Generated'
Push-Location $repoRoot
try {
    & $Java -jar $jar -Dlanguage=CSharp -package CoreTheSpire.Engine.Compilation.Generated -no-listener -no-visitor -Xexact-output-dir -o $output 'core-the-spire/Core.TheSpire/Engine/Compilation/CoreSpire.g4'
    if ($LASTEXITCODE -ne 0) { throw "ANTLR generation failed ($LASTEXITCODE)." }
    foreach ($file in Get-ChildItem -LiteralPath $output -Filter '*.cs') {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $lines = $text -split '\r?\n' | ForEach-Object { $_.TrimEnd() }
        $text = $lines -join "`n"
        $text = "#pragma warning disable 3021 // ANTLR emits CLS attributes into a non-CLS assembly.`n" + $text
        [System.IO.File]::WriteAllText($file.FullName, $text, [System.Text.UTF8Encoding]::new($false))
    }
} finally { Pop-Location }
Write-Output "Generated CoreSpire with ANTLR $version. Normal builds need only the pinned .NET runtime package."
