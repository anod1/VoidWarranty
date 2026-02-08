# ========================================
# GENERATEUR DE README AVEC LIENS RAW
# ========================================
# Ce script scanne ton projet Unity et genere un README.md
# avec tous les liens raw GitHub de tes scripts .cs
# ========================================

# Configuration - MODIFIE CES VALEURS SI NECESSAIRE
$repoUser = "anod1"
$repoName = "VoidWarranty"
$branch = "main"
$baseRawUrl = "https://raw.githubusercontent.com/$repoUser/$repoName/$branch"

# Dossier a scanner
$scriptsFolder = "Assets/_Game/Scripts"

Write-Host "Scanning $scriptsFolder..." -ForegroundColor Cyan

# Recupere tous les fichiers .cs recursivement
$allScripts = Get-ChildItem -Path $scriptsFolder -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue

if ($allScripts.Count -eq 0) {
    Write-Host "ERREUR: Aucun script trouve dans $scriptsFolder" -ForegroundColor Red
    Write-Host "Verifie que tu es bien a la racine du projet Unity" -ForegroundColor Yellow
    exit
}

# Groupe les scripts par dossier
$scriptsByFolder = $allScripts | Group-Object { $_.DirectoryName }

# Debut du README
$readmeContent = @()
$readmeContent += "# VoidWarranty - Technical Documentation"
$readmeContent += ""
$readmeContent += "**Technician multiplayer game built with Unity and FishNet**"
$readmeContent += ""
$readmeContent += "## Project Overview"
$readmeContent += ""
$readmeContent += "Multiplayer co-op game where players repair infected machines/patients on a spaceship."
$readmeContent += ""
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "## Scripts Structure"
$readmeContent += ""

# Pour chaque dossier
foreach ($folder in $scriptsByFolder | Sort-Object Name) {
    # Nom du dossier (derniere partie du chemin)
    $folderName = Split-Path $folder.Name -Leaf
    
    $readmeContent += "### $folderName"
    $readmeContent += ""
    
    # Pour chaque script dans ce dossier
    foreach ($script in $folder.Group | Sort-Object Name) {
        $scriptName = $script.Name
        
        # Chemin relatif pour GitHub (remplace \ par /)
        $relativePath = $script.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
        $rawUrl = "$baseRawUrl/$relativePath"
        
        $readmeContent += "- **[$scriptName]($rawUrl)**"
    }
    
    $readmeContent += ""
}

# Section problemes connus
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "## Known Issues / Work In Progress"
$readmeContent += ""
$readmeContent += "### Fixed"
$readmeContent += "- [x] Character Controller center offset bug"
$readmeContent += "- [x] Crouch system collision detection"
$readmeContent += ""
$readmeContent += "### Current Work"
$readmeContent += "- [ ] Scanner target priority system"
$readmeContent += "- [ ] Network synchronization improvements"
$readmeContent += "- [ ] Patient repair progress UI"
$readmeContent += ""
$readmeContent += "### Planned Features"
$readmeContent += "- [ ] Multiple ship levels"
$readmeContent += "- [ ] Advanced toolbox mechanics"
$readmeContent += "- [ ] Infection spread system"
$readmeContent += ""
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "## Tech Stack"
$readmeContent += ""
$readmeContent += "- **Engine**: Unity 2022.3 LTS"
$readmeContent += "- **Networking**: FishNet"
$readmeContent += "- **Input**: New Input System"
$readmeContent += "- **Rendering**: URP"
$readmeContent += ""
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "## Quick Links to Main Systems"
$readmeContent += ""

# Liens rapides vers scripts principaux
$mainScripts = @(
    "Assets/_Game/Scripts/Player/PlayerMovement.cs",
    "Assets/_Game/Scripts/Player/PlayerGrab.cs",
    "Assets/_Game/Scripts/Interaction/GrabbableObject.cs",
    "Assets/_Game/Scripts/Interaction/PatientObject.cs",
    "Assets/_Game/Scripts/Interaction/Scanner.cs"
)

foreach ($scriptPath in $mainScripts) {
    if (Test-Path $scriptPath) {
        $scriptName = Split-Path $scriptPath -Leaf
        $githubPath = $scriptPath.Replace("\", "/")
        $rawUrl = "$baseRawUrl/$githubPath"
        $readmeContent += "- [$scriptName]($rawUrl)"
    }
}

$readmeContent += ""
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "## Setup Instructions"
$readmeContent += ""
$readmeContent += "1. Clone the repository"
$readmeContent += "2. Open with Unity 2022.3+"
$readmeContent += "3. FishNet will auto-import dependencies"
$readmeContent += "4. Open _Game/Scenes/MainScene"
$readmeContent += ""
$readmeContent += "---"
$readmeContent += ""
$readmeContent += "**Last Updated**: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$readmeContent += ""

# Sauvegarde le fichier
$readmeContent | Out-File -FilePath "README.md" -Encoding utf8

Write-Host ""
Write-Host "README.md generated successfully!" -ForegroundColor Green
Write-Host "Found $($allScripts.Count) scripts" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  git add README.md"
Write-Host "  git commit -m ""docs: add automated README with script links"""
Write-Host "  git push"
Write-Host ""