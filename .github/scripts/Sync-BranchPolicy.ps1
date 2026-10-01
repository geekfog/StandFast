<#
.SYNOPSIS
    Applies the repository's branch policy: every ruleset in .github/rulesets, with only the newest release branch left open, and that branch as the
    repository's default so new pull requests target it.

.DESCRIPTION
    Release branches are named release/MM.mm, optionally with a patch (release/MM.mm.pp). They are ordered by version number rather than by name, so
    release/02.00 is newer than release/01.25 however the parts are padded. A release branch whose name is not a version is left alone.

    Each ruleset file is created the first time and updated in place after that, matched by its name, so the files are the one definition of every
    ruleset; a ruleset on the repository that no file defines is deleted. The locked releases file carries no branches of its own; this script fills
    in every release branch but the newest. Calls go through the GitHub CLI, which reads its token from GH_TOKEN.
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
$rulesetDirectory = Join-Path $PSScriptRoot '../rulesets'
$lockedReleasesFile = 'locked-releases.json'

$releases = @(gh api "repos/$p_Repository/branches" --paginate --jq '.[].name' |
    Where-Object { $_.StartsWith($releasePrefix) -and $_ -match $versionPattern } |
    ForEach-Object { [pscustomobject]@{ Name = $_; Version = [version]$Matches.version } } |
    Sort-Object Version)

$newest = if ($releases.Count -gt 0) { $releases[-1].Name } else { $null }
[string[]] $older = @($releases | Select-Object -SkipLast 1 | ForEach-Object { "$branchRefPrefix$($_.Name)" })

Write-Host "Newest release, left open: $(if ($newest) { $newest } else { '(none)' })"
Write-Host "Locked: $(if ($older.Count -gt 0) { $older -join ', ' } else { '(none)' })"

$existingIds = @{}
gh api "repos/$p_Repository/rulesets" --paginate --jq '.[] | "\(.id)\t\(.name)"' | ForEach-Object {
    $id, $name = $_ -split "`t", 2
    $existingIds[$name] = $id
}

foreach ($file in Get-ChildItem $rulesetDirectory -Filter '*.json' | Sort-Object Name) {
    $ruleset = Get-Content $file.FullName -Raw | ConvertFrom-Json
    $existingId = $existingIds[$ruleset.name]
    $existingIds.Remove($ruleset.name)

    if ($file.Name -eq $lockedReleasesFile) {
        if ($older.Count -eq 0) {
            Write-Host "Skipped ruleset '$($ruleset.name)': no release branch is older than the newest."
            continue
        }

        $ruleset.conditions.ref_name.include = $older
    }

    $body = $ruleset | ConvertTo-Json -Depth 10

    if ($existingId) {
        $body | gh api --method PUT "repos/$p_Repository/rulesets/$existingId" --input - --silent
        Write-Host "Updated ruleset '$($ruleset.name)' ($existingId)."
    }
    else {
        $body | gh api --method POST "repos/$p_Repository/rulesets" --input - --silent
        Write-Host "Created ruleset '$($ruleset.name)'."
    }
}

foreach ($name in $existingIds.Keys) {
    gh api --method DELETE "repos/$p_Repository/rulesets/$($existingIds[$name])" --silent
    Write-Host "Deleted ruleset '$name' ($($existingIds[$name])): no file defines it."
}

if ($newest) {
    $defaultBranch = gh api "repos/$p_Repository" --jq '.default_branch'
    if ($defaultBranch -ne $newest) {
        gh api --method PATCH "repos/$p_Repository" -f "default_branch=$newest" --silent
        Write-Host "Default branch changed from $defaultBranch to $newest."
    }
    else {
        Write-Host "Default branch is already $newest."
    }
}
