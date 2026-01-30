$ErrorActionPreference = "Stop"
Write-Host "Starting Aria..."
dotnet run --project "$PSScriptRoot/../src/Aria.App/Aria.App.csproj"
