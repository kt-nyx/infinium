Set-StrictMode -Version Latest

function Get-Wp9ReviewedOwnerPendingDocumentationRequirements {
    param(
        [Parameter(Mandatory = $true)] [string] $ManifestId,
        [Parameter(Mandatory = $true)] [string] $ManifestSha256,
        [Parameter(Mandatory = $true)] [string] $CloseReadyCommit,
        [Parameter(Mandatory = $true)] [string] $ReviewedCandidate
    )
    return [ordered]@{
        current_state = @(
            "| Current authorized work | ``M1/S6/WP9`` exact production-profile enrollment manifest ``$ManifestId`` at SHA-256 ``$ManifestSha256`` is independently accepted at candidate ``$ReviewedCandidate`` and remains pending exact owner acceptance. Close-ready source is ``$CloseReadyCommit``. No execution or effect is authorized. |",
            '| Next eligible action | Owner decision on the exact independently reviewed WP9 production-profile manifest only; do not execute unless the exact canonical owner record is added. |',
            '| WP9 owner-stop effect boundary | No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. No packet, review, or prior owner statement grants inherited authority. |'
        )
        readme = @(
            "WP9 production-profile manifest ``$ManifestId`` at SHA-256 ``$ManifestSha256`` is independently accepted at exact candidate ``$ReviewedCandidate`` and remains pending exact owner acceptance.",
            'No execution or effect is authorized: no API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS or public-network operation, provider request, billable operation, or production-profile materialization/use. No authority is inherited.'
        )
    }
}

function Get-Wp9OwnerAcceptedDocumentationRequirements {
    param(
        [Parameter(Mandatory = $true)] [string] $ManifestId,
        [Parameter(Mandatory = $true)] [string] $ManifestSha256,
        [Parameter(Mandatory = $true)] [string] $CloseReadyCommit,
        [Parameter(Mandatory = $true)] [string] $ReviewedCandidate
    )
    return [ordered]@{
        current_state = @(
            "| Current authorized work | ``M1/S6/WP9`` exactly one owner-accepted production-profile enrollment-or-cancel operation for manifest ``$ManifestId`` at SHA-256 ``$ManifestSha256``, independently reviewed at candidate ``$ReviewedCandidate`` with close-ready source ``$CloseReadyCommit``. |",
            '| Next eligible action | Execute the exact owner-accepted EnrollOrVerifyProfile command once, or cancel; stop after its retained terminal evidence. No retry or other WP9/WP10/WP11 action is authorized. |',
            '| WP9 owner-accepted effect boundary | Only the exact bounded helper-owned production-profile credential operation is authorized. No DNS operation, public-network operation, provider request, billable operation, transport qualification, inherited packet authority, or additional credential operation is authorized. |'
        )
        readme = @(
            "WP9 production-profile manifest ``$ManifestId`` at SHA-256 ``$ManifestSha256`` is owner accepted for exactly one enrollment-or-cancel operation after independent review at ``$ReviewedCandidate``.",
            'Only the exact bounded helper-owned credential operation is authorized. No DNS or public-network operation, provider request, billable operation, transport qualification, inherited authority, retry, or additional credential operation is authorized.'
        )
    }
}

function Test-Wp9DocumentationRequirements {
    param(
        [Parameter(Mandatory = $true)] [string] $CurrentStateText,
        [Parameter(Mandatory = $true)] [string] $ReadmeText,
        [Parameter(Mandatory = $true)] $Requirements
    )
    foreach ($line in @($Requirements.current_state)) {
        if ([Regex]::Matches($CurrentStateText, [Regex]::Escape([string]$line)).Count -ne 1) { return $false }
    }
    foreach ($line in @($Requirements.readme)) {
        if ([Regex]::Matches($ReadmeText, [Regex]::Escape([string]$line)).Count -ne 1) { return $false }
    }
    return $true
}
