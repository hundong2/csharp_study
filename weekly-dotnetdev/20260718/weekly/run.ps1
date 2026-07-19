param(
    [ValidateSet("all", "process", "false-sharing", "query-budget", "help")]
    [string] $Demo = "all"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\WeeklyDotNetDev36\WeeklyDotNetDev36.csproj"

dotnet run --project $project -- $Demo
