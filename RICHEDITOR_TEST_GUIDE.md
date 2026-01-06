# Test RichTextEditor - Instrukcja

Stworzy³em stronê testow¹ `RichEditorTestPage`, która pozwala sprawdziæ, czy `richeditor.html` poprawnie zwraca wartoœci.

## Jak uruchomiæ stronê testow¹

### Opcja 1: Z kodu C#
Dodaj w dowolnym miejscu aplikacji (np. w przycisku lub podczas debugowania):

```csharp
await Shell.Current.GoToAsync("RichEditorTestPage");
```

### Opcja 2: Przez Immediate Window podczas debugowania
1. Uruchom aplikacjê w trybie debugowania
2. Otwórz **Immediate Window** (Debug ? Windows ? Immediate)
3. Wpisz:
```csharp
Shell.Current.GoToAsync("RichEditorTestPage").Wait()
```

### Opcja 3: Tymczasowy przycisk testowy
Mo¿esz dodaæ tymczasowy przycisk na g³ównym ekranie. W pliku `RecipesPage.xaml` dodaj:

```xml
<Button Text="TEST EDITOR" 
        Clicked="OnTestEditorClicked"
        BackgroundColor="Red"/>
```

A w `RecipesPage.xaml.cs`:

```csharp
private async void OnTestEditorClicked(object sender, EventArgs e)
{
    await Shell.Current.GoToAsync(nameof(RichEditorTestPage));
}
```

## Co testuje strona

### 1. **Test komunikacji JS ? C#**
- Sprawdza czy funkcje JavaScript s¹ wywo³ywalne z C#
- Wywo³uje `testCommunication()` w JavaScript
- Powinna zwróciæ wiadomoœæ z dat¹ i czasem

### 2. **Pobierz zawartoœæ (GetContentAsync)**
- Pobiera aktualn¹ zawartoœæ HTML z edytora
- Wywo³uje `getContent()` w JavaScript
- Wyœwietla d³ugoœæ i pe³ny HTML

### 3. **Ustaw przyk³adow¹ zawartoœæ**
- Ustawia przyk³adowy HTML z nag³ówkami, listami, formatowaniem
- Testuje dwukierunkowe wi¹zanie (binding)
- Wywo³uje `setContent()` w JavaScript

### 4. **Wyczyœæ zawartoœæ**
- Czyœci edytor
- Testuje czy binding dzia³a w obie strony

## Diagnostyka

### Logi w Output Window
Wszystkie operacje s¹ logowane do Output Window w Visual Studio z prefiksem `[RichEditorTest]`:

```
[RichEditorTest] EditorContent changed: <h1>Przyk³adowy przepis</h1>...
[RichEditorTest] GetContentAsync returned: <p>Zawartoœæ...</p>
```

### Logi JavaScript w przegl¹darce
W pliku `richeditor.html` doda³em funkcje logowania:
- `console.log('[RichEditor] ...')` - wszystkie operacje s¹ logowane
- Na Windows mo¿esz zobaczyæ logi w DevTools WebView2

### Panel wyników na stronie
- **Wynik** - pokazuje rezultat ostatniej operacji (test, get, set)
- **Wartoœæ z bindingu** - pokazuje aktualn¹ wartoœæ w³aœciwoœci `HtmlContent` przez binding

## Znane problemy i ich rozwi¹zania

### Problem: "ERROR: WebView not initialized"
**Przyczyna:** Próba wywo³ania funkcji przed za³adowaniem WebView  
**Rozwi¹zanie:** Poczekaj chwilê po otwarciu strony (WebView musi za³adowaæ HTML)

### Problem: Nie otrzymujê zawartoœci z GetContentAsync
**Mo¿liwe przyczyny:**
1. WebView nie za³adowa³ siê - sprawdŸ czy edytor Quill jest widoczny
2. Funkcja JavaScript nie istnieje - sprawdŸ konsolê przegl¹darki
3. Problem z escapowaniem znaków - sprawdŸ logi

### Problem: SetContent nie dzia³a
**Mo¿liwe przyczyny:**
1. Problem z escapowaniem HTML - sprawdŸ metodê `SetContentAsync` w `RichTextEditor.cs`
2. Quill nie zosta³ zainicjalizowany - sprawdŸ console.log w WebView

### Problem: Binding nie aktualizuje siê automatycznie (tylko Windows)
**Przyczyna:** Obs³uga `WebMessageReceived` jest obecnie tylko dla Windows  
**Rozwi¹zanie:** Dla innych platform dodaj odpowiedni handler:

**iOS/macOS:**
```csharp
// W RichTextEditor.cs dodaj handler dla iOS
#elif IOS || MACCATALYST
// Implementacja dla webkit.messageHandlers
#endif
```

**Android:**
```csharp
#elif ANDROID
// Implementacja dla Android WebView
#endif
```

## Debugging WebView

### Windows (WebView2)
1. Uruchom aplikacjê
2. Otwórz Edge DevTools:
   - Edge ? Wiêcej narzêdzi ? Narzêdzia dla deweloperów ? Po³¹cz z WebView
   - Lub u¿yj `edge://inspect`
3. SprawdŸ zak³adkê Console dla logów JavaScript

### Android
1. W Chrome otwórz `chrome://inspect`
2. ZnajdŸ swoj¹ aplikacjê w liœcie urz¹dzeñ
3. Kliknij "Inspect"

### iOS/macOS
1. Safari ? Develop ? [nazwa urz¹dzenia] ? [WebView]
2. SprawdŸ Console

## Struktura plików

```
ReceptyOks/
??? Views/
?   ??? RichEditorTestPage.cs          # Strona testowa
??? Controls/
?   ??? RichTextEditor.cs              # Kontrolka edytora (zaktualizowana)
??? Resources/
?   ??? Raw/
?       ??? richeditor.html            # HTML z Quill.js (zaktualizowany)
??? AppShell.xaml.cs                    # Zarejestrowana trasa
```

## Nastêpne kroki

Po potwierdzeniu, ¿e komunikacja dzia³a poprawnie:

1. **Dodaj obs³ugê dla innych platform** (iOS, Android) w `RichTextEditor.cs`
2. **Optymalizuj** - ogranicz czêstotliwoœæ powiadomieñ o zmianach
3. **Dodaj wiêcej funkcji** - kolory, rozmiary czcionek, obrazki
4. **Popraw escapowanie** - u¿yj JSON zamiast prostego escapowania stringów

## Pytania?

Jeœli masz problemy:
1. SprawdŸ Output Window w Visual Studio
2. SprawdŸ Console w WebView DevTools
3. Upewnij siê, ¿e plik `richeditor.html` jest ustawiony jako **MauiAsset**
