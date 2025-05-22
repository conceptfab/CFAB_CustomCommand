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
            return new Result
            {
                Title = $"{cmd.Info} ({cmd.Key})",
                SubTitle = $"Uruchom: {cmd.Code}",
                IcoPath = _iconPath,
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
