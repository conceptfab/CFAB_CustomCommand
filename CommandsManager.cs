using System.Collections.Generic;
using System.IO;
using System.Text.Json; // Używamy System.Text.Json
using System.Windows; // Dla MessageBox, jeśli potrzebne
using Flow.Launcher.Plugin;
using System.Threading;
using System.Linq;

namespace Flow.Plugin.CommandLauncher
{
    public class CommandsManager
    {
        private readonly string _commandsFilePath;
        private readonly PluginInitContext _context;

        public List<CommandEntry> Commands { get; private set; } = new();

        public CommandsManager(PluginInitContext context)
        {
            _context = context;
            // Przechowuj commands.json w folderze danych wtyczki
            _commandsFilePath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "Data", "commands.json");
            EnsureDataDirectoryExists();
            LoadCommands();
        }

        private void EnsureDataDirectoryExists()
        {
            var dir = Path.GetDirectoryName(_commandsFilePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public void LoadCommands()
        {
            try
            {
                if (!File.Exists(_commandsFilePath))
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
        }

        private void LoadDefaultCommandsWithLog(string reason)
        {
            _context.API.LogWarn(nameof(CommandsManager), $"{reason}, ładowanie domyślnych komend");
            Commands = GetDefaultCommands();
            SaveCommands();
        }

        public void SaveCommands()
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

        private List<CommandEntry> GetDefaultCommands()
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

        // Metody do zarządzania komendami (dodawanie, usuwanie, edycja)
        // Te metody będą wywoływane z UI Ustawień
        public void AddCommand(CommandEntry command)
        {
            if (string.IsNullOrWhiteSpace(command.Key) || string.IsNullOrWhiteSpace(command.Code))
            {
                _context.API.LogWarn("AddCommand", "Próba dodania nieprawidłowej komendy (pusty klucz lub kod)");
                return;
            }

            if (Commands.Any(c => c.Key.Equals(command.Key, StringComparison.OrdinalIgnoreCase)))
            {
                _context.API.LogWarn("AddCommand", $"Komenda o kluczu '{command.Key}' już istnieje");
                return;
            }

            Commands.Add(command);
            SaveCommands();
        }

        public void UpdateCommand(CommandEntry oldCommand, CommandEntry newCommand)
        {
            var index = Commands.FindIndex(c => c.Key == oldCommand.Key && c.Code == oldCommand.Code); // Proste porównanie
            if (index != -1)
            {
                Commands[index] = newCommand;
                SaveCommands();
            }
        }

        public void RemoveCommand(CommandEntry command)
        {
            var itemToRemove = Commands.FirstOrDefault(c => c.Key == command.Key && c.Code == command.Code);
            if (itemToRemove != null)
            {
                Commands.Remove(itemToRemove);
                SaveCommands();
            }
        }
    }
}
