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
            string searchQuery = query.Search; // To co użytkownik wpisał po "aa"

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                // Pokaż wszystkie dostępne komendy, jeśli nic nie wpisano po "aa"
                foreach (var cmd in _commandsManager.Commands)
                {
                    results.Add(new Result
                    {
                        Title = $"{cmd.Info} ({cmd.Key})",
                        SubTitle = $"Uruchom: {cmd.Code}",
                        IcoPath = _iconPath, // Możesz tu ustawić specyficzną ikonę dla każdej komendy
                        Action = c => ExecuteCommand(cmd.Code)
                    });
                }
            }
            else
            {
                // Filtruj komendy na podstawie wpisanego podklucza
                var matchedCommand = _commandsManager.Commands.FirstOrDefault(cmd =>
                    cmd.Key.Equals(searchQuery, StringComparison.OrdinalIgnoreCase));

                if (matchedCommand != null)
                {
                    results.Add(new Result
                    {
                        Title = matchedCommand.Info,
                        SubTitle = $"Uruchom: {matchedCommand.Code} (po wpisaniu '{_context.CurrentPluginMetadata.ActionKeyword}{matchedCommand.Key}')",
                        IcoPath = _iconPath,
                        Action = c => ExecuteCommand(matchedCommand.Code)
                    });
                }
                else
                {
                    // Opcjonalnie: pokaż pasujące częściowo lub zasugeruj dodanie nowej
                    var partialMatches = _commandsManager.Commands.Where(cmd =>
                        cmd.Key.StartsWith(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        cmd.Info.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    foreach (var cmd in partialMatches)
                    {
                         results.Add(new Result
                        {
                            Title = $"{cmd.Info} ({cmd.Key})",
                            SubTitle = $"Uruchom: {cmd.Code}",
                            IcoPath = _iconPath,
                            Action = c => ExecuteCommand(cmd.Code)
                        });
                    }
                }
            }

            return results;
        }

        private bool ExecuteCommand(string commandCode)
        {
            try
            {
                // Rozdzielenie komendy od argumentów
                string fileName;
                string arguments = string.Empty;

                if (commandCode.Contains("%"))
                {
                    // Obsługa zmiennych środowiskowych
                    commandCode = Environment.ExpandEnvironmentVariables(commandCode);
                }

                int firstSpace = commandCode.IndexOf(' ');
                if (firstSpace > 0)
                {
                    fileName = commandCode.Substring(0, firstSpace);
                    arguments = commandCode.Substring(firstSpace + 1);
                }
                else
                {
                    fileName = commandCode;
                }

                var startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = true
                };

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
