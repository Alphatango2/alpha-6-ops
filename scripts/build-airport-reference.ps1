param([string]$InputCsv = 'work/live-flight-context/airports.csv')
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot '../assets/airports'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$rows = Import-Csv -LiteralPath $InputCsv | Where-Object { $_.type -notin 'closed','balloonport' }
$file = [IO.File]::Create((Join-Path $destination 'airports.tsv.gz'))
$gzip = New-Object IO.Compression.GZipStream($file, [IO.Compression.CompressionLevel]::Optimal)
$writer = New-Object IO.StreamWriter($gzip, [Text.UTF8Encoding]::new($false))
$count = 0
try {
    foreach ($row in $rows) {
        $ident = if ($row.icao_code) { $row.icao_code } elseif ($row.gps_code) { $row.gps_code } else { $row.ident }
        $values = @($ident, $row.name, $row.municipality, $row.latitude_deg, $row.longitude_deg) | ForEach-Object { $_ -replace '[\t\r\n]', ' ' }
        $writer.WriteLine(($values -join "`t"))
        $count++
    }
} finally { $writer.Dispose(); $gzip.Dispose(); $file.Dispose() }
[ordered]@{ source = 'https://ourairports.com/data/'; downloadedUtc = [DateTime]::UtcNow.ToString('o'); license = 'Public domain'; records = $count; inputSha256 = (Get-FileHash -LiteralPath $InputCsv).Hash; purpose = 'Offline approximate airport proximity; not navigation or proof of departure/destination.' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'provenance.json') -Encoding UTF8
Write-Output "Airport reference: $count records"
