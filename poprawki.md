Poprawki dla CFAB Command Launcher
1. Main.cs - Funkcja ParseCommand
Problem: Funkcja niepoprawnie parsuje ścieżki ze spacjami, co powoduje błędy wykonania komend.
Plik: Main.cs
Funkcja: ParseCommand
csharpprivate static (string exePath, string arguments) ParseCommand(string command)
{
    command = command.Trim();

    // Jeśli ścieżka jest w cudzysłowach
    if (command.StartsWith("\""))
    {
        int endQuote = command.IndexOf('"', 1);
        if (endQuote > 0)
        {
            string path = command.Substring(1, endQuote - 1);
            string args = command.Length > endQuote + 1 ? command.Substring(endQuote + 1).TrimStart() : "";
            return (path, args);
        }
    }

    // Sprawdź czy ścieżka zawiera spacje i czy jest to ścieżka do pliku
    if (command.Contains(" ") && (command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                                command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                                command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)))
    {
        int lastSpace = command.LastIndexOf(' ');
        return (command.Substring(0, lastSpace), command.Substring(lastSpace + 1));
    }

    // Standardowe przetwarzanie
    int spaceIndex = command.IndexOf(' ');
    return spaceIndex > 0
        ? (command.Substring(0, spaceIndex), command.Substring(spaceIndex + 1))
        : (command, "");
}
2. Main.cs - Funkcja ExecuteCommand
Problem: Niepoprawne wywołanie procesu dla ścieżek ze spacjami.
Plik: Main.cs
Funkcja: ExecuteCommand
csharpprivate bool ExecuteCommand(string commandCode)
{
    try
    {
        commandCode = Environment.ExpandEnvironmentVariables(commandCode.Trim());
        _context.API.LogDebug("ExecuteCommand", $"Przetwarzanie: {commandCode}");

        var (exePath, arguments) = ParseCommand(commandCode);

        if (ShouldValidateFile(exePath) && !File.Exists(exePath))
        {
            throw new FileNotFoundException($"Nie można znaleźć pliku: {exePath}");
        }

        // Lepsze formatowanie dla cmd.exe
        var processInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = true,  // Zmiana na true dla lepszej kompatybilności
            CreateNoWindow = false
        };

        // Alternatywne podejście dla plików exe
        if (exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
        }

        using var process = Process.Start(processInfo);
        return true;
    }
    catch (Exception ex)
    {
        _context.API.LogException("ExecuteCommand", $"Błąd wykonania: {commandCode}", ex);
        _context.API.ShowMsg("Błąd", $"Nie można uruchomić: {commandCode}\n{ex.Message}");
        return false;
    }
}
3. CommandsManager.cs - Domyślne komendy
Problem: Domyślne komendy mogą zawierać ścieżki ze spacjami.
Plik: CommandsManager.cs
Funkcja: GetDefaultCommands
csharpprivate List<CommandEntry> GetDefaultCommands()
{
    return new List<CommandEntry>
    {
        new CommandEntry("notepad", "notepad.exe", "Uruchom Notatnik"),
        new CommandEntry("calc", "calc.exe", "Uruchom Kalkulator"),
        new CommandEntry("cmd", "cmd.exe", "Uruchom Wiersz Poleceń"),
        new CommandEntry("explorer", "explorer.exe", "Uruchom Eksplorator plików"),
        new CommandEntry("paint", "mspaint.exe", "Uruchom Paint")
    };
}
4. SettingsControl.xaml.cs - Walidacja ścieżek
Problem: Brak walidacji ścieżek ze spacjami podczas dodawania komend.
Plik: SettingsControl.xaml.cs
Funkcja: TryCreateValidCommand
csharpprivate bool TryCreateValidCommand(out CommandEntry command, out string errorMessage)
{
    var key = EditKey?.Trim() ?? string.Empty;
    var code = EditCode?.Trim() ?? string.Empty;
    var info = EditInfo?.Trim() ?? string.Empty;

    // Automatyczne dodanie cudzysłowów dla ścieżek ze spacjami
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

    // Dodatkowa walidacja dla plików
    if (Path.IsPathRooted(command.Code.Trim('"')))
    {
        var filePath = command.Code.Trim('"');
        if (!File.Exists(filePath))
        {
            errorMessage = $"Plik nie istnieje: {filePath}";
            return false;
        }
    }

    return true;
}
5. Main.cs - Pomocnicze funkcje
Nowa funkcja: Dodanie funkcji pomocniczej do lepszego zarządzania ścieżkami.
Plik: Main.cs
csharpprivate static string EscapePathForExecution(string path)
{
    if (string.IsNullOrEmpty(path)) return path;

    // Usuń istniejące cudzysłowy i dodaj nowe jeśli ścieżka zawiera spacje
    path = path.Trim('"');
    return path.Contains(" ") ? $"\"{path}\"" : path;
}

private static bool IsExecutableFile(string path)
{
    if (string.IsNullOrEmpty(path)) return false;

    var extension = Path.GetExtension(path).ToLowerInvariant();
    return extension == ".exe" || extension == ".bat" || extension == ".cmd" || extension == ".lnk";
}
Podsumowanie zmian

Poprawione parsowanie ścieżek - Lepsze rozpoznawanie ścieżek ze spacjami
Zmienione wykonywanie procesów - Używanie UseShellExecute = true dla lepszej kompatybilności
Automatyczne dodawanie cudzysłowów - W interfejsie ustawień
Walidacja plików - Sprawdzanie czy plik istnieje przed dodaniem
Lepsze domyślne komendy - Używanie komend systemowych bez pełnych ścieżek

Te zmiany powinny rozwiązać problem z wykonywaniem komend zawierających ścieżki ze spacjami, szczególnie dla Cinema 4D i innych aplikacji z podobnymi ścieżkami.