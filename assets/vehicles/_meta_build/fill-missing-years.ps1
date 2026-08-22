$ErrorActionPreference = 'Stop'
$metaPath = Join-Path $PSScriptRoot '..\metadata.json'
$m = Get-Content $metaPath -Raw | ConvertFrom-Json

$years = @{
    'fclt9000'                 = @{ From = 1977; To = 1991; BasedOn = 'Ford CLT-9000' }
    'gmh9500'                  = @{ From = 1966; To = 1978; BasedOn = 'GMC MH-9500' }
    'transtar'                 = @{ From = 1971; To = 1986; BasedOn = 'International Transtar 4070A' }
    'fleetstar'                = @{ From = 1962; To = 1977; BasedOn = 'International Fleetstar F-2070A' }
    'international_hx520'      = @{ From = 2016; To = $null; BasedOn = 'International HX520' }
    'pstd4'                    = @{ From = 2003; To = 2012; BasedOn = 'Astra HHD8 86.48' }
    'wws4964'                  = @{ From = 1967; To = 1981; BasedOn = 'White Western Star 4964' }
    'azov-73210'               = @{ From = 2019; To = $null; BasedOn = 'KamAZ K5 (K5340)' }
    '96320'                    = @{ From = 1990; To = $null; BasedOn = 'BAZ-69092' }
    '3194'                     = @{ From = 1976; To = 1990; BasedOn = 'Oshkosh M911' }
    '74760'                    = @{ From = 2013; To = $null; BasedOn = 'MZKT-741351' }
    '74941'                    = @{ From = 2013; To = $null; BasedOn = 'MZKT-741350' }
    'navistar'                 = @{ From = 1988; To = 2015; BasedOn = 'Navistar 5000-MV' }
    'pp16'                     = @{ From = 1943; To = 1945; BasedOn = 'Pacific M26/P16' }
    'pp512pf'                  = @{ From = 1971; To = 1982; BasedOn = 'Pacific P512' }
    'western_star_47x_nf_1424' = @{ From = 2021; To = $null; BasedOn = 'Western Star 47X' }
    'western_star_47x_nf_1430' = @{ From = 2021; To = $null; BasedOn = 'Western Star 47X' }
    '5319_nara'                = @{ From = 2005; To = $null; BasedOn = 'KamAZ-6560' }
    '64131'                    = @{ From = 2008; To = $null; BasedOn = 'KamAZ-65228' }
    '114sd'                    = @{ From = 2011; To = $null; BasedOn = 'Freightliner 114SD' }
    'paystar'                  = @{ From = 1973; To = 2016; BasedOn = 'International Paystar 5070' }
    'mr230'                    = @{ From = 1959; To = 1977; BasedOn = 'Berliet GBC 8x8' }
    'bm17'                     = @{ From = 1980; To = 1988; BasedOn = 'Scammell S24' }
    'grad'                     = @{ From = 2015; To = $null; BasedOn = 'Ural Next' }
    'ff750'                    = @{ From = 2000; To = $null; BasedOn = 'Ford F-750 Super Duty' }
    'by4'                      = @{ From = 1960; To = 1969; BasedOn = 'ZAZ-965A Zaporozhets' }
    'h2'                       = @{ From = 2005; To = 2009; BasedOn = 'Hummer H2 SUT' }
    'khan_sentinel'            = @{ From = 2014; To = $null; BasedOn = 'UAZ-23632 Patriot Pickup' }
}

foreach ($v in $m.vehicles) {
    if (-not $years.ContainsKey($v.id)) { continue }
    $y = $years[$v.id]
    $v.yearFrom = $y.From
    $v.yearTo = $y.To
    if ($y.BasedOn) { $v.basedOn = $y.BasedOn }

    if ($v.countryCode -eq 'RU' -and $null -ne $v.yearFrom -and $v.yearFrom -lt 1991) {
        $v.countryCode = 'SU'
        $v.countryName = 'USSR'
    }
}

$hasSu = $false
foreach ($c in $m.countries) {
    if ($c.code -eq 'SU') { $hasSu = $true }
}
if (-not $hasSu) {
    $m.countries = @($m.countries) + @([pscustomobject]@{
            code     = 'SU'
            name     = 'USSR'
            flagFile = 'flags/su.png'
        })
}

$m | ConvertTo-Json -Depth 8 | Set-Content $metaPath -Encoding UTF8
$missing = @($m.vehicles | Where-Object { $null -eq $_.yearFrom })
Write-Host "still missing: $($missing.Count)"
$missing | ForEach-Object { $_.id }
Write-Host "with years: $((@($m.vehicles | Where-Object { $null -ne $_.yearFrom })).Count) / $($m.vehicles.Count)"
