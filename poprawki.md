Proponowane poprawki dla CFAB Command Launcher

1.  Main.cs - Poprawa obsługi ikon w funkcji CreateResult
    Plik: Main.cs
    Funkcja: CreateResult
    Problem: Nieefektywna obsługa ścieżek ikon i potencjalne błędy przy dostępie do plików
    csharpprivate Result CreateResult(CommandEntry cmd)
    {
    string iconPath = \_iconPath; // Domyślnie używamy ikony wtyczki

        // Próbujemy pobrać ikonę z pliku wykonywalnego
        try
        {
            string cleanPath = cmd.Code.Trim('"');
            // Sprawdź czy to ścieżka bezwzględna i czy plik istnieje
            if (Path.IsPathRooted(cleanPath) && File.Exists(cleanPath))
            {
                iconPath = cleanPath; // Flow Launcher automatycznie pobierze ikonę z pliku
            }
            else if (!Path.IsPathRooted(cleanPath))
            {
                // Dla komend systemowych spróbuj znaleźć w PATH
                string fullPath = FindInPath(cleanPath);
                if (!string.IsNullOrEmpty(fullPath))
                {
                    iconPath = fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            _context.API.LogDebug("CreateResult", $"Nie można pobrać ikony z pliku: {ex.Message}");
        }

        return new Result
        {
            Title = $"{cmd.Info} ({cmd.Key})",
            SubTitle = $"Uruchom: {cmd.Code}",
            IcoPath = iconPath,
            Action = _ => ExecuteCommand(cmd.Code)
        };

    }

// Nowa funkcja pomocnicza
private static string FindInPath(string fileName)
{
var pathVariable = Environment.GetEnvironmentVariable("PATH");
if (string.IsNullOrEmpty(pathVariable)) return string.Empty;

    var paths = pathVariable.Split(';');
    foreach (var path in paths)
    {
        try
        {
            var fullPath = Path.Combine(path.Trim(), fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        catch
        {
            // Ignoruj błędy dla nieprawidłowych ścieżek w PATH
        }
    }
    return string.Empty;

} 2. CommandsManager.cs - Poprawa obsługi błędów w funkcji LoadCommands
Plik: CommandsManager.cs
Funkcja: LoadCommands
Problem: Brak sprawdzenia dostępności pliku przed odczytem oraz lepszej obsługi błędów JSON
csharppublic void LoadCommands()
{
try
{
if (!File.Exists(\_commandsFilePath))
{
Commands = GetDefaultCommands();
SaveCommands();
return;
}

        // Sprawdź czy plik nie jest zablokowany przez inny proces
        using (var fileStream = new FileStream(_commandsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fileStream.Length == 0)
            {
                LoadDefaultCommandsWithLog("Plik commands.json jest pusty");
                return;
            }

            string json = File.ReadAllText(_commandsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                LoadDefaultCommandsWithLog("Plik commands.json zawiera tylko białe znaki");
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            Commands = JsonSerializer.Deserialize<List<CommandEntry>>(json, options) ?? new List<CommandEntry>();

            // Filtruj nieprawidłowe wpisy
            var validCommands = Commands.Where(cmd => cmd.IsValid()).ToList();
            if (validCommands.Count != Commands.Count)
            {
                _context.API.LogWarn(nameof(CommandsManager),
                    $"Usunięto {Commands.Count - validCommands.Count} nieprawidłowych komend");
                Commands = validCommands;
                SaveCommands(); // Zapisz oczyszczoną listę
            }

            if (Commands.Count == 0)
            {
                LoadDefaultCommandsWithLog("Brak prawidłowych komend w pliku");
            }
        }
    }
    catch (UnauthorizedAccessException ex)
    {
        _context.API.LogException(nameof(CommandsManager), "Brak uprawnień do odczytu pliku commands.json", ex);
        LoadDefaultCommandsWithLog("Brak uprawnień do odczytu pliku");
    }
    catch (JsonException ex)
    {
        _context.API.LogException(nameof(CommandsManager), $"Błąd struktury JSON: {ex.Message}", ex);
        LoadDefaultCommandsWithLog($"Nieprawidłowy format JSON: {ex.Message}");
    }
    catch (Exception ex)
    {
        _context.API.LogException(nameof(CommandsManager), $"Nieoczekiwany błąd wczytywania commands.json: {ex.Message}", ex);
        LoadDefaultCommandsWithLog($"Nieoczekiwany błąd: {ex.Message}");
    }

} 3. SettingsControl.xaml.cs - Poprawa walidacji w funkcji TryCreateValidCommand
Plik: SettingsControl.xaml.cs
Funkcja: TryCreateValidCommand
Problem: Brak walidacji dla potencjalnych problemów bezpieczeństwa i lepszej obsługi różnych typów komend
csharpprivate bool TryCreateValidCommand(out CommandEntry command, out string errorMessage)
{
var key = EditKey?.Trim() ?? string.Empty;
var code = EditCode?.Trim() ?? string.Empty;
var info = EditInfo?.Trim() ?? string.Empty;

    // Walidacja klucza - nie może zawierać białych znaków
    if (key.Contains(" ") || key.Contains("\t"))
    {
        command = new CommandEntry();
        errorMessage = "Klucz nie może zawierać spacji ani tabulatorów!";
        return false;
    }

    // Automatyczne dodanie cudzysłowów dla ścieżek ze spacjami (nie ruszamy tej części)
    if (!string.IsNullOrEmpty(code) &&
        code.Contains(" ") &&
        !code.StartsWith("\"") &&
        (code.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
         code.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
         Path.IsPathRooted(code)))
    {
        code = $"\"{code}\"";
    }

    command = new CommandEntry(key, code, info);
    errorMessage = "";

    if (!command.IsValid())
    {
        errorMessage = string.IsNullOrWhiteSpace(command.Key) ?
            "Klucz jest wymagany!" : "Komenda/Ścieżka jest wymagana!";
        return false;
    }

    // Rozszerzona walidacja dla plików
    var cleanCode = command.Code.Trim('"');
    if (Path.IsPathRooted(cleanCode))
    {
        try
        {
            if (!File.Exists(cleanCode) && !Directory.Exists(cleanCode))
            {
                errorMessage = $"Plik lub folder nie istnieje: {cleanCode}";
                return false;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd sprawdzania ścieżki: {ex.Message}";
            return false;
        }
    }

    // Ostrzeżenie dla potencjalnie niebezpiecznych komend
    var dangerousCommands = new[] { "format", "del", "rd", "rmdir", "deltree" };
    if (dangerousCommands.Any(cmd => cleanCode.StartsWith(cmd, StringComparison.OrdinalIgnoreCase)))
    {
        var result = MessageBox.Show(
            "Ta komenda może być potencjalnie niebezpieczna. Czy na pewno chcesz ją dodać?",
            "Ostrzeżenie bezpieczeństwa",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            errorMessage = "Operacja anulowana przez użytkownika";
            return false;
        }
    }

    return true;

} 4. CommandEntry.cs - Dodanie lepszej walidacji
Plik: CommandEntry.cs
Funkcja: IsValid i konstruktor
Problem: Brak walidacji długości i znaków specjalnych
csharppublic class CommandEntry
{
[JsonPropertyName("key")]
[Required(ErrorMessage = "Klucz jest wymagany")]
[StringLength(50, ErrorMessage = "Klucz nie może być dłuższy niż 50 znaków")]
public string Key { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    [Required(ErrorMessage = "Kod jest wymagany")]
    [StringLength(500, ErrorMessage = "Kod nie może być dłuższy niż 500 znaków")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("info")]
    [StringLength(200, ErrorMessage = "Opis nie może być dłuższy niż 200 znaków")]
    public string Info { get; set; } = string.Empty;

    // Konstruktor bezparametrowy potrzebny do deserializacji
    public CommandEntry() { }

    public CommandEntry(string key, string code, string info = "")
    {
        Key = key?.Trim() ?? string.Empty;
        Code = code?.Trim() ?? string.Empty;
        Info = info?.Trim() ?? string.Empty;
    }

    // Rozszerzona walidacja
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Code))
            return false;

        // Sprawdź długość
        if (Key.Length > 50 || Code.Length > 500 || Info.Length > 200)
            return false;

        // Sprawdź czy klucz nie zawiera niedozwolonych znaków
        var invalidChars = new char[] { ' ', '\t', '\n', '\r', '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        if (Key.IndexOfAny(invalidChars) >= 0)
            return false;

        return true;
    }

    // Nowa metoda do walidacji z szczegółowymi komunikatami
    public ValidationResult ValidateDetailed()
    {
        if (string.IsNullOrWhiteSpace(Key))
            return new ValidationResult("Klucz jest wymagany");

        if (string.IsNullOrWhiteSpace(Code))
            return new ValidationResult("Kod jest wymagany");

        if (Key.Length > 50)
            return new ValidationResult("Klucz nie może być dłuższy niż 50 znaków");

        if (Code.Length > 500)
            return new ValidationResult("Kod nie może być dłuższy niż 500 znaków");

        if (Info.Length > 200)
            return new ValidationResult("Opis nie może być dłuższy niż 200 znaków");

        var invalidChars = new char[] { ' ', '\t', '\n', '\r', '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        if (Key.IndexOfAny(invalidChars) >= 0)
            return new ValidationResult("Klucz zawiera niedozwolone znaki");

        return ValidationResult.Success!;
    }

}

public class ValidationResult
{
public bool IsValid { get; }
public string ErrorMessage { get; }
public static ValidationResult Success { get; } = new ValidationResult(true, string.Empty);

    public ValidationResult(string errorMessage) : this(false, errorMessage) { }

    private ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

} 5. plugin.json - Aktualizacja metadanych
Plik: plugin.json
Problem: Brak niektórych nowoczesnych właściwości dla Flow Launcher
json{
"ID": "73c151c9-3384-4c31-a707-2b256d559c24",
"ActionKeyword": "aa",
"Name": "CFAB Command Launcher",
"Description": "Uruchamia zdefiniowane programy/komendy za pomocą skrótów.",
"Author": "TwojeImię",
"Version": "1.0.0",
"Language": "csharp",
"Website": "https://github.com/TwojeRepo",
"IcoPath": "Images\\app.png",
"ExecuteFileName": "Flow.Plugin.CommandLauncher.dll",
"SupportedOS": ["Win32NT"],
"Dependencies": []
}
Te poprawki skupiają się na:

Lepszej obsłudze błędów - bardziej precyzyjne przechwytywanie i logowanie błędów
Walidacji danych - zapobieganie problemom z nieprawidłowymi danymi wejściowymi
Bezpieczeństwie - ostrzeżenia przed potencjalnie niebezpiecznymi komendami
Stabilności - lepsze zarządzanie zasobami i dostępem do plików
Zgodności - aktualizacja do najnowszych standardów Flow Launcher

Wszystkie te zmiany nie wpływają na obsługę ścieżek ze spacjami, która została oznaczona jako kluczowa funkcjonalność do zachowania bez zmian.
