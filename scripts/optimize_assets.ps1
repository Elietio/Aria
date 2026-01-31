$ErrorActionPreference = "Stop"

# Portable ImageMagick URL (Official)
$magickVersion = "7.1.1-27" # Use a known stable version or check latest
# Note: Official portable builds are often tar.gz or zip. Let's use a reliable mirror or github release if possible.
# Actually, downloading ImageMagick portable is huge (~100MB).
# But if the user ASKED for it, maybe they have it installed?

$targetFolder = "src/Aria.App/Assets/Moe/Variations"
$fullTargetPath = Join-Path $PSScriptRoot ".." $targetFolder
$fullTargetPath = [System.IO.Path]::GetFullPath($fullTargetPath)

Write-Host "Checking for ImageMagick..." -ForegroundColor Cyan

if (Get-Command magick -ErrorAction SilentlyContinue) {
    Write-Host "ImageMagick found!" -ForegroundColor Green
    $magick = "magick"
}
else {
    Write-Host "ImageMagick not found in PATH." -ForegroundColor Yellow
    Write-Host "Attempting to download portable version..."
    
    $tempDir = Join-Path $env:TEMP "aria_magick"
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    
    # Download a smaller standalone mogrify if possible? No, usually comprehensive.
    # Let's download the official portable zip.
    $url = "https://imagemagick.org/archive/binaries/ImageMagick-7.1.1-29-portable-Q16-x64.zip"
    $zipPath = Join-Path $tempDir "magick.zip"
    
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
        Expand-Archive -Path $zipPath -DestinationPath $tempDir
        $magick = Join-Path $tempDir "magick.exe"
    }
    catch {
        Write-Host "Failed to download ImageMagick. Please install it manually." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Starting optimization in: $fullTargetPath" -ForegroundColor Cyan

# 1. Resize if huge (e.g. limit to 2048px width)
# 2. Reduce quality to 85% (Visually lossless usually)
# 3. Strip metadata

$files = Get-ChildItem -Path $fullTargetPath -Filter "*.png"
$totalSaved = 0

foreach ($file in $files) {
    $originalSize = $file.Length
    
    # Command: magick input -resize '2048x>' -quality 85 -strip output
    # '2048x>' means only resize if larger than 2048px width/height
    
    $cmdArgs = @(
        $file.FullName,
        "-resize", "1500x>",       # Limit max dimension to 1500px
        "-colors", "256",          # Reduce to 256 colors
        "-strip",                  # Remove metadata
        $file.FullName             # Overwrite
    )
    
    try {
        Start-Process -FilePath $magick -ArgumentList $cmdArgs -NoNewWindow -Wait
        
        $newSize = (Get-Item $file.FullName).Length
        $saved = $originalSize - $newSize
        
        if ($saved -gt 0) {
            $totalSaved += $saved
            Write-Host "Optimized: $($file.Name) | Saved: $([math]::Round($saved/1KB, 2)) KB" -ForegroundColor Green
        }
        else {
            Write-Host "Skipped: $($file.Name)" -ForegroundColor DarkGray
        }
    }
    catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }
}

Write-Host "`nTotal space saved: $([math]::Round($totalSaved/1MB, 2)) MB" -ForegroundColor Yellow

# Clean up temp
if ($tempDir) {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
