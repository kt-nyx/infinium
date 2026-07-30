[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][ValidateSet('Deny', 'Restore')][string]$Mode
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Disposable ACL fixture is missing: $Path"
}
$marker = "$Path.acl-sddl.txt"
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User

if ($Mode -eq 'Deny') {
    $acl = Get-Acl -LiteralPath $Path
    [IO.File]::WriteAllText($marker, $acl.Sddl, [Text.UTF8Encoding]::new($false))
    $rule = [Security.AccessControl.FileSystemAccessRule]::new(
        $identity,
        [Security.AccessControl.FileSystemRights]::ReadData,
        [Security.AccessControl.AccessControlType]::Deny
    )
    $acl.AddAccessRule($rule) | Out-Null
    Set-Acl -LiteralPath $Path -AclObject $acl
}
else {
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Saved ACL marker is missing: $marker"
    }
    $sddl = Get-Content -LiteralPath $marker -Raw
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetSecurityDescriptorSddlForm($sddl)
    Set-Acl -LiteralPath $Path -AclObject $acl
    Remove-Item -LiteralPath $marker -Force
}
