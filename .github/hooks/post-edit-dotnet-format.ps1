$rawInput = [Console]::In.ReadToEnd()

if ([string]::IsNullOrWhiteSpace($rawInput)) {
    Write-Output "Copilot hook: no payload provided."
    exit 0
}

$payload = $rawInput | ConvertFrom-Json

$toolName = $payload.tool_name
if (-not $toolName) {
    $toolName = $payload.toolName
}

if ($toolName -ne "Write" -and $toolName -ne "Edit") {
    exit 0
}

$filePath = $payload.tool_input.file_path
if (-not $filePath) {
    $filePath = $payload.toolInput.file_path
}

if (-not $filePath) {
    Write-Output "Copilot hook: missing file path for tool '$toolName'."
    exit 0
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")

$absolutePath = $filePath
if (-not [System.IO.Path]::IsPathRooted($absolutePath)) {
    $absolutePath = Join-Path $projectRoot.Path $filePath
}

if (-not (Test-Path $absolutePath)) {
    Write-Output "Copilot hook: edited file not found: $filePath"
    exit 0
}

$extension = [System.IO.Path]::GetExtension($absolutePath).ToLowerInvariant()
$supportedExtensions = @(".cs", ".razor", ".cshtml")
if ($supportedExtensions -notcontains $extension) {
    exit 0
}

$resolvedAbsolutePath = (Resolve-Path $absolutePath).Path
$relativePath = $resolvedAbsolutePath.Substring($projectRoot.Path.Length + 1)

Push-Location $projectRoot.Path
try {
    & dotnet format --include "$relativePath" --verbosity minimal --no-restore
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
