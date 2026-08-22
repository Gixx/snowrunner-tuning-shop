$ErrorActionPreference = 'Stop'
$ua = @{ 'User-Agent' = 'SnowRunnerTuningShop/1.0 (local metadata build)' }
$api = 'https://spintires.fandom.com/api.php'
$assets = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# script lives in assets/vehicles/_meta_build
$assets = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logoDir = Join-Path $assets 'manufacturers'
$flagDir = Join-Path $assets 'flags'
$rawPath = Join-Path $PSScriptRoot 'scrape-raw.json'
New-Item -ItemType Directory -Force -Path $logoDir, $flagDir | Out-Null

function Invoke-WikiJson([string]$query) {
    Invoke-RestMethod -Uri "$api`?$query" -Headers $ua
}

function Get-Wikitext([string]$title) {
    $t = [uri]::EscapeDataString($title)
    (Invoke-WikiJson "action=parse&page=$t&prop=wikitext&format=json").parse.wikitext.'*'
}

function Get-FileUrl([string]$fileName) {
    $t = [uri]::EscapeDataString("File:$fileName")
    $r = Invoke-WikiJson "action=query&titles=$t&prop=imageinfo&iiprop=url&format=json"
    foreach ($p in @($r.query.pages.PSObject.Properties.Value)) {
        if ($p.imageinfo) { return $p.imageinfo[0].url }
    }
    return $null
}

function Ensure-Logo([string]$logoSource) {
    if (-not $logoSource) { return $null }
    $ext = [IO.Path]::GetExtension($logoSource)
    if (-not $ext) { $ext = '.png' }
    $slugBase = [IO.Path]::GetFileNameWithoutExtension($logoSource).ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $destName = "$slugBase$ext"
    $dest = Join-Path $logoDir $destName
    if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 100) {
        $url = Get-FileUrl $logoSource
        Start-Sleep -Milliseconds 120
        if ($url) {
            Invoke-WebRequest -Uri $url -Headers $ua -OutFile $dest -UseBasicParsing
        }
    }
    if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 100) {
        return "manufacturers/$destName"
    }
    return $null
}

function Parse-BasedOn([string]$wt) {
    if ($wt -match '(?i)\|based_on\s*=\s*(.*?)(?=\||\n|\}\})') {
        $raw = $Matches[1].Trim()
        if ($raw -match '\[\[(?:\:?wikipedia\:)?([^\|\]]+)\|([^\]]+)\]\]') { return $Matches[2].Trim() }
        if ($raw -match '\[https?://[^\s\]]+\s+([^\]]+)\]') { return $Matches[1].Trim() }
        if ($raw -match '\[\[(?:\:?wikipedia\:)?([^\|\]]+)\]\]') { return ($Matches[1] -replace '_', ' ').Trim() }
        return ($raw -replace '<[^>]+>', '').Trim()
    }
    return $null
}

function Parse-Manufacturer([string]$wt) {
    if ($wt -match '(?i)\|manufacturer\s*=\s*(.*?)(?=\||\n|\}\})') {
        $raw = $Matches[1].Trim()
        $file = $null
        $name = $null
        if ($raw -match '\[\[File:([^\|\]]+)') { $file = $Matches[1].Trim() }
        if ($raw -match 'link=([^\|\]]+)') { $name = $Matches[1].Trim() }
        return @{ File = $file; Name = $name }
    }
    return @{ File = $null; Name = $null }
}

function Guess-Country([string]$basedOn, [string]$mfg, [string]$displayName) {
    $t = "$basedOn $mfg $displayName".ToLowerInvariant()
    $rules = @(
        @{ Code = 'US'; Name = 'United States'; Patterns = @('chevrolet', 'ford', 'gmc', 'gm ', 'international', 'navistar', 'kenworth', 'mack', 'western star', 'freightliner', 'pacific', 'caterpillar', 'cat ', 'hummer', 'scout', 'loadstar', 'paystar', 'fleetstar', 'transtar', 'kodiak', 'ck1500', 'apache', 'rezvani', 'aramatsu', 'oshkosh', 'earthroamer', 'jeep', 'neo falcon', 'force gurkha') },
        @{ Code = 'RU'; Name = 'Russia'; Patterns = @('kamaz', 'kam az', 'ural', 'zil', 'gaz-', 'gaz ', 'maz', 'azov', 'tuz', 'step', 'kolob', 'dan ', 'yar', 'khan', 'kirovets', 'tayga', 'voron', 'don ', 'bandit', 'tatarin', 'burlak', 'ank', 'tonar', 'yamal', 'trekol', 'zikz') },
        @{ Code = 'CZ'; Name = 'Czech Republic'; Patterns = @('tatra') },
        @{ Code = 'DE'; Name = 'Germany'; Patterns = @('mercedes', 'unimog', 'man ', 'zetros', 'faun', 'claas') },
        @{ Code = 'SE'; Name = 'Sweden'; Patterns = @('volvo', 'scania') },
        @{ Code = 'JP'; Name = 'Japan'; Patterns = @('hino', 'mitsubishi', 'toyota') },
        @{ Code = 'IT'; Name = 'Italy'; Patterns = @('astra', 'iveco') },
        @{ Code = 'FR'; Name = 'France'; Patterns = @('renault', 'berliet') },
        @{ Code = 'GB'; Name = 'United Kingdom'; Patterns = @('land rover', 'landrover', 'defender', 'leyland', 'scammell') },
        @{ Code = 'NL'; Name = 'Netherlands'; Patterns = @('daf ') },
        @{ Code = 'BE'; Name = 'Belgium'; Patterns = @('mol ') },
        @{ Code = 'CA'; Name = 'Canada'; Patterns = @('bomber') },
        @{ Code = 'UA'; Name = 'Ukraine'; Patterns = @('kraz', 'kryukov', 'zaz', 'zaporozhets') },
        @{ Code = 'CN'; Name = 'China'; Patterns = @('faw', 'sinotruk', 'dongfeng', 'jangsu') },
        @{ Code = 'PL'; Name = 'Poland'; Patterns = @('jelcz', 'star ') },
        @{ Code = 'IN'; Name = 'India'; Patterns = @('mahindra') },
        @{ Code = 'PT'; Name = 'Portugal'; Patterns = @('umm ', 'alter') },
        @{ Code = 'FI'; Name = 'Finland'; Patterns = @('sisu', 'valmet') },
        @{ Code = 'AU'; Name = 'Australia'; Patterns = @('hendrickson') }
    )
    foreach ($rule in $rules) {
        foreach ($k in $rule.Patterns) {
            if ($t.Contains($k)) {
                return @{ Code = $rule.Code; Name = $rule.Name }
            }
        }
    }
    return @{ Code = ''; Name = '' }
}

$overrides = @{
    'mr230'        = @{ basedOn = 'Berliet GBC 8x8'; manufacturerName = 'Mercer'; manufacturerLogoSource = 'Mercer_logo.png'; countryCode = 'FR'; countryName = 'France' }
    'mk520'        = @{ basedOn = 'Berliet GBC 8'; manufacturerName = 'Mercer'; countryCode = 'FR'; countryName = 'France' }
    'jrx600'       = @{ basedOn = 'Dongfeng EQ2100'; manufacturerName = 'Jangsu'; countryCode = 'CN'; countryName = 'China' }
    'aac58dw'      = @{ basedOn = 'UMM Alter II'; manufacturerName = 'AAC'; countryCode = 'PT'; countryName = 'Portugal' }
    'smfk816e'     = @{ basedOn = 'MOL TB800'; manufacturerName = 'Sleiter'; countryCode = 'BE'; countryName = 'Belgium' }
    'sst833c'      = @{ basedOn = 'MOL F 7066'; manufacturerName = 'Sleiter'; countryCode = 'BE'; countryName = 'Belgium' }
    'hibbm816'     = @{ basedOn = 'DAF F241 Series'; manufacturerName = 'HIB'; countryCode = 'NL'; countryName = 'Netherlands' }
    'hibb1980'     = @{ basedOn = 'Leyland DAF T244 Expedition'; manufacturerName = 'HIB'; countryCode = 'GB'; countryName = 'United Kingdom' }
    'aa15'         = @{ basedOn = 'Hendrickson B-Series Prime Mover'; manufacturerName = 'AVENHORN'; countryCode = 'AU'; countryName = 'Australia' }
    'p450'         = @{ basedOn = 'Faun Goliath 8x8'; manufacturerName = 'PLAD'; countryCode = 'DE'; countryName = 'Germany' }
    'p440b'        = @{ basedOn = 'Faun L912 SA'; manufacturerName = 'PLAD'; countryCode = 'DE'; countryName = 'Germany' }
    'a1160'        = @{ basedOn = 'Valmet 1502'; manufacturerName = 'Ankatra'; countryCode = 'FI'; countryName = 'Finland' }
    'f7290ra'      = @{ basedOn = 'Claas Xerion 5000'; manufacturerName = 'Futom'; countryCode = 'DE'; countryName = 'Germany' }
    'femm7at'      = @{ basedOn = 'Yamal B-6M'; manufacturerName = 'FEMM'; countryCode = 'RU'; countryName = 'Russia' }
    'boar'         = @{ basedOn = 'TONAR-7502'; manufacturerName = 'BOAR'; countryCode = 'RU'; countryName = 'Russia' }
    '3194'         = @{ basedOn = 'Oshkosh M911'; manufacturerName = 'Derry Longhorn'; countryCode = 'US'; countryName = 'United States' }
    '4520'         = @{ basedOn = 'Oshkosh HET A0 M1070A0'; manufacturerName = 'Derry Longhorn'; countryCode = 'US'; countryName = 'United States' }
    'derryspecial' = @{ basedOn = 'Oshkosh M1120A4'; manufacturerName = 'Derry Longhorn'; countryCode = 'US'; countryName = 'United States' }
    'bm17'         = @{ basedOn = 'Scammell S24'; manufacturerName = 'Royal'; countryCode = 'GB'; countryName = 'United Kingdom' }
    'elti'         = @{ basedOn = 'EarthRoamer LTi'; manufacturerName = 'EarthRoamer'; countryCode = 'US'; countryName = 'United States' }
    'esx'          = @{ basedOn = 'EarthRoamer SX'; manufacturerName = 'EarthRoamer'; countryCode = 'US'; countryName = 'United States' }
    'by4'          = @{ basedOn = 'ZAZ-965A "Zaporozhets"'; manufacturerName = 'Gor'; countryCode = 'UA'; countryName = 'Ukraine' }
    'mtb8106rg'    = @{ basedOn = 'Force Gurkha'; manufacturerName = 'MTB'; countryCode = 'IN'; countryName = 'India' }
    'nf2000'       = @{ basedOn = 'Mahindra Bolero'; manufacturerName = 'Neo'; countryCode = 'IN'; countryName = 'India' }
    'yar87'        = @{ basedOn = 'TREKOL-39294'; manufacturerName = 'YAR'; countryCode = 'RU'; countryName = 'Russia' }
}

$raw = Get-Content $rawPath -Raw | ConvertFrom-Json
$vehicles = New-Object System.Collections.Generic.List[object]
$countries = @{}
$manufacturers = @{}

foreach ($row in $raw) {
    $basedOn = $row.basedOn
    $mfgName = $row.manufacturerName
    $logoSource = $row.manufacturerLogoSource
    $logoFile = $row.manufacturerLogoFile
    $cc = $row.countryCode
    $cn = $row.countryName

    if (-not $basedOn -and $row.wikiTitle) {
        Write-Host "refetch $($row.id) ($($row.wikiTitle))"
        try {
            $wt = Get-Wikitext $row.wikiTitle
            Start-Sleep -Milliseconds 150
            $basedOn = Parse-BasedOn $wt
            $m = Parse-Manufacturer $wt
            if ($m.File) { $logoSource = $m.File }
            if ($m.Name) { $mfgName = $m.Name }
        }
        catch {
            Write-Host "  fail: $($_.Exception.Message)"
        }
    }

    if ($overrides.ContainsKey($row.id)) {
        $o = $overrides[$row.id]
        if ($o.basedOn) { $basedOn = $o.basedOn }
        if ($o.manufacturerName) { $mfgName = $o.manufacturerName }
        if ($o.manufacturerLogoSource) { $logoSource = $o.manufacturerLogoSource }
        if ($o.countryCode) { $cc = $o.countryCode; $cn = $o.countryName }
    }

    if ($logoSource) {
        $logoFile = Ensure-Logo $logoSource
    }

    if (-not $cc) {
        $g = Guess-Country $basedOn $mfgName $row.displayName
        $cc = $g.Code
        $cn = $g.Name
    }

    if ($mfgName) {
        $mfgName = ($mfgName -replace '(?i)\s*logo$', '' -replace '(?i)^logo\s*', '').Trim()
    }

    if ($cc) { $countries[$cc] = $cn }

    $mfgId = $null
    if ($logoFile -or $mfgName) {
        if ($mfgName) {
            $mfgId = ($mfgName.ToLowerInvariant() -replace '[^a-z0-9]+', '-')
        }
        else {
            $mfgId = [IO.Path]::GetFileNameWithoutExtension($logoFile)
        }
        if (-not $manufacturers.ContainsKey($mfgId)) {
            $display = if ($mfgName) { $mfgName } else { $mfgId }
            $manufacturers[$mfgId] = @{ id = $mfgId; name = $display; logoFile = $logoFile }
        }
        elseif ($logoFile -and -not $manufacturers[$mfgId].logoFile) {
            $manufacturers[$mfgId].logoFile = $logoFile
        }
    }

    $vehicles.Add([ordered]@{
            id             = $row.id
            manufacturerId = $mfgId
            basedOn        = $basedOn
            countryCode    = $cc
            countryName    = $cn
        }) | Out-Null
}

foreach ($code in ($countries.Keys | Sort-Object)) {
    $lc = $code.ToLowerInvariant()
    $dest = Join-Path $flagDir "$lc.png"
    if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 50) {
        $url = "https://flagcdn.com/w80/$lc.png"
        Write-Host "flag $lc"
        try {
            Invoke-WebRequest -Uri $url -Headers $ua -OutFile $dest -UseBasicParsing
        }
        catch {
            Write-Host "  flag fail $lc"
        }
    }
}

$mfgList = New-Object System.Collections.Generic.List[object]
foreach ($m in ($manufacturers.Values | Sort-Object { $_.id })) {
    $mfgList.Add([pscustomobject]@{
            id       = [string]$m.id
            name     = [string]$m.name
            logoFile = $(if ($m.logoFile) { [string]$m.logoFile } else { $null })
        }) | Out-Null
}

$countryList = New-Object System.Collections.Generic.List[object]
foreach ($entry in ($countries.GetEnumerator() | Sort-Object { $_.Value })) {
    $countryList.Add([pscustomobject]@{
            code     = [string]$entry.Key
            name     = [string]$entry.Value
            flagFile = ('flags/{0}.png' -f $entry.Key.ToLowerInvariant())
        }) | Out-Null
}

$vehicleList = New-Object System.Collections.Generic.List[object]
foreach ($v in $vehicles) {
    $vehicleList.Add([pscustomobject]@{
            id             = [string]$v.id
            manufacturerId = $(if ($v.manufacturerId) { [string]$v.manufacturerId } else { $null })
            basedOn        = $(if ($v.basedOn) { [string]$v.basedOn } else { $null })
            countryCode    = $(if ($v.countryCode) { [string]$v.countryCode } else { $null })
            countryName    = $(if ($v.countryName) { [string]$v.countryName } else { $null })
        }) | Out-Null
}

$meta = [pscustomobject]@{
    version       = 1
    source        = 'spintires.fandom.com Infobox Vehicles (Based On / Manufacturer); country inferred from Based On'
    manufacturers = $mfgList
    countries     = $countryList
    vehicles      = $vehicleList
}

$metaPath = Join-Path $assets 'metadata.json'
$meta | ConvertTo-Json -Depth 6 | Set-Content $metaPath -Encoding UTF8

Write-Host ""
Write-Host "Wrote $metaPath"
Write-Host "vehicles: $($vehicleList.Count) basedOn: $((@($vehicleList | Where-Object basedOn)).Count) country: $((@($vehicleList | Where-Object countryCode)).Count)"
Write-Host "manufacturers: $($mfgList.Count) logos: $((Get-ChildItem $logoDir).Count) flags: $((Get-ChildItem $flagDir).Count)"
Write-Host "missing basedOn:"
$vehicleList | Where-Object { -not $_.basedOn } | ForEach-Object { $_.id }
Write-Host "missing country:"
$vehicleList | Where-Object { -not $_.countryCode } | ForEach-Object { "$($_.id) | $($_.basedOn)" }
