[CmdletBinding()]
param(
    [string]$SolutionFile,
    [string]$AuditJsonPath
)

$ErrorActionPreference = 'Stop'

if ($AuditJsonPath) {
    $auditJson = Get-Content -LiteralPath $AuditJsonPath -Raw
}
else {
    if (-not $SolutionFile) {
        throw 'SolutionFile is required when AuditJsonPath is not supplied.'
    }

    $auditOutput = & dotnet list $SolutionFile package `
        --include-transitive `
        --vulnerable `
        --format json `
        --output-version 1 `
        --no-restore

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet package vulnerability audit failed with exit code $LASTEXITCODE."
    }

    $auditJson = $auditOutput -join [Environment]::NewLine
}

$audit = $auditJson | ConvertFrom-Json
$blockingVulnerabilities = @()

foreach ($project in @($audit.projects)) {
    foreach ($framework in @($project.frameworks)) {
        $packages = @($framework.topLevelPackages) + @($framework.transitivePackages)

        foreach ($package in $packages) {
            foreach ($vulnerability in @($package.vulnerabilities)) {
                if ($vulnerability.severity -in @('High', 'Critical')) {
                    $blockingVulnerabilities += [pscustomobject]@{
                        Project = $project.path
                        Framework = $framework.framework
                        Package = $package.id
                        Version = $package.resolvedVersion
                        Severity = $vulnerability.severity
                        Advisory = $vulnerability.advisoryurl
                    }
                }
            }
        }
    }
}

if ($blockingVulnerabilities.Count -gt 0) {
    $blockingVulnerabilities | Format-Table -AutoSize | Out-String | Write-Error
    throw "NuGet audit found $($blockingVulnerabilities.Count) HIGH or CRITICAL package vulnerability finding(s)."
}

Write-Output 'NuGet audit found no HIGH or CRITICAL package vulnerabilities.'
