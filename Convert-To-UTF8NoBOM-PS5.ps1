# ========================================
# CONVERTISSEUR UTF-8 SANS BOM
# PowerShell 5.1 Compatible
# ========================================

$scriptsFolder = "Assets\_Game\Scripts"

Write-Host "Scanning for .cs files in $scriptsFolder..." -ForegroundColor Cyan

# Recupere tous les fichiers .cs
$allFiles = Get-ChildItem -Path $scriptsFolder -Filter "*.cs" -Recurse

if ($allFiles.Count -eq 0) {
    Write-Host "ERROR: No .cs files found!" -ForegroundColor Red
    exit
}

Write-Host "Found $($allFiles.Count) files to convert" -ForegroundColor Yellow
Write-Host ""

# Creation de l'encodeur UTF-8 sans BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

$converted = 0
$errors = 0

foreach ($file in $allFiles) {
    try {
        # Lit le contenu
        $content = [System.IO.File]::ReadAllText($file.FullName)
        
        # Ecrit en UTF-8 sans BOM
        [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
        
        $converted++
        Write-Host "[OK] $($file.Name)" -ForegroundColor Green
    }
    catch {
        $errors++
        Write-Host "[ERREUR] $($file.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Conversion terminee!" -ForegroundColor Green
Write-Host "Convertis: $converted" -ForegroundColor Green
Write-Host "Erreurs: $errors" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($converted -gt 0) {
    Write-Host "Prochaines etapes:" -ForegroundColor Yellow
    Write-Host "1. git add ." -ForegroundColor White
    Write-Host "2. git commit -m 'fix: convert .cs files to UTF-8 without BOM'" -ForegroundColor White
    Write-Host "3. git push" -ForegroundColor White
    Write-Host ""
    Write-Host "Apres le push, tous les liens raw fonctionneront!" -ForegroundColor Cyan
}
