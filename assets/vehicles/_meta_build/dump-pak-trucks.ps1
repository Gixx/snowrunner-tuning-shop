$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$pak = 'E:\SteamLibrary\steamapps\common\SnowRunner\preload\paks\client\initial.pak'
$out = 'd:\Work\Gabi\Projects\snowrunner-tuning-shop\assets\vehicles\_meta_build\pak-trucks.json'
$z = [System.IO.Compression.ZipFile]::OpenRead($pak)
try {
    $stringsEntry = $z.Entries | Where-Object { $_.FullName.Replace('\','/') -like '*/strings_english.str' } | Select-Object -First 1
    $sr = New-Object System.IO.StreamReader($stringsEntry.Open(), [System.Text.Encoding]::Unicode, $true)
    $strText = $sr.ReadToEnd()
    $sr.Close()
    $strings = @{}
    [regex]::Matches($strText, '(UI_[A-Za-z0-9_]+)\s+"((?:\\.|[^"])*)"') | ForEach-Object {
        $strings[$_.Groups[1].Value] = $_.Groups[2].Value.Replace('\"','"')
    }
    $trucks = @()
    foreach ($entry in $z.Entries) {
        $p = $entry.FullName.Replace('\','/')
        $marker = '/classes/trucks/'
        $i = $p.ToLowerInvariant().IndexOf($marker)
        if ($i -lt 0 -or -not $p.ToLowerInvariant().EndsWith('.xml')) { continue }
        $rel = $p.Substring($i + $marker.Length)
        if ($rel.Contains('/')) { continue }
        $sr2 = New-Object System.IO.StreamReader($entry.Open(), [System.Text.Encoding]::UTF8, $true)
        $xml = $sr2.ReadToEnd()
        $sr2.Close()
        $m = [regex]::Match($xml, 'UiName\s*=\s*"(UI_[^"]+)"', 'IgnoreCase')
        $key = if ($m.Success) { $m.Groups[1].Value } else { '' }
        $id = [System.IO.Path]::GetFileNameWithoutExtension($rel)
        $en = if ($key -and $strings.ContainsKey($key)) { $strings[$key] } else { '' }
        $trucks += [pscustomobject]@{ id = $id; uiNameKey = $key; englishName = $en }
    }
    $trucks | ConvertTo-Json -Depth 4 | Set-Content -Path $out -Encoding UTF8
    Write-Output "wrote $($trucks.Count) trucks"
}
finally { $z.Dispose() }
