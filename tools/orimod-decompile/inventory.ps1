# Extracts a content inventory from the decompiled OriMod source tree.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/orimod-decompile/inventory.ps1

param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Src = "reference\orimod\src"
)

$ErrorActionPreference = "Stop"
$src = Join-Path $Root $Src

function Get-Source([string]$path) { Get-Content -Raw -LiteralPath $path }

Write-Output "### CARDS"
Write-Output "file|class|base|cost|type|rarity|target|pool|flags"
$cardFiles = Get-ChildItem (Join-Path $src "Ori\Cards") -Recurse -Filter *.cs |
    Where-Object { $_.Name -notlike "Abstract*" -and $_.Name -notin @("OriCardTags.cs", "OriKeywords.cs") } |
    Sort-Object Name
foreach ($f in $cardFiles) {
    $t = Get-Source $f.FullName
    $cls = ([regex]::Match($t, 'public\s+(?:sealed\s+|abstract\s+|static\s+)?class\s+(\w+)')).Groups[1].Value
    $base = ([regex]::Match($t, 'class\s+\w+\s*:\s*(\w+)')).Groups[1].Value
    $ctor = [regex]::Match($t, ':\s*base\(\s*(-?\d+)\s*,\s*CardType\.(\w+)\s*,\s*CardRarity\.(\w+)\s*,\s*TargetType\.(\w+)')
    $cost = $ctor.Groups[1].Value; $type = $ctor.Groups[2].Value
    $rarity = $ctor.Groups[3].Value; $target = $ctor.Groups[4].Value
    $pool = ([regex]::Match($t, '\[Pool\(typeof\((\w+)\)\)\]')).Groups[1].Value
    $flags = @()
    if ($t -match 'MultiplayerOnly') { $flags += 'multiplayerOnly' }
    if ($t -match 'CardKeyword\.Exhaust') { $flags += 'exhaust' }
    if ($t -match 'CardKeyword\.Ethereal') { $flags += 'ethereal' }
    if ($t -match 'CardKeyword\.Retain') { $flags += 'retain' }
    if ($t -match 'CardKeyword\.Innate') { $flags += 'innate' }
    if ($t -match 'Unplayable') { $flags += 'unplayable' }
    if ($t -match 'OriCardTags\.(\w+)') { $flags += 'tag:' + (([regex]::Matches($t, 'OriCardTags\.(\w+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique) -join '+') }
    if ($t -match 'OriKeyword') { $flags += 'oriKeyword' }
    if ($t -match 'WithMultiplier|CalculationBaseVar|CalculatedDamageVar') { $flags += 'customCalc' }
    if ($t -match 'AloftSystem|EnemyAloftSystem') { $flags += 'aloft' }
    if ($t -match 'OriPlacement|Placement') { $flags += 'placement' }
    if ($t -match 'OriAbility') { $flags += 'ability' }
    if ($t -match 'Wisp') { $flags += 'wisp' }
    if ($t -match 'Ori\.Powers') { $flags += 'oriPower' }
    Write-Output "$($f.Name)|$cls|$base|$cost|$type|$rarity|$target|$pool|$($flags -join ',')"
}

Write-Output ""
Write-Output "### POWERS"
Write-Output "file|class|base|powerType|stackType|coupling"
$powerFiles = Get-ChildItem (Join-Path $src "Ori\Powers") -Recurse -Filter *.cs | Sort-Object Name
foreach ($f in $powerFiles) {
    $t = Get-Source $f.FullName
    $cls = ([regex]::Match($t, 'public\s+(?:sealed\s+|abstract\s+|static\s+)?class\s+(\w+)')).Groups[1].Value
    $base = ([regex]::Match($t, 'class\s+\w+\s*:\s*(\w+)')).Groups[1].Value
    $pt = ([regex]::Match($t, 'PowerType\.(\w+)')).Groups[1].Value
    $st = ([regex]::Match($t, 'PowerStackType\.(\w+)')).Groups[1].Value
    $c = @()
    if ($t -match 'AloftSystem|IAloftChangedListener') { $c += 'aloft' }
    if ($t -match 'OriPlacement') { $c += 'placement' }
    if ($t -match 'IOriAbility') { $c += 'ability' }
    if ($t -match 'Hook\.|HarmonyPatch') { $c += 'patchesGame' }
    if ($t -match 'override\s+.*(AfterSideTurnEnd|AfterTurnEnd|AfterCardPlayed|BeforeCardPlayed|AfterDamage)') { $c += 'lifecycleOverride' }
    Write-Output "$($f.Name)|$cls|$base|$pt|$st|$($c -join ',')"
}

Write-Output ""
Write-Output "### RELICS"
Write-Output "file|class|base|rarity|coupling"
$relicFiles = Get-ChildItem (Join-Path $src "Ori\Relics") -Recurse -Filter *.cs | Sort-Object Name
foreach ($f in $relicFiles) {
    $t = Get-Source $f.FullName
    $cls = ([regex]::Match($t, 'public\s+(?:sealed\s+|abstract\s+|static\s+)?class\s+(\w+)')).Groups[1].Value
    $base = ([regex]::Match($t, 'class\s+\w+\s*:\s*(\w+)')).Groups[1].Value
    $r = ([regex]::Match($t, 'RelicRarity\.(\w+)')).Groups[1].Value
    $c = @()
    if ($t -match 'AloftSystem') { $c += 'aloft' }
    if ($t -match 'OriAbility') { $c += 'ability' }
    if ($t -match 'OriPlacement') { $c += 'placement' }
    if ($t -match 'Wisp') { $c += 'wisp' }
    if ($t -match '\.Powers|OriKeyword') { $c += 'oriPower' }
    Write-Output "$($f.Name)|$cls|$base|$r|$($c -join ',')"
}

Write-Output ""
Write-Output "### PLACEMENTS"
Get-ChildItem (Join-Path $src "Ori\Placements") -Recurse -Filter *.cs |
    Where-Object { $_.Name -like "*PlacementState.cs" } | Sort-Object Name |
    ForEach-Object { Write-Output $_.BaseName }

Write-Output ""
Write-Output "### ABILITIES (handlers)"
Get-ChildItem (Join-Path $src "Ori\Abilities") -Recurse -Filter *AbilityHandler.cs | Sort-Object Name |
    ForEach-Object { Write-Output $_.BaseName }

Write-Output ""
Write-Output "### ABILITY CARDS"
Get-ChildItem (Join-Path $src "Ori\Abilities") -Recurse -Filter *AbilityCard.cs | Sort-Object Name |
    ForEach-Object { Write-Output $_.BaseName }

Write-Output ""
Write-Output "### WISPS"
Get-ChildItem (Join-Path $src "Ori\Wisps") -Recurse -Filter *.cs | Sort-Object Name |
    ForEach-Object { Write-Output $_.BaseName }

Write-Output ""
Write-Output "### CARD POOLS / KEYWORDS / TAGS"
Get-ChildItem (Join-Path $src "Ori\CardPools") -Filter *.cs | ForEach-Object { Write-Output $_.BaseName }
Write-Output "--- OriKeywords ---"
$kw = Get-Source (Join-Path $src "Ori\Cards\OriKeywords.cs")
[regex]::Matches($kw, '(?:static\s+readonly\s+\w+\s+)?(\w+)\s*=\s*new\s+CardKeyword') | ForEach-Object { Write-Output $_.Groups[1].Value }
Write-Output "--- OriCardTags ---"
$tg = Get-Source (Join-Path $src "Ori\Cards\OriCardTags.cs")
[regex]::Matches($tg, '(\w+)\s*=\s*new\s+CardTag') | ForEach-Object { Write-Output $_.Groups[1].Value }
