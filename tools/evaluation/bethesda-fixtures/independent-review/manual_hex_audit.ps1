param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

# Independent manual-worksheet formatter. This is intentionally separate from
# bounded_raw_reader.py and uses only PowerShell/.NET byte operations.
$ErrorActionPreference = 'Stop'
$maximumRecords = 4096
$maximumSubrecords = 4096
$maximumDepth = 64
$maximumInflated = 64MB

function Read-U16([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-U32([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Read-Signature([byte[]]$Bytes, [int]$Offset) {
    return [Text.Encoding]::ASCII.GetString($Bytes, $Offset, 4)
}

function Convert-Hex([byte[]]$Bytes, [int]$Offset, [int]$Length) {
    if ($Length -eq 0) { return '' }
    return [Convert]::ToHexString($Bytes, $Offset, $Length)
}

function Add-Row(
    [System.Collections.Generic.List[object]]$Rows,
    [string]$Space,
    [long]$Offset,
    [long]$Length,
    [string]$Kind,
    [string]$Signature,
    [string]$RawHex
) {
    $Rows.Add([ordered]@{
        offset_space = $Space
        offset = $Offset
        length = $Length
        kind = $Kind
        signature = $Signature
        raw_hex = $RawHex
    })
}

function Read-Subrecords(
    [byte[]]$Payload,
    [string]$Space,
    [int]$BaseOffset,
    [System.Collections.Generic.List[object]]$Rows
) {
    $cursor = 0
    $pending = $null
    $count = 0
    while ($cursor -lt $Payload.Length) {
        if (($Payload.Length - $cursor) -lt 6) {
            throw "truncated-subrecord-header@$($BaseOffset + $cursor)"
        }
        $signature = Read-Signature $Payload $cursor
        $declared16 = Read-U16 $Payload ($cursor + 4)
        $headerOffset = $BaseOffset + $cursor
        Add-Row $Rows $Space $headerOffset 6 'subrecord-header' $signature (
            Convert-Hex $Payload $cursor 6)
        $cursor += 6
        $count++
        if ($count -gt $maximumSubrecords) {
            throw "subrecord-count-over-limit@$headerOffset"
        }
        if ($signature -eq 'XXXX') {
            if ($null -ne $pending -or $declared16 -ne 4 -or
                ($Payload.Length - $cursor) -lt 4) {
                throw "invalid-extended-size@$headerOffset"
            }
            $pending = Read-U32 $Payload $cursor
            Add-Row $Rows $Space ($BaseOffset + $cursor) 4 'subrecord-data' $signature (
                Convert-Hex $Payload $cursor 4)
            $cursor += 4
            continue
        }
        $declared = if ($null -ne $pending) { [long]$pending } else { [long]$declared16 }
        $pending = $null
        if ($declared -gt ($Payload.Length - $cursor)) {
            throw "subrecord-body-overrun@$headerOffset"
        }
        Add-Row $Rows $Space ($BaseOffset + $cursor) $declared 'subrecord-data' $signature (
            Convert-Hex $Payload $cursor $declared)
        $cursor += $declared
    }
    if ($null -ne $pending) {
        throw "dangling-extended-size@$($BaseOffset + $cursor)"
    }
}

function Read-Record(
    [byte[]]$Bytes,
    [int]$Offset,
    [int]$End,
    [System.Collections.Generic.List[object]]$Rows,
    [ref]$RecordCount
) {
    if (($End - $Offset) -lt 24) {
        throw "truncated-record-header@$Offset"
    }
    $signature = Read-Signature $Bytes $Offset
    [long]$size = Read-U32 $Bytes ($Offset + 4)
    $flags = Read-U32 $Bytes ($Offset + 8)
    $payloadOffset = $Offset + 24
    if ($size -gt (0xFFFFFFFFL - $payloadOffset)) {
        throw "record-size-overflow@$Offset"
    }
    if ($size -gt ($End - $payloadOffset)) {
        throw "record-size-past-end@$Offset"
    }
    $RecordCount.Value++
    if ($RecordCount.Value -gt $maximumRecords) {
        throw "record-count-over-limit@$Offset"
    }
    Add-Row $Rows 'physical-file' $Offset 24 'record-header' $signature (
        Convert-Hex $Bytes $Offset 24)
    $payload = [byte[]]::new([int]$size)
    if ($size -gt 0) {
        [Array]::Copy($Bytes, $payloadOffset, $payload, 0, [int]$size)
    }
    if (($flags -band 0x00040000) -ne 0) {
        Add-Row $Rows 'physical-file' $payloadOffset $size 'compressed-container' $signature (
            Convert-Hex $Bytes $payloadOffset ([int]$size))
        if ($size -lt 4) { throw "compressed-missing-length@$payloadOffset" }
        [long]$declared = Read-U32 $payload 0
        if ($declared -gt $maximumInflated) {
            throw "compressed-declared-size-over-limit@$payloadOffset"
        }
        $input = [IO.MemoryStream]::new($payload, 4, $payload.Length - 4, $false)
        $zlib = [IO.Compression.ZLibStream]::new(
            $input, [IO.Compression.CompressionMode]::Decompress, $false)
        $output = [IO.MemoryStream]::new()
        try {
            $buffer = [byte[]]::new(8192)
            while (($read = $zlib.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $output.Write($buffer, 0, $read)
                if ($output.Length -gt $maximumInflated) {
                    throw "compressed-output-over-limit@$payloadOffset"
                }
            }
        }
        catch {
            if ($_.Exception.Message -like 'compressed-*') { throw }
            throw "compressed-invalid-zlib@$($payloadOffset + 4)"
        }
        finally {
            $zlib.Dispose()
            $input.Dispose()
        }
        $logical = $output.ToArray()
        $output.Dispose()
        if ($logical.Length -ne $declared) {
            throw "compressed-size-mismatch@$payloadOffset"
        }
        Read-Subrecords $logical 'decompressed-record' 0 $Rows
    }
    else {
        Read-Subrecords $payload 'physical-file' $payloadOffset $Rows
    }
    return $payloadOffset + [int]$size
}

function Read-Elements(
    [byte[]]$Bytes,
    [int]$Start,
    [int]$End,
    [int]$Depth,
    [System.Collections.Generic.List[object]]$Rows,
    [ref]$RecordCount
) {
    if ($Depth -gt $maximumDepth) {
        throw "group-depth-over-limit@$Start"
    }
    $cursor = $Start
    while ($cursor -lt $End) {
        if (($End - $cursor) -lt 4) { throw "truncated-signature@$cursor" }
        if ((Read-Signature $Bytes $cursor) -eq 'GRUP') {
            if (($End - $cursor) -lt 24) { throw "truncated-group-header@$cursor" }
            [long]$size = Read-U32 $Bytes ($cursor + 4)
            if ($size -lt 24) { throw "group-size-too-small@$cursor" }
            if ($size -gt (0xFFFFFFFFL - $cursor)) {
                throw "group-size-overflow@$cursor"
            }
            if ($size -gt ($End - $cursor)) { throw "group-size-past-end@$cursor" }
            Add-Row $Rows 'physical-file' $cursor 24 'group-header' 'GRUP' (
                Convert-Hex $Bytes $cursor 24)
            $groupEnd = $cursor + [int]$size
            Read-Elements $Bytes ($cursor + 24) $groupEnd ($Depth + 1) $Rows $RecordCount
            $cursor = $groupEnd
        }
        else {
            $cursor = Read-Record $Bytes $cursor $End $Rows $RecordCount
        }
    }
}

$package = (Resolve-Path -LiteralPath $PackagePath).Path
$inputs = Join-Path $package 'inputs'
$files = Get-ChildItem -LiteralPath $inputs -Recurse -File |
    Where-Object { $_.Extension.ToLowerInvariant() -in @('.esm', '.esp', '.esl') } |
    Sort-Object FullName
$audit = [System.Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $rows = [System.Collections.Generic.List[object]]::new()
    $recordCount = 0
    $malformed = $null
    try {
        Read-Elements $bytes 0 $bytes.Length 0 $rows ([ref]$recordCount)
    }
    catch {
        $malformed = $_.Exception.Message
    }
    $relative = [IO.Path]::GetRelativePath($inputs, $file.FullName).Replace('\', '/')
    $audit.Add([ordered]@{
        path = $relative
        byte_length = $bytes.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        record_count_before_failure = $recordCount
        malformed = $malformed
        rows = $rows
    })
}
$result = [ordered]@{
    method_id = 'manual-annotated-hex-worksheet-v1'
    package_id = (Split-Path -Leaf $package)
    files = $audit
    supplemental_inputs = @(
        Get-ChildItem -LiteralPath $inputs -Recurse -File |
            Where-Object {
                $_.Extension.ToLowerInvariant() -notin @('.esm', '.esp', '.esl') -and
                ($_.Directory.Name -eq 'requests' -or $_.Extension.ToLowerInvariant() -eq '.strings')
            } |
            Sort-Object FullName |
            ForEach-Object {
                $otherBytes = [IO.File]::ReadAllBytes($_.FullName)
                [ordered]@{
                    path = [IO.Path]::GetRelativePath($inputs, $_.FullName).Replace('\', '/')
                    byte_length = $otherBytes.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                    raw_hex = Convert-Hex $otherBytes 0 $otherBytes.Length
                }
            }
    )
}
$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputPath -Encoding utf8
