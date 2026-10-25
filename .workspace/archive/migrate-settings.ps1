param([Parameter(Mandatory)][string]$FahrenheitDir)

$ErrorActionPreference = 'Stop'
$modules = @{
    'Fahrenheit.Mods.Qol.NoSplashModule' = 'fhqol.splash'
    'Fahrenheit.Mods.Qol.NoLauncherModule' = 'fhqol.launcher'
    'Fahrenheit.Mods.Qol.FocusInputModule' = 'fhqol.focus'
    'Fahrenheit.Mods.Qol.FastForwardModule' = 'fhqol.ff'
}
foreach ($module in $modules.Keys) {
    $path = Join-Path $FahrenheitDir ('config/global/fhqol/' + $module + '.json')
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $values = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    $legacyPrefix = $modules[$module] + '.'
    $changed = $false
    foreach ($key in @($values.Keys)) {
        if (-not $key.StartsWith($legacyPrefix, [StringComparison]::Ordinal)) { continue }
        $newKey = $module + '.' + $key.Substring($legacyPrefix.Length)
        if ($values.ContainsKey($newKey)) { continue }
        $values[$newKey] = $values[$key]
        $changed = $true
    }
    if (-not $changed) { continue }
    $backup = $path + '.alpha11.bak'
    if (-not (Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $path -Destination $backup }
    $temporary = $path + '.alpha12.tmp'
    [IO.File]::WriteAllText($temporary, ($values | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $path -Force
    Write-Host "Migrated settings: $module"
}
