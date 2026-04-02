param(
    [switch]$NoBuild,
    [switch]$Wait
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "WinNotes.Client\WinNotes.Client.csproj"
$localDotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"

if (Test-Path $localDotnet) {
    $dotnet = $localDotnet
} else {
    $dotnet = "dotnet"
}

$arguments = @("run", "--project", $projectPath)
if ($NoBuild) {
    $arguments += "--no-build"
}

if ($Wait) {
    & $dotnet @arguments
    exit $LASTEXITCODE
}

$process = Start-Process -FilePath $dotnet -ArgumentList $arguments -WorkingDirectory $repoRoot -PassThru
Write-Output "WinNotes started. PID: $($process.Id)"
