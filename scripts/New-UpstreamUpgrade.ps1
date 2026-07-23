[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UpstreamRef,

    [string]$UpgradeName,
    [string]$CustomBranch = "custom/main",
    [string]$UpstreamRemote = "upstream",
    [switch]$SkipFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw "Run this script from the v2rayN Pro Git repository."
}
Set-Location $repositoryRoot

$changes = @(& git status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect the Git worktree."
}
if ($changes.Count -gt 0) {
    throw "The worktree must be clean before starting an upstream upgrade."
}

$remoteUrl = (& git remote get-url $UpstreamRemote).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
    throw "Git remote '$UpstreamRemote' is not configured."
}

if (-not $SkipFetch) {
    Invoke-Git -Arguments @("fetch", $UpstreamRemote, "--prune", "--tags")
}

$candidates = @($UpstreamRef, "$UpstreamRemote/$UpstreamRef")
$resolvedRef = $null
foreach ($candidate in $candidates) {
    $resolved = (& git rev-parse --verify "${candidate}^{commit}" 2>$null)
    if ($LASTEXITCODE -eq 0) {
        $resolvedRef = $candidate
        break
    }
}
if ($null -eq $resolvedRef) {
    throw "Cannot resolve upstream ref '$UpstreamRef'."
}

if ([string]::IsNullOrWhiteSpace($UpgradeName)) {
    $UpgradeName = $UpstreamRef -replace "^refs/tags/", ""
    $UpgradeName = $UpgradeName -replace "^$([regex]::Escape($UpstreamRemote))/", ""
}
$safeName = $UpgradeName -replace "[^A-Za-z0-9._-]", "-"
if ([string]::IsNullOrWhiteSpace($safeName)) {
    throw "Upgrade name is empty after normalization."
}
$upgradeBranch = "upgrade/$safeName"

& git show-ref --verify --quiet "refs/heads/$upgradeBranch"
if ($LASTEXITCODE -eq 0) {
    throw "Local branch '$upgradeBranch' already exists."
}

Invoke-Git -Arguments @("switch", $CustomBranch)
Invoke-Git -Arguments @("switch", "-c", $upgradeBranch)

& git merge --no-ff --no-commit $resolvedRef
$mergeExitCode = $LASTEXITCODE
if ($mergeExitCode -ne 0) {
    Write-Warning "The merge has conflicts. Resolve them on '$upgradeBranch', then run the verification script."
    exit $mergeExitCode
}

Write-Host ""
Write-Host "Upstream changes are merged but not committed."
Write-Host "Review the diff and docs/customizations.md, then run:"
Write-Host "  .\scripts\Test-CustomBuild.ps1 -Target all"
Write-Host "  git commit -m `"chore: merge upstream $UpstreamRef`""
Write-Host "  git push -u origin $upgradeBranch"
