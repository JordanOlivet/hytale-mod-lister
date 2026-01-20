# Script de demarrage pour le developpement local
# Lance le backend .NET et le frontend Svelte en parallele

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=== Hytale Mod Lister - Dev Mode ===" -ForegroundColor Cyan
Write-Host ""

# Demarrer le backend dans une nouvelle fenetre
Write-Host "[Backend] Demarrage sur http://localhost:5000..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$scriptDir\backend\HytaleModLister.Api'; `$env:ADMIN_PASSWORD='test'; dotnet run"

# Attendre un peu que le backend demarre
Start-Sleep -Seconds 2

# Demarrer le frontend dans une nouvelle fenetre
Write-Host "[Frontend] Demarrage sur http://localhost:5173..." -ForegroundColor Blue
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$scriptDir\frontend'; npm run dev"

Write-Host ""
Write-Host "Les deux applications demarrent dans des fenetres separees." -ForegroundColor Yellow
Write-Host ""
Write-Host "URLs:" -ForegroundColor Cyan
Write-Host "  Frontend: http://localhost:5173" -ForegroundColor White
Write-Host "  Backend:  http://localhost:5000/api/mods" -ForegroundColor White
Write-Host ""
Write-Host "Admin login: password = 'test'" -ForegroundColor Magenta
Write-Host ""
Write-Host "Fermez les fenetres PowerShell pour arreter les serveurs." -ForegroundColor Gray
