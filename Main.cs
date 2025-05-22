using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls; // Dla ISettingProvider

namespace Flow.Plugin.CommandLauncher
{
    public class Main : IPlugin, ISettingProvider, IReloadable // IReloadable pozwala na przeładowanie danych bez restartu Flow
    {
        private PluginInitContext _context = null!;
        private CommandsManager _commandsManager = null!;
        private string _iconPath = string.Empty; // Ścieżka do ikony wtyczki

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _commandsManager = new CommandsManager(context);
            _iconPath = context.CurrentPluginMetadata.IcoPath; // Użyj ikony z plugin.json
                                                               // Jeśli ikona nie istnieje lub nie jest ustawiona, możesz użyć domyślnej
            if (string.IsNullOrEmpty(_iconPath) || !File.Exists(Path.Combine(context.CurrentPluginMetadata.PluginDirectory, _iconPath)))
            {
                _iconPath = "Images\\app.png"; // Domyślna ikona Flow Launchera
            }
        }

        public List<Result> Query(Query query)
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
            string iconPath = _iconPath; // Domyślnie używamy ikony wtyczki

            // Próbujemy pobrać ikonę z pliku wykonywalnego
            try
            {
                string cleanPath = cmd.Code.Trim('"');
                if (File.Exists(cleanPath))
                {
                    iconPath = cleanPath; // Flow Launcher automatycznie pobierze ikonę z pliku
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

        private bool ExecuteCommand(string commandCode)
        {
            try
            {
                commandCode = Environment.ExpandEnvironmentVariables(commandCode.Trim());
                _context.API.LogDebug("ExecuteCommand", $"Przetwarzanie: {commandCode}");

                var (exePath, arguments) = ParseCommand(commandCode);

                // Sprawdź czy plik istnieje przed dodaniem cudzysłowów
                string cleanPath = exePath.Trim('"');
                if (ShouldValidateFile(cleanPath) && !File.Exists(cleanPath))
                {
                    throw new FileNotFoundException($"Nie można znaleźć pliku: {cleanPath}");
                }

                // Lepsze formatowanie dla cmd.exe
                var processInfo = new ProcessStartInfo
                {
                    FileName = cleanPath,
                    Arguments = arguments,
                    UseShellExecute = true,  // Zmiana na true dla lepszej kompatybilności
                    CreateNoWindow = false
                };

                // Alternatywne podejście dla plików exe
                if (cleanPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
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

        private static (string exePath, string arguments) ParseCommand(string command)
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

            // Dla ścieżek bezwzględnych (z pełną ścieżką)
            if (Path.IsPathRooted(command))
            {
                // Jeśli ścieżka kończy się na .exe, .bat lub .cmd, traktuj całość jako ścieżkę
                if (command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                    command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                {
                    return (command, "");
                }

                // W przeciwnym razie szukaj ostatniej spacji przed rozszerzeniem
                int lastSpace = command.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    string potentialPath = command.Substring(0, lastSpace);
                    if (File.Exists(potentialPath))
                    {
                        return (potentialPath, command.Substring(lastSpace + 1));
                    }
                }
            }

            // Standardowe przetwarzanie
            int spaceIndex = command.IndexOf(' ');
            return spaceIndex > 0
                ? (command.Substring(0, spaceIndex), command.Substring(spaceIndex + 1))
                : (command, "");
        }

        private static string EscapePathForExecution(string path)
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

        // Implementacja ISettingProvider
        public Control CreateSettingPanel()
        {
            // Przekazujemy referencję do CommandsManager, aby UserControl mógł operować na danych
            return new SettingsControl(_context, _commandsManager);
        }

        // Implementacja IReloadable (opcjonalnie, ale dobre praktyka)
        public void ReloadData()
        {
            _commandsManager?.LoadCommands(); // Przeładuj komendy z pliku
        }
    }
}
