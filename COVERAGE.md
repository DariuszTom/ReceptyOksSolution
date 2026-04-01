# Code Coverage - Quick Guide

## 🚀 Uruchomienie lokalnie

```powershell
# Prosty sposób
.\run-coverage.ps1

# Bez otwierania raportu w przeglądarce
.\run-coverage.ps1 -OpenReport $false
```

## 📊 Aktualne progi

Pipeline sprawdza minimalne wartości:
- **Line Coverage**: 25% (zwiększaj stopniowo)
- **Branch Coverage**: 20%

## 📈 Obecny stan (z twojego screenshota)

| Assembly | Line Coverage | Blocks Coverage |
|----------|---------------|-----------------|
| receptyoks_unittests.dll | 82.5% ✅ | 83.7% ✅ |
| receptyoks.shared.dll | 42.4% ✅ | 89.7% ✅ |
| receptyoks.blazorcomponents.dll | 19.9% ❌ | 13.7% ❌ |
| receptyoks-api.dll | 7.3% ❌ | 11.7% ❌ |
| receptyoks.dll (MAUI) | **3.9%** ❌ | **3.9%** ❌ |

**Średnia całkowita**: ~26% line coverage

## 🎯 Strategia poprawy

### Priorytet 1: receptyoks.dll (MAUI app)
To główny projekt - 3.9% to bardzo mało!

Dodaj testy dla:
- ViewModels (RecipesViewModel, SettingsViewModel, LoginViewModel)
- Services (LocalDatabase, SyncService)
- Converters
- Helpers

### Priorytet 2: receptyoks-api.dll
Dodaj integration testy dla endpoints:
- SyncEndpoints
- RecipeEndpoints

### Priorytet 3: receptyoks.blazorcomponents.dll
Użyj bUnit do testowania komponentów Blazor

## 📂 Artefakty

Po uruchomieniu `.\run-coverage.ps1`:
- HTML Report: `./coverage-report/index.html`
- Cobertura XML: `./coverage/**/coverage.cobertura.xml`
- Badges: `./coverage-report/badge_*.svg`
