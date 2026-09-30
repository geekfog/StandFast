<#
.SYNOPSIS
    Makes every release branch except the newest read-only, by writing their names into the "Locked releases" ruleset.

.DESCRIPTION
    Release branches are named release/MM.mm, optionally with a patch (release/MM.mm.pp). They are ordered by version number rather than by name, so
    release/02.00 is newer than release/01.25 however the parts are padded. A release branch whose name is not a version is left alone.

    The ruleset is created from .github/rulesets/locked-releases.json the first time and updated in place after that, so the template is the one
    definition of what a lock is and the branch list is the only thing this script decides. Calls go through the GitHub CLI, which reads its token from GH_TOKEN.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $p_Repository
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$releasePrefix = 'release/'
$branchRefPrefix = 'refs/heads/'
$versionPattern = '^release/(?<version>\d+\.\d+(\.\d+)?)$'
$templatePath = Join-Path $PSScriptRoot '../rulesets/locked-releases.json'

$ruleset = Get-Content $templatePath -Raw | ConvertFrom-Json

$releases = @(gh api "repos/$p_Repository/branches" --paginate --jq '.[].name' |
    Where-Object { $_.StartsWith($releasePrefix) -and $_ -match $versionPattern } |
    ForEach-Object { [pscustomobject]@{ Name = $_; Version = [version]$Matches.version } } |
    Sort-Object Version)

if ($releases.Count -lt 2) {
    Write-Host "Nothing to lock: $($releases.Count) versioned release branch(es)."
    return
}

$newest = $releases[-1].Name
$older = @($releases[0..($releases.Count - 2)] | ForEach-Object { "$branchRefPrefix$($_.Name)" })
$ruleset.conditions.ref_name.include = $older

Write-Host "Newest release, left open: $newest"
Write-Host "Locking: $($older -join ', ')"

$existingId = gh api "repos/$p_Repository/rulesets" --paginate --jq ".[] | select(.name == `"$($ruleset.name)`") | .id"
$body = $ruleset | ConvertTo-Json -Depth 10

if ($existingId) {
    $body | gh api --method PUT "repos/$p_Repository/rulesets/$existingId" --input - --silent
    Write-Host "Updated ruleset '$($ruleset.name)' ($existingId)."
}
else {
    $body | gh api --method POST "repos/$p_Repository/rulesets" --input - --silent
    Write-Host "Created ruleset '$($ruleset.name)'."
}
