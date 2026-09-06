# Extracts every yearly zip file from the JMA shindo database download
# into a single destination folder, so the resulting .dat files can be
# combined and fed to convert_jma_dat.py.
#
# Usage (from PowerShell), passing the actual folder paths yourself:
#   powershell -ExecutionPolicy Bypass -File tools\extract_jma_zips.ps1 -SourceFolder "C:\path\to\zips" -DestinationFolder "C:\path\to\extracted"

param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFolder,

    [Parameter(Mandatory=$true)]
    [string]$DestinationFolder
)

if (-not (Test-Path $DestinationFolder)) {
    New-Item -ItemType Directory -Path $DestinationFolder | Out-Null
}

$zipFiles = Get-ChildItem -Path $SourceFolder -Filter "*.zip"

if ($zipFiles.Count -eq 0) {
    Write-Host "No zip files found in $SourceFolder"
    exit 1
}

foreach ($zip in $zipFiles) {
    Write-Host "Extracting $($zip.Name) ..."
    Expand-Archive -Path $zip.FullName -DestinationPath $DestinationFolder -Force
}

Write-Host "Done. Extracted $($zipFiles.Count) zip files into $DestinationFolder"
