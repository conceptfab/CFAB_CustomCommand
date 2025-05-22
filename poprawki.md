Zmiany w pliku Main.cs
W funkcji ExecuteCommand - naprawienie obsługi ścieżek z spacjami
csharpprivate bool ExecuteCommand(string commandCode)
{
    try
    {
        if (commandCode.Contains("%"))
        {
            // Obsługa zmiennych środowiskowych
            commandCode = Environment.ExpandEnvironmentVariables(commandCode);
        }

        _context.API.LogDebug("ExecuteCommand", $"Przetwarzanie komendy: {commandCode}");

        // Rozdziel ścieżkę i argumenty zgodnie z zasadami Windows
        string exePath;
        string arguments = string.Empty;
        string trimmed = commandCode.Trim();

        if (trimmed.StartsWith("\""))
        {
            int endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 0)
            {
                exePath = trimmed.Substring(1, endQuote - 1);
                if (trimmed.Length > endQuote + 1)
                {
                    arguments = trimmed.Substring(endQuote + 1).TrimStart();
                }
            }
            else
            {
                exePath = trimmed; // nieprawidłowa składnia, ale próbujemy całość
            }
        }
        else
        {
            int firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                exePath = trimmed.Substring(0, firstSpace);
                arguments = trimmed.Substring(firstSpace + 1);
            }
            else
            {
                exePath = trimmed;
            }
        }

        // Automatycznie otaczaj ścieżkę cudzysłowami, jeśli zawiera spacje i nie jest już w cudzysłowie
        string exePathForCmd = exePath;
        if (exePathForCmd.Contains(" ") && !(exePathForCmd.StartsWith("\"") && exePathForCmd.EndsWith("\"")))
        {
            exePathForCmd = $"\"{exePathForCmd}\"";
        }
        string cmdToRun = string.IsNullOrEmpty(arguments) ? exePathForCmd : $"{exePathForCmd} {arguments}";

        // Sprawdź czy to program systemowy
        bool isSystemCommand = exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                             !Path.IsPathRooted(exePath) &&
                             !exePath.Contains("\\") &&
                             !exePath.Contains("/");

        if (!isSystemCommand && Path.IsPathRooted(exePath))
        {
            if (!File.Exists(exePath))
            {
                _context.API.LogException("ExecuteCommand", $"Plik nie istnieje: {exePath}", new FileNotFoundException($"Nie można znaleźć pliku: {exePath}"));
                throw new FileNotFoundException($"Nie można znaleźć pliku: {exePath}");
            }
            _context.API.LogDebug("ExecuteCommand", $"Plik istnieje: {exePath}");
        }

        // Użyj cmd.exe do uruchomienia komendy
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" {cmdToRun}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _context.API.LogDebug("ExecuteCommand", $"Uruchamianie procesu: cmd.exe /c start \"\" {cmdToRun}");
        Process.Start(startInfo);
        return true;
    }
    catch (Exception ex)
    {
        _context.API.LogException("ExecuteCommand", $"Nie można uruchomić: {commandCode}", ex);
        _context.API.ShowMsg("Błąd wykonania komendy", $"Nie można uruchomić: {commandCode}\nBłąd: {ex.Message}");
        return false;
    }
}
Zmiany w pliku SettingsControl.xaml
Dodanie instrukcji o otaczaniu ścieżek z spacjami cudzysłowami
xml<TextBlock Margin="0,15,0,5" TextWrapping="Wrap">
    <Run Text="Instrukcja:"/>
    <LineBreak/>
    <Run Text="- Klucz: unikalny identyfikator, który wpiszesz po głównym skrócie (np. jeśli główny to 'aa', a klucz to '1', wpiszesz 'aa1')."/>
    <LineBreak/>
    <Run Text="- Komenda/Ścieżka: pełna ścieżka do pliku .exe, skrótu .lnk lub komenda systemowa. Ścieżki z spacjami automatycznie otaczane cudzysłowami."/>
    <LineBreak/>
    <Run Text="- Przykłady: C:\Program Files\Notepad++\notepad++.exe, calc, cmd /c echo Hello"/>
    <LineBreak/>
    <Run Text="- Opis: krótki opis komendy wyświetlany w wynikach."/>
</TextBlock>
