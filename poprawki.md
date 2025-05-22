Analiza projektu CFAB Command Launcher - Optymalizacje i Poprawki
Po przeanalizowaniu projektu Flow Launcher plugin, znalazłem kilka obszarów wymagających optymalizacji i poprawek:
1. CommandsManager.cs - Optymalizacje zarządzania plikami
Funkcja LoadCommands() - Uproszczenie obsługi błędów
csharppublic void LoadCommands()
{
    try
    {
        if (!File.Exists(_commandsFilePath))
        {
            Commands = GetDefaultCommands();
            SaveCommands();
            return;
        }

        string json = File.ReadAllText(_commandsFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            LoadDefaultCommandsWithLog("Plik commands.json jest pusty");
            return;
        }

        Commands = JsonSerializer.Deserialize<List<CommandEntry>>(json) ?? new List<CommandEntry>();

        if (Commands.Count == 0)
        {
            LoadDefaultCommandsWithLog("Brak komend w pliku");
        }
    }
    catch (Exception ex)
    {
        _context.API.LogException(nameof(CommandsManager), $"Błąd wczytywania commands.json: {ex.Message}", ex);
        LoadDefaultCommandsWithLog($"Błąd deserializacji: {ex.Message}");
    }
}

private void LoadDefaultCommandsWithLog(string reason)
{
    _context.API.LogWarn(nameof(CommandsManager), $"{reason}, ładowanie domyślnych komend");
    Commands = GetDefaultCommands();
    SaveCommands();
}
Funkcja SaveCommands() - Lepsze zabezpieczenie przed utratą danych
csharppublic void SaveCommands()
{
    const int maxRetries = 3;
    for (int retry = 0; retry < maxRetries; retry++)
    {
        try
        {
            EnsureDataDirectoryExists();

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Commands, options);

            string tempFilePath = $"{_commandsFilePath}.tmp";
            File.WriteAllText(tempFilePath, json);

            // Atomowe zastąpienie pliku
            if (File.Exists(_commandsFilePath))
                File.Replace(tempFilePath, _commandsFilePath, null);
            else
                File.Move(tempFilePath, _commandsFilePath);

            return; // Sukces
        }
        catch (Exception ex) when (retry < maxRetries - 1)
        {
            _context.API.LogWarn(nameof(CommandsManager), $"Próba zapisu {retry + 1} nieudana: {ex.Message}");
            Thread.Sleep(100); // Krótkie opóźnienie przed ponowną próbą
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(CommandsManager), $"Błąd zapisu po {maxRetries} próbach: {ex.Message}", ex);
        }
    }
}
2. Main.cs - Optymalizacje wyszukiwania i wykonywania
Funkcja Query() - Poprawa wydajności wyszukiwania
csharppublic List<Result> Query(Query query)
{
    var results = new List<Result>();
    string searchQuery = query.Search.Trim();

    if (string.IsNullOrEmpty(searchQuery))
    {
        // Użyj LINQ dla lepszej czytelności
        return _commandsManager.Commands.Select(cmd => CreateResult(cmd)).ToList();
    }

    // Najpierw dokładne dopasowanie
    var exactMatch = _commandsManager.Commands.FirstOrDefault(cmd =>
        cmd.Key.Equals(searchQuery, StringComparison.OrdinalIgnoreCase));

    if (exactMatch != null)
    {
        results.Add(CreateResult(exactMatch));
    }

    // Następnie częściowe dopasowania (tylko jeśli nie ma dokładnego)
    if (results.Count == 0)
    {
        var partialMatches = _commandsManager.Commands
            .Where(cmd => cmd.Key.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                         cmd.Info.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .Select(CreateResult);

        results.AddRange(partialMatches);
    }

    return results;
}

private Result CreateResult(CommandEntry cmd)
{
    return new Result
    {
        Title = $"{cmd.Info} ({cmd.Key})",
        SubTitle = $"Uruchom: {cmd.Code}",
        IcoPath = _iconPath,
        Action = _ => ExecuteCommand(cmd.Code)
    };
}
Funkcja ExecuteCommand() - Uproszczenie parsowania ścieżek
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

        var processInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" {FormatCommandForExecution(exePath, arguments)}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

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

private static (string exePath, string arguments) ParseCommand(string command)
{
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

    int spaceIndex = command.IndexOf(' ');
    return spaceIndex > 0
        ? (command.Substring(0, spaceIndex), command.Substring(spaceIndex + 1))
        : (command, "");
}

private static string FormatCommandForExecution(string exePath, string arguments)
{
    string formattedPath = exePath.Contains(" ") && !exePath.StartsWith("\"")
        ? $"\"{exePath}\""
        : exePath;

    return string.IsNullOrEmpty(arguments) ? formattedPath : $"{formattedPath} {arguments}";
}

private static bool ShouldValidateFile(string exePath)
{
    return Path.IsPathRooted(exePath) &&
           !IsSystemCommand(exePath);
}

private static bool IsSystemCommand(string exePath)
{
    return exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
           !Path.IsPathRooted(exePath) &&
           !exePath.Contains('\\') &&
           !exePath.Contains('/');
}
3. Usunięcie nadmiarowego kodu
Usunięcie SettingsViewModel.cs
Plik SettingsViewModel.cs jest duplikacją logiki z SettingsControl.xaml.cs. Można go całkowicie usunąć, ponieważ SettingsControl już implementuje wzorzec MVVM.
4. CommandEntry.cs - Uproszczenie walidacji
Optymalizacja klasy CommandEntry
csharppublic class CommandEntry
{
    [JsonPropertyName("key")]
    [Required(ErrorMessage = "Klucz jest wymagany")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    [Required(ErrorMessage = "Kod jest wymagany")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("info")]
    public string Info { get; set; } = string.Empty;

    public CommandEntry() { }

    public CommandEntry(string key, string code, string info = "")
    {
        Key = key?.Trim() ?? string.Empty;
        Code = code?.Trim() ?? string.Empty;
        Info = info?.Trim() ?? string.Empty;
    }

    // Uproszczona walidacja używająca wbudowanych atrybutów
    public bool IsValid() => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Code);
}
5. SettingsControl.xaml.cs - Redukcja duplikacji kodu
Optymalizacja metod walidacji i zarządzania
csharpprivate void AddButton_Click(object sender, RoutedEventArgs e)
{
    if (!TryCreateValidCommand(out var newCommand, out var errorMessage))
    {
        ShowError(errorMessage);
        return;
    }

    if (CommandExists(newCommand.Key))
    {
        ShowError($"Komenda o kluczu '{newCommand.Key}' już istnieje!");
        return;
    }

    _commandsManager.AddCommand(newCommand);
    Commands.Add(newCommand);
    ClearFields();
}

private bool TryCreateValidCommand(out CommandEntry command, out string errorMessage)
{
    command = new CommandEntry(EditKey, EditCode, EditInfo);
    errorMessage = "";

    if (!command.IsValid())
    {
        errorMessage = string.IsNullOrWhiteSpace(command.Key) ?
            "Klucz jest wymagany!" : "Komenda/Ścieżka jest wymagana!";
        return false;
    }

    return true;
}

private bool CommandExists(string key) =>
    Commands.Any(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

private void ShowError(string message) =>
    MessageBox.Show(message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
Podsumowanie głównych korzyści:

Usunięcie duplikacji - Eliminacja SettingsViewModel.cs
Lepsza obsługa błędów - Retry mechanizm dla zapisu plików
Optymalizacja wyszukiwania - Efektywniejsze filtrowanie wyników
Uproszczenie kodu - Wydzielenie pomocniczych metod
Poprawa czytelności - Bardziej zwięzły i zrozumiały kod
Atomowe operacje - Bezpieczniejszy zapis plików
Lepsze używanie LINQ - Bardziej funkcyjny styl programowania