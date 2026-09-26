# Run with PowerShell 7. This audit reads metadata and never executes the DLLs.
param(
    [string]$CheckerAssembly = (Join-Path $PSScriptRoot '..\..\..\..\Out\Bin\ToolBridge_v0.1.0_ApiChecker.dll')
)
$ErrorActionPreference = 'Stop'
if (-not ('ApiSignatureProvider' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'Verification\ApiSignatureProvider.cs')
}
$baseline = (Resolve-Path (Join-Path $PSScriptRoot 'references\Topomatic.ToolBridge.dll')).Path
$consumer = (Resolve-Path $CheckerAssembly).Path
$report = [ApiSignatureProvider]::Verify($baseline, $consumer)
$report | Write-Output
if ($report.Length -ne 1) { throw 'Not all baseline API members are referenced by the checker.' }
