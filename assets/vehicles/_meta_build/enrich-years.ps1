$ErrorActionPreference = 'Stop'
$ua = @{ 'User-Agent' = 'SnowRunnerTuningShop/1.0 (local metadata build)' }
$api = 'https://spintires.fandom.com/api.php'
$assets = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$rawPath = Join-Path $PSScriptRoot 'scrape-raw.json'
$metaPath = Join-Path $assets 'metadata.json'
$flagDir = Join-Path $assets 'flags'

function Invoke-WikiJson([string]$query) {
    Invoke-RestMethod -Uri "$api`?$query" -Headers $ua
}

function Get-Wikitext([string]$title) {
    $t = [uri]::EscapeDataString($title)
    (Invoke-WikiJson "action=parse&page=$t&prop=wikitext&format=json").parse.wikitext.'*'
}

function Parse-YearRange([string]$wt) {
    if (-not $wt) { return @{ From = $null; To = $null } }
    if ($wt -notmatch '(?i)\|year\s*=\s*(.*?)(?=\||\n|\}\})') {
        return @{ From = $null; To = $null }
    }
    $raw = ($Matches[1] -replace '<[^>]+>', '').Trim()
    # 1953-1972 / 1953–1972 / 1953 - 1972 / 1953–present
    if ($raw -match '(?i)(\d{4})\s*[–—-]\s*(present|now|current|\d{4})') {
        $from = [int]$Matches[1]
        $to = if ($Matches[2] -match '^\d{4}$') { [int]$Matches[2] } else { $null }
        return @{ From = $from; To = $to }
    }
    if ($raw -match '(\d{4})') {
        $y = [int]$Matches[1]
        return @{ From = $y; To = $y }
    }
    return @{ From = $null; To = $null }
}

# Manual year overrides when wiki is empty / wrong (production of Based On / lore year)
$yearOverrides = @{
    'kodiak'                 = @{ From = 1980; To = 2009 }   # Chevrolet Kodiak
    'm916a1'                 = @{ From = 1978; To = $null }  # Freightliner M916
    'gmc_8000'               = @{ From = 1978; To = 1988 }   # GMC Brigadier
    'loadstar'               = @{ From = 1962; To = 1979 }
    'chevrolet-apache'       = @{ From = 1958; To = 1961 }
    'jcj7r'                  = @{ From = 1976; To = 1986 }
    'jw'                     = @{ From = 1986; To = $null }  # Wrangler ongoing generations
    'landroverdefender110'   = @{ From = 1983; To = 2016 }
    'landroverdefender90'    = @{ From = 1984; To = 2016 }
    'rezvani-hercules-6x6'   = @{ From = 2019; To = $null }
    'd53233'                 = @{ From = 1959; To = 1990 }   # Voron ~ KrAZ/Ural era placeholder from wiki when fixed
}

$yearCachePath = Join-Path $PSScriptRoot 'years-cache.json'
$yearCache = @{}
if (Test-Path $yearCachePath) {
    foreach ($row in (Get-Content $yearCachePath -Raw | ConvertFrom-Json)) {
        $yearCache[$row.id] = $row
    }
}

$rawById = @{}
foreach ($r in (Get-Content $rawPath -Raw | ConvertFrom-Json)) {
    $rawById[$r.id] = $r
}

$meta = Get-Content $metaPath -Raw | ConvertFrom-Json
$updated = New-Object System.Collections.Generic.List[object]
$i = 0
foreach ($v in $meta.vehicles) {
    $i++
    $from = $null
    $to = $null
    $title = $null
    if ($rawById.ContainsKey($v.id)) { $title = $rawById[$v.id].wikiTitle }

    # Prefer existing year fields if already present in metadata
    if ($null -ne $v.yearFrom) {
        $from = $v.yearFrom
        $to = $v.yearTo
        Write-Host "[$i] $($v.id) from metadata $from-$to"
    }
    elseif ($yearOverrides.ContainsKey($v.id)) {
        $from = $yearOverrides[$v.id].From
        $to = $yearOverrides[$v.id].To
        Write-Host "[$i] $($v.id) override $from-$to"
    }
    elseif ($yearCache.ContainsKey($v.id) -and $null -ne $yearCache[$v.id].yearFrom) {
        $from = $yearCache[$v.id].yearFrom
        $to = $yearCache[$v.id].yearTo
        Write-Host "[$i] $($v.id) cache $from-$to"
    }
    elseif ($title) {
        Write-Host "[$i] $($v.id) fetch $title"
        try {
            $wt = Get-Wikitext $title
            Start-Sleep -Milliseconds 120
            $yr = Parse-YearRange $wt
            $from = $yr.From
            $to = $yr.To
        }
        catch {
            Write-Host "  fail $($_.Exception.Message)"
        }
    }

    $countryCode = $v.countryCode
    $countryName = $v.countryName
    # Historical: Russian-lineage vehicles introduced before 1991 → USSR
    if ($countryCode -eq 'RU' -and $from -ne $null -and $from -lt 1991) {
        $countryCode = 'SU'
        $countryName = 'USSR'
    }

    $updated.Add([pscustomobject]@{
            id             = $v.id
            manufacturerId = $v.manufacturerId
            basedOn        = $v.basedOn
            yearFrom       = $from
            yearTo         = $to
            countryCode    = $countryCode
            countryName    = $countryName
        }) | Out-Null
}

# Ensure USSR flag (flagcdn uses 'su')
$suFlag = Join-Path $flagDir 'su.png'
if (-not (Test-Path $suFlag) -or (Get-Item $suFlag).Length -lt 50) {
    Write-Host "download SU flag"
    $flagUrls = @(
        'https://flagcdn.com/48x36/su.png',
        'https://flagcdn.com/w40/su.png',
        'https://flagsapi.com/SU/flat/64.png',
        'https://upload.wikimedia.org/wikipedia/commons/thumb/a/a9/Flag_of_the_Soviet_Union.svg/250px-Flag_of_the_Soviet_Union.svg.png'
    )
    $ok = $false
    foreach ($url in $flagUrls) {
        try {
            Invoke-WebRequest -Uri $url -Headers $ua -OutFile $suFlag -UseBasicParsing
            if ((Test-Path $suFlag) -and (Get-Item $suFlag).Length -gt 100) {
                $ok = $true
                break
            }
        }
        catch {
            Write-Host "  flag source failed: $url"
        }
    }
    if (-not $ok) {
        Write-Host "WARNING: could not download SU flag; continuing without it"
    }
}

$countryMap = @{}
foreach ($v in $updated) {
    if (-not $v.countryCode) { continue }
    $countryMap[$v.countryCode] = [string]$v.countryName
}
foreach ($c in @($meta.countries)) {
    if (-not $countryMap.ContainsKey($c.code)) {
        $countryMap[$c.code] = [string]$c.name
    }
}
$countryMap['SU'] = 'USSR'

$countryList = New-Object System.Collections.Generic.List[object]
foreach ($code in ($countryMap.Keys | Sort-Object)) {
    $countryList.Add([pscustomobject]@{
            code     = [string]$code
            name     = [string]$countryMap[$code]
            flagFile = ('flags/{0}.png' -f $code.ToLowerInvariant())
        }) | Out-Null
}

$vehicleList = New-Object System.Collections.Generic.List[object]
foreach ($v in $updated) {
    $vehicleList.Add([pscustomobject]@{
            id             = [string]$v.id
            manufacturerId = $(if ($v.manufacturerId) { [string]$v.manufacturerId } else { $null })
            basedOn        = $(if ($v.basedOn) { [string]$v.basedOn } else { $null })
            yearFrom       = $v.yearFrom
            yearTo         = $v.yearTo
            countryCode    = $(if ($v.countryCode) { [string]$v.countryCode } else { $null })
            countryName    = $(if ($v.countryName) { [string]$v.countryName } else { $null })
        }) | Out-Null
}

# Cache years for faster rebuilds
$updated | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $PSScriptRoot 'years-cache.json') -Encoding UTF8

$out = [pscustomobject]@{
    version       = 2
    source        = 'spintires.fandom.com Infobox (Based On / Manufacturer / Year); country from Based On; USSR if RU lineage and yearFrom < 1991'
    manufacturers = $meta.manufacturers
    countries     = $countryList
    vehicles      = $vehicleList
}

$out | ConvertTo-Json -Depth 6 | Set-Content $metaPath -Encoding UTF8

$withYear = @($vehicleList | Where-Object { $null -ne $_.yearFrom }).Count
$ussr = @($vehicleList | Where-Object { $_.countryCode -eq 'SU' }).Count
Write-Host ""
Write-Host "Wrote $metaPath"
Write-Host "with year: $withYear / $($vehicleList.Count); USSR: $ussr"
Write-Host "missing year:"
$vehicleList | Where-Object { $null -eq $_.yearFrom } | ForEach-Object { $_.id } | Select-Object -First 40
