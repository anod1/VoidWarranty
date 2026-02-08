# Script simple pour inserer la mise a jour dans le GDD Word
# Usage: .\Insert-GDD-Update.ps1

$gddPath = "G:\UnityGames\VoidWarranty\VoidWarranty_GDD_v0.5.docx"
$outputPath = "G:\UnityGames\VoidWarranty\VoidWarranty_GDD_v0.6.docx"
$updateFile = "G:\UnityGames\VoidWarranty\GDD_Update_v0.6.txt"

Write-Host "Mise a jour du GDD Word..." -ForegroundColor Cyan

try {
    # Lecture du contenu de mise a jour
    $updateContent = Get-Content -Path $updateFile -Raw -Encoding UTF8

    # Ouverture de Word
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false

    # Copie et ouverture du document
    Copy-Item -Path $gddPath -Destination $outputPath -Force
    $doc = $word.Documents.Open($outputPath)

    # Ajout du contenu a la fin
    $range = $doc.Content
    $range.Collapse([Microsoft.Office.Interop.Word.WdCollapseDirection]::wdCollapseEnd)

    # Insertion d'un saut de page
    $range.InsertBreak([Microsoft.Office.Interop.Word.WdBreakType]::wdPageBreak)

    # Insertion du nouveau contenu
    $range.InsertAfter($updateContent)

    # Sauvegarde et fermeture
    $doc.Save()
    $doc.Close()
    $word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()

    Write-Host "Document Word mis a jour: $outputPath" -ForegroundColor Green
}
catch {
    Write-Host "Erreur: $_" -ForegroundColor Red
    Write-Host "Vous pouvez copier manuellement le contenu de $updateFile" -ForegroundColor Yellow
}
