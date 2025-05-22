using System.Collections.Generic;
using System.IO;
using System.Text.Json; // Używamy System.Text.Json
using System.Windows; // Dla MessageBox, jeśli potrzebne
using Flow.Launcher.Plugin;

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
            if (File.Exists(_commandsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_commandsFilePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _context.API.LogWarn(nameof(CommandsManager), "Plik commands.json jest pusty, ładowanie domyślnych komend");
                        Commands = GetDefaultCommands();
                        SaveCommands();
                        return;
                    }

                    var deserializedCommands = JsonSerializer.Deserialize<List<CommandEntry>>(json);
                    Commands = deserializedCommands ?? new List<CommandEntry>();

                    if (Commands.Count == 0)
                    {
                        _context.API.LogWarn(nameof(CommandsManager), "Brak komend w pliku, ładowanie domyślnych");
                        Commands = GetDefaultCommands();
                        SaveCommands();
                    }
                }
                catch (JsonException ex)
                {
                    _context.API.LogException(nameof(CommandsManager), $"Błąd deserializacji pliku commands.json: {ex.Message}", ex);
                    Commands = GetDefaultCommands();
                    SaveCommands();
                }
                catch (IOException ex)
                {
                    _context.API.LogException(nameof(CommandsManager), $"Błąd IO podczas wczytywania commands.json: {ex.Message}", ex);
                    Commands = GetDefaultCommands();
                }
                catch (Exception ex)
                {
                    _context.API.LogException(nameof(CommandsManager), $"Nieoczekiwany błąd podczas wczytywania commands.json: {ex.Message}", ex);
                    Commands = GetDefaultCommands();
                }
            }
            else
            {
                Commands = GetDefaultCommands();
                SaveCommands();
            }
        }

        public void SaveCommands()
        {
            try
            {
                EnsureDataDirectoryExists();

                string json = JsonSerializer.Serialize(Commands, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Najpierw zapisz do pliku tymczasowego, a następnie zastąp oryginalny
                string tempFilePath = _commandsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json);

                if (File.Exists(_commandsFilePath))
                {
                    File.Delete(_commandsFilePath);
                }

                File.Move(tempFilePath, _commandsFilePath);
            }
            catch (Exception ex)
            {
                _context.API.LogException(nameof(CommandsManager), $"Błąd zapisu pliku commands.json: {ex.Message}", ex);
            }
        }

        private List<CommandEntry> GetDefaultCommands()
        {
            // Przykładowe domyślne komendy
            return new List<CommandEntry>
            {
                new CommandEntry("1", "notepad.exe", "Uruchom Notatnik"),
                new CommandEntry("calc", "calc.exe", "Uruchom Kalkulator"),
                new CommandEntry("cmd", "cmd.exe", "Uruchom Wiersz Poleceń")
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
