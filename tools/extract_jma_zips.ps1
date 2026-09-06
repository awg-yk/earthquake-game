# Extracts every yearly zip file (i1919.zip ... i2022.zip) from the JMA
# 震度データベース download into a single "extracted" folder, so all the
# resulting .dat files can be combined and fed to convert_jma_dat.py.
#
# Usage (from PowerShell):
#   powershell -ExecutionPolicy Bypass -File tools\extract_jma_zips.ps1 `
#       -SourceFolder "$env:USERPROFILE\Desktop\地震" `
#       -DestinationFolder "$env:USERPROFILE\Desktop\地震\extracted"

param(
    [string]$SourceFolder = "$env:USERPROFILE\Desktop\地震",
    [string]$DestinationFolder = "$env:USERPROFILE\Desktop\地震\extracted"
)

if (-not (Test-Path $DestinationFolder)) {
    New-Item -ItemType Directory -Path $DestinationFolder | Out-Null
}

$zipFiles = Get-ChildItem -Path $SourceFolder -Filter "*.zip"

if ($zipFiles.Count -eq 0) {
    Write-Host "No .zip files found in $SourceFolder"
    exit 1
}

foreach ($zip in $zipFiles) {
    Write-Host "Extracting $($zip.Name) ..."
    Expand-Archive -Path $zip.FullName -DestinationPath $DestinationFolder -Force
}

Write-Host "Done. Extracted $($zipFiles.Count) zip files into $DestinationFolder"
