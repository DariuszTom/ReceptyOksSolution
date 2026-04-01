# Code Coverage

## 🚀 Uruchom lokalnie

```powershell
# Prosty sposób - automatycznie otworzy raport
.\run-coverage.ps1

# Bez otwierania przeglądarki
.\run-coverage.ps1 -OpenReport $false
```

## 📊 W Visual Studio

1. **Test Explorer** → Ctrl+E, T
2. **Analyze Code Coverage** → ikona osłonki
3. Zobacz wyniki w **Code Coverage Results**

## 🔄 GitHub Actions

Pipeline automatycznie:
- ✅ Uruchamia testy z coverage przy każdym push/PR
- ✅ Generuje raport HTML
- ✅ Uploaduje artefakty (30 dni)
- ✅ Sprawdza minimum 25% line coverage

## 📈 Obecny stan

| Projekt | Coverage |
|---------|----------|
| ReceptyOks_UnitTests | ~83% ✅ |
| ReceptyOks.Shared | ~89% ✅ |
| ReceptyOks (MAUI) | ~4% ⚠️ |
| ReceptyOks.Api | ~12% ⚠️ |

**Cel**: Zwiększyć coverage projektów głównych do 40%+
