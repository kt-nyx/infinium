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

function Convert-RowHex([object]$Row) {
    if ([string]::IsNullOrEmpty($Row.raw_hex)) { return [byte[]]::new(0) }
    return [Convert]::FromHexString([string]$Row.raw_hex)
}

function Convert-RowsToSemanticStructure([System.Collections.Generic.List[object]]$Rows) {
    $records = [System.Collections.Generic.List[object]]::new()
    $groups = [System.Collections.Generic.List[object]]::new()
    $current = $null
    $pendingHeader = $null
    foreach ($row in $Rows) {
        if ($row.kind -eq 'group-header') {
            $raw = Convert-RowHex $row
            $groups.Add([ordered]@{
                offset = [long]$row.offset
                length = [long](Read-U32 $raw 4)
                label_hex = Convert-Hex $raw 8 4
                group_type = [long](Read-U32 $raw 12)
            })
            continue
        }
        if ($row.kind -eq 'record-header') {
            $raw = Convert-RowHex $row
            $flags = Read-U32 $raw 8
            $current = [ordered]@{
                signature = [string]$row.signature
                offset = [long]$row.offset
                length = [long](24 + (Read-U32 $raw 4))
                data_length = [long](Read-U32 $raw 4)
                flags_hex = '{0:X8}' -f $flags
                raw_form_id_hex = '{0:X8}' -f (Read-U32 $raw 12)
                compressed = (($flags -band 0x00040000) -ne 0)
                subrecords = [System.Collections.Generic.List[object]]::new()
            }
            $records.Add($current)
            $pendingHeader = $null
            continue
        }
        if ($null -eq $current) { continue }
        if ($row.kind -eq 'subrecord-header') {
            $pendingHeader = $row
            continue
        }
        if ($row.kind -eq 'subrecord-data' -and $null -ne $pendingHeader) {
            $current.subrecords.Add([ordered]@{
                signature = [string]$pendingHeader.signature
                header_offset = [long]$pendingHeader.offset
                data_offset = [long]$row.offset
                length = [long]$row.length
                raw_hex = [string]$row.raw_hex
            })
            $pendingHeader = $null
        }
    }
    return [ordered]@{
        records = $records
        groups = $groups
    }
}

function Get-MasterOrder([object]$Tes4Record) {
    $masters = [System.Collections.Generic.List[string]]::new()
    $subs = @($Tes4Record.subrecords)
    for ($index = 0; $index -lt $subs.Count; $index++) {
        $item = $subs[$index]
        if ($item.signature -ne 'MAST') { continue }
        $raw = [Convert]::FromHexString([string]$item.raw_hex)
        if ($raw.Length -eq 0 -or $raw[-1] -ne 0) {
            throw "master-not-zero-terminated@$($item.data_offset)"
        }
        if (($index + 1) -ge $subs.Count -or $subs[$index + 1].signature -ne 'DATA') {
            throw "master-missing-data-pair@$($item.header_offset)"
        }
        $masters.Add([Text.Encoding]::ASCII.GetString($raw, 0, $raw.Length - 1))
        $index++
    }
    return $masters
}

function Get-SubrecordHexValues([object]$Record, [string]$Signature) {
    return @(
        $Record.subrecords |
            Where-Object { $_.signature -eq $Signature } |
            ForEach-Object { [string]$_.raw_hex }
    )
}

function Get-AllowlistedPayload([object]$Record) {
    $fields = [ordered]@{}
    if ($Record.signature -eq 'NPC_') {
        foreach ($name in @('ACBS', 'TPLT', 'RNAM', 'AIDT', 'PKID', 'PNAM', 'HCLF')) {
            $values = @(Get-SubrecordHexValues $Record $name)
            if ($values.Count -gt 0) { $fields[$name] = $values }
        }
        if ($fields.Contains('ACBS')) {
            $raw = [Convert]::FromHexString([string]$fields.ACBS[0])
            if ($raw.Length -ne 24) {
                throw "invalid-npc-acbs-length@$($Record.offset)"
            }
            $fields.configuration_flags_hex = '{0:X8}' -f (Read-U32 $raw 0)
            $fields.template_flags_hex = '{0:X4}' -f (Read-U16 $raw 18)
        }
        foreach ($name in @('TPLT', 'RNAM', 'PKID', 'PNAM', 'HCLF')) {
            if (-not $fields.Contains($name)) { continue }
            foreach ($rawHex in @($fields[$name])) {
                if ([Convert]::FromHexString([string]$rawHex).Length -ne 4) {
                    throw "invalid-npc-$($name.ToLowerInvariant())-length@$($Record.offset)"
                }
            }
        }
        foreach ($rawHex in @(if ($fields.Contains('AIDT')) { $fields.AIDT } else { @() })) {
            if ([Convert]::FromHexString([string]$rawHex).Length -ne 20) {
                throw "invalid-npc-aidt-length@$($Record.offset)"
            }
        }
        return $fields
    }
    if ($Record.signature -eq 'REFR') {
        foreach ($name in @('NAME', 'XLKR', 'XLRL', 'XOWN', 'DATA', 'XESP')) {
            $values = @(Get-SubrecordHexValues $Record $name)
            if ($values.Count -gt 0) { $fields[$name] = $values }
        }
        foreach ($name in @('NAME', 'XLRL', 'XOWN')) {
            if (-not $fields.Contains($name)) { continue }
            foreach ($rawHex in @($fields[$name])) {
                if ([Convert]::FromHexString([string]$rawHex).Length -ne 4) {
                    throw "invalid-refr-$($name.ToLowerInvariant())-length@$($Record.offset)"
                }
            }
        }
        foreach ($rawHex in @(if ($fields.Contains('XLKR')) { $fields.XLKR } else { @() })) {
            if ([Convert]::FromHexString([string]$rawHex).Length -ne 8) {
                throw "invalid-refr-xlkr-length@$($Record.offset)"
            }
        }
        foreach ($rawHex in @(if ($fields.Contains('DATA')) { $fields.DATA } else { @() })) {
            $raw = [Convert]::FromHexString([string]$rawHex)
            if ($raw.Length -ne 24) {
                throw "invalid-refr-data-length@$($Record.offset)"
            }
            $patterns = [System.Collections.Generic.List[string]]::new()
            for ($offset = 0; $offset -lt 24; $offset += 4) {
                $patterns.Add((Convert-Hex $raw $offset 4))
            }
            $fields.float32_bit_patterns = $patterns
        }
        return $fields
    }
    if ($Record.signature -eq 'RACE') {
        $values = @(Get-SubrecordHexValues $Record 'DATA')
        if ($values.Count -eq 0) { return $fields }
        $fields.DATA = $values
        $raw = [Convert]::FromHexString([string]$values[0])
        if ($raw.Length -lt 4) {
            throw "invalid-race-data-length@$($Record.offset)"
        }
        $fields.face_gen_head = (((Read-U32 $raw 0) -band 0x2) -ne 0)
        return $fields
    }
    return $null
}

function Resolve-ManualFormId(
    [string]$RawHex,
    [string]$CurrentName,
    [object[]]$Masters,
    [hashtable]$LightFlags
) {
    [uint32]$raw = [Convert]::ToUInt32($RawHex, 16)
    if ($raw -eq 0) {
        return [ordered]@{
            form_id_hex = $RawHex
            resolution_state = 'null'
            form_key = $null
            origin_plugin = $null
            origin_kind = $null
            local_id_hex = $null
        }
    }
    $index = $raw -shr 24
    $origins = @($Masters) + @($CurrentName)
    if ($index -ge $origins.Count) {
        return [ordered]@{
            form_id_hex = $RawHex
            resolution_state = 'invalid'
            reason = "master index $index unavailable in $($Masters.Count) masters"
            form_key = $null
            origin_plugin = $null
            origin_kind = 'invalid'
            local_id_hex = $null
        }
    }
    $origin = [string]$origins[$index]
    if (-not $LightFlags.ContainsKey($origin)) {
        return [ordered]@{
            form_id_hex = $RawHex
            resolution_state = 'unknown'
            reason = "origin flags unavailable for $origin"
            form_key = $null
            origin_plugin = $origin
            origin_kind = 'unknown'
            local_id_hex = $null
        }
    }
    [uint32]$rawLocal = $raw -band 0x00FFFFFF
    if ($LightFlags[$origin]) {
        if ($rawLocal -lt 0x800 -or $rawLocal -gt 0xFFF) {
            return [ordered]@{
                form_id_hex = $RawHex
                resolution_state = 'invalid'
                reason = ('light local ID {0:X6} outside 000800..000FFF' -f $rawLocal)
                form_key = $null
                origin_plugin = $origin
                origin_kind = 'invalid'
                local_id_hex = ('{0:X6}' -f $rawLocal)
            }
        }
        $local = $rawLocal -band 0xFFF
        $kind = 'light'
    }
    else {
        $local = $rawLocal
        $kind = 'full'
    }
    return [ordered]@{
        form_id_hex = $RawHex
        resolution_state = 'resolved'
        form_key = ('{0:X8}:{1}' -f $local, $origin)
        origin_plugin = $origin
        origin_kind = $kind
        local_id_hex = ('{0:X8}' -f $local)
    }
}

function Convert-LittleEndianFormId([string]$RawHex) {
    $raw = [Convert]::FromHexString($RawHex)
    return '{0:X8}' -f (Read-U32 $raw 0)
}

function Set-ExtensionMismatchInvalid([object]$Resolved) {
    $Resolved.resolution_state = 'invalid'
    $Resolved.reason = 'native .esl extension/header light-flag mismatch'
    $Resolved.form_key = $null
    $Resolved.origin_kind = 'invalid'
    $Resolved.local_id_hex = $null
}

function Add-ManualSemanticAnnotations(
    [System.Collections.Generic.List[object]]$Audit
) {
    $lightFlags = @{}
    foreach ($file in $Audit) {
        if ($null -ne $file.tes4 -and $file.path.StartsWith('plugins/')) {
            $lightFlags[[IO.Path]::GetFileName([string]$file.path)] = [bool]$file.tes4.esl_flag
        }
    }
    foreach ($file in $Audit) {
        if ($null -eq $file.tes4) { continue }
        $currentName = [IO.Path]::GetFileName([string]$file.path)
        $mismatch = ([IO.Path]::GetExtension($currentName).ToLowerInvariant() -eq '.esl' -and
            -not [bool]$file.tes4.esl_flag)
        $file.extension_header_mismatch = $mismatch
        if (-not $lightFlags.ContainsKey($currentName)) {
            $lightFlags[$currentName] = [bool]$file.tes4.esl_flag
        }
        foreach ($record in $file.records) {
            if ($record.signature -eq 'TES4') { continue }
            $identity = Resolve-ManualFormId `
                ([string]$record.raw_form_id_hex) `
                $currentName `
                @($file.tes4.masters) `
                $lightFlags
            if ($mismatch -and $identity.origin_plugin -eq $currentName) {
                Set-ExtensionMismatchInvalid $identity
            }
            $record.identity = $identity
            $payload = Get-AllowlistedPayload $record
            if ($null -eq $payload) { continue }
            $record.allowlisted_payload = $payload
            $links = [System.Collections.Generic.List[object]]::new()
            foreach ($field in @('TPLT', 'RNAM', 'PKID', 'PNAM', 'HCLF', 'NAME', 'XLRL', 'XOWN')) {
                if (-not $payload.Contains($field)) { continue }
                $occurrence = 0
                foreach ($rawHex in @($payload[$field])) {
                    $resolved = Resolve-ManualFormId `
                        (Convert-LittleEndianFormId ([string]$rawHex)) `
                        $currentName `
                        @($file.tes4.masters) `
                        $lightFlags
                    if ($mismatch -and $resolved.origin_plugin -eq $currentName) {
                        Set-ExtensionMismatchInvalid $resolved
                    }
                    $link = [ordered]@{
                        field = $field
                        occurrence = $occurrence
                    }
                    foreach ($key in $resolved.Keys) { $link[$key] = $resolved[$key] }
                    $links.Add($link)
                    $occurrence++
                }
            }
            $occurrence = 0
            foreach ($rawHex in @(if ($payload.Contains('XLKR')) { $payload.XLKR } else { @() })) {
                $raw = [Convert]::FromHexString([string]$rawHex)
                foreach ($component in @(
                    [ordered]@{ name = 'keyword'; offset = 0 },
                    [ordered]@{ name = 'linked-reference'; offset = 4 }
                )) {
                    $part = Convert-Hex $raw $component.offset 4
                    $resolved = Resolve-ManualFormId `
                        (Convert-LittleEndianFormId $part) `
                        $currentName `
                        @($file.tes4.masters) `
                        $lightFlags
                    $link = [ordered]@{
                        field = 'XLKR'
                        occurrence = $occurrence
                        component = $component.name
                    }
                    foreach ($key in $resolved.Keys) { $link[$key] = $resolved[$key] }
                    $links.Add($link)
                }
                $occurrence++
            }
            $record.links = $links
        }
    }
}

function Get-ScenarioSemantics(
    [System.Collections.Generic.List[object]]$Audit,
    [string]$Inputs
) {
    $matrix = Get-Content -Raw -LiteralPath (Join-Path $Inputs 'case-matrix.json') |
        ConvertFrom-Json -AsHashtable
    $filesByPath = @{}
    foreach ($file in $Audit) { $filesByPath[[string]$file.path] = $file }
    $scenarios = [System.Collections.Generic.List[object]]::new()
    foreach ($case in $matrix.cases) {
        $pluginPaths = @(
            $case.input_paths |
                Where-Object { [IO.Path]::GetExtension([string]$_).ToLowerInvariant() -in @('.esm', '.esp', '.esl') }
        )
        $definitions = [System.Collections.Generic.List[object]]::new()
        if ($case.operation -eq 'scan' -and $pluginPaths.Count -gt 0) {
            $definitions.Add([ordered]@{
                scenario_id = [string]$case.case_id
                plugin_paths = $pluginPaths
            })
        }
        elseif ($case.operation -eq 'compare') {
            for ($index = 0; $index -lt $pluginPaths.Count; $index++) {
                $definitions.Add([ordered]@{
                    scenario_id = "$($case.case_id).variant-$index"
                    plugin_paths = @([string]$pluginPaths[$index])
                })
            }
        }
        elseif ($case.operation -eq 'orchestrated-read') {
            $request = Get-Content -Raw -LiteralPath (Join-Path $Inputs ([string]$case.input_paths[0])) |
                ConvertFrom-Json -AsHashtable
            $definitions.Add([ordered]@{
                scenario_id = "$($case.case_id).initial"
                plugin_paths = @([string]$request.initial_path)
            })
            $definitions.Add([ordered]@{
                scenario_id = "$($case.case_id).replacement"
                plugin_paths = @([string]$request.replacement_path)
            })
        }
        foreach ($definition in $definitions) {
        $paths = @($definition.plugin_paths)
        $population = @{}
        $records = [System.Collections.Generic.List[object]]::new()
        for ($order = 0; $order -lt $paths.Count; $order++) {
            $path = [string]$paths[$order]
            $file = $filesByPath[$path]
            if ($null -eq $file) { continue }
            if ($null -ne $file.malformed -and
                -not ([string]$file.malformed).StartsWith('master-missing-data-pair@')) {
                continue
            }
            for ($recordIndex = 0; $recordIndex -lt $file.records.Count; $recordIndex++) {
                $record = $file.records[$recordIndex]
                if ($null -eq $record.identity -or
                    [string]::IsNullOrEmpty([string]$record.identity.form_key)) { continue }
                $locator = "$path#$recordIndex"
                if (-not $population.ContainsKey([string]$record.identity.form_key)) {
                    $population[[string]$record.identity.form_key] =
                        [System.Collections.Generic.List[string]]::new()
                }
                $population[[string]$record.identity.form_key].Add($locator)
                $links = [System.Collections.Generic.List[object]]::new()
                $recordLinks = if ($record.Contains('links')) { @($record.links) } else { @() }
                foreach ($link in $recordLinks) {
                    $state = [string]$link.resolution_state
                    if ($state -eq 'resolved' -and
                        -not $population.ContainsKey([string]$link.form_key)) {
                        # Final population is applied below after all records are collected.
                        $state = 'pending'
                    }
                    $links.Add([ordered]@{
                        field = [string]$link.field
                        occurrence = [int]$link.occurrence
                        component = if ($null -eq $link.component) { $null } else { [string]$link.component }
                        form_id_hex = [string]$link.form_id_hex
                        form_key = if ($null -eq $link.form_key) { $null } else { [string]$link.form_key }
                        resolution_state = $state
                    })
                }
                $records.Add([ordered]@{
                    locator = $locator
                    plugin_order = $order
                    form_key = [string]$record.identity.form_key
                    deleted = (([Convert]::ToUInt32([string]$record.flags_hex, 16) -band 0x20) -ne 0)
                    links = $links
                })
            }
        }
        foreach ($entry in $records) {
            foreach ($link in $entry.links) {
                if ($link.resolution_state -eq 'pending') {
                    $link.resolution_state = if ($population.ContainsKey([string]$link.form_key)) {
                        'resolved'
                    } else {
                        'unresolved'
                    }
                }
            }
        }
        $chains = [System.Collections.Generic.List[object]]::new()
        foreach ($formKey in @($population.Keys | Sort-Object)) {
            $ordered = @($population[$formKey])
            if ($ordered.Count -lt 2) { continue }
            $chains.Add([ordered]@{
                form_key = $formKey
                ordered_locators = $ordered
                winner_locator = $ordered[-1]
            })
        }
        $scenarios.Add([ordered]@{
            scenario_id = [string]$definition.scenario_id
            plugin_paths = $paths
            records = $records
            chains = $chains
            denominator = $case.denominator
        })
        }
    }
    return $scenarios
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
    $structure = Convert-RowsToSemanticStructure $rows
    $tes4 = $null
    if ($structure.records.Count -gt 0 -and $structure.records[0].signature -eq 'TES4') {
        $tes4Record = $structure.records[0]
        try {
            $tes4 = [ordered]@{
                flags_hex = [string]$tes4Record.flags_hex
                esl_flag = (([Convert]::ToUInt32([string]$tes4Record.flags_hex, 16) -band 0x200) -ne 0)
                masters = @(Get-MasterOrder $tes4Record)
            }
        }
        catch {
            if ($null -eq $malformed) { $malformed = $_.Exception.Message }
            $tes4 = [ordered]@{
                flags_hex = [string]$tes4Record.flags_hex
                esl_flag = (([Convert]::ToUInt32([string]$tes4Record.flags_hex, 16) -band 0x200) -ne 0)
                masters = @(
                    $tes4Record.subrecords |
                        Where-Object { $_.signature -eq 'MAST' } |
                        ForEach-Object {
                            $raw = [Convert]::FromHexString([string]$_.raw_hex)
                            if ($raw.Length -gt 0 -and $raw[-1] -eq 0) {
                                [Text.Encoding]::ASCII.GetString($raw, 0, $raw.Length - 1)
                            }
                        }
                )
            }
        }
    }
    elseif ($bytes.Length -ge 24 -and (Read-Signature $bytes 0) -eq 'TES4') {
        $flags = Read-U32 $bytes 8
        $tes4 = [ordered]@{
            flags_hex = '{0:X8}' -f $flags
            esl_flag = (($flags -band 0x200) -ne 0)
            masters = @()
        }
    }
    $relative = [IO.Path]::GetRelativePath($inputs, $file.FullName).Replace('\', '/')
    $audit.Add([ordered]@{
        path = $relative
        byte_length = $bytes.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        record_count_before_failure = $recordCount
        malformed = $malformed
        rows = $rows
        tes4 = $tes4
        groups = $structure.groups
        records = $structure.records
        extension_header_mismatch = $false
    })
}
Add-ManualSemanticAnnotations $audit
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
                    json_value = if ($_.Extension.ToLowerInvariant() -eq '.json') {
                        Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json -AsHashtable
                    } else {
                        $null
                    }
                }
            }
    )
    scenario_semantics = @(Get-ScenarioSemantics $audit $inputs)
}
$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$result | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $OutputPath -Encoding utf8
