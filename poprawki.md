Po przeanalizowaniu kodu wtyczki CommandLauncher, zidentyfikowałem kilka obszarów, w których można wprowadzić ulepszenia. Poniżej przedstawiam proponowane zmiany, uporządkowane według plików i funkcji.
Zmiany w pliku Main.cs
1. Poprawa obsługi błędów w metodzie ExecuteCommand
csharpprivate bool ExecuteCommand(string commandCode)
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
Zmiany w pliku CommandsManager.cs
1. Dodanie lepszej obsługi błędów i sprawdzania istniejącego klucza
csharppublic void AddCommand(CommandEntry command)
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
2. Usprawnienie metody LoadCommands - dokładniejsze logowanie błędów
csharppublic void LoadCommands()
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
3. Lepsze zabezpieczenie metody SaveCommands
csharppublic void SaveCommands()
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
Zmiany w pliku SettingsControl.xaml.cs
1. Walidacja danych wejściowych przed zapisem
csharpprivate void AddButton_Click(object sender, RoutedEventArgs e)
{
    if (!ValidateInput())
    {
        return;
    }

    var newCommand = new CommandEntry
    {
        Key = EditKey.Trim(),
        Code = EditCode.Trim(),
        Info = EditInfo.Trim()
    };

    // Sprawdź czy klucz już istnieje
    if (_commands.Any(c => c.Key.Equals(newCommand.Key, StringComparison.OrdinalIgnoreCase)))
    {
        MessageBox.Show($"Komenda o kluczu '{newCommand.Key}' już istnieje!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    _commandsManager.AddCommand(newCommand);
    Commands.Add(newCommand);
    ClearFields();
}

private bool ValidateInput()
{
    if (string.IsNullOrWhiteSpace(EditKey))
    {
        MessageBox.Show("Klucz jest wymagany!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        KeyTextBox.Focus();
        return false;
    }

    if (string.IsNullOrWhiteSpace(EditCode))
    {
        MessageBox.Show("Komenda/Ścieżka jest wymagana!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        CodeTextBox.Focus();
        return false;
    }

    return true;
}
2. Potwierdzenie usunięcia komendy
csharpprivate void RemoveButton_Click(object sender, RoutedEventArgs e)
{
    if (SelectedCommand != null)
    {
        var result = MessageBox.Show(
            $"Czy na pewno chcesz usunąć komendę '{SelectedCommand.Key}' ({SelectedCommand.Info})?",
            "Potwierdzenie usunięcia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _commandsManager.RemoveCommand(SelectedCommand);
            Commands.Remove(SelectedCommand);
            ClearFields();
        }
    }
}
Zmiany w pliku CommandEntry.cs
1. Dodanie walidacji dla wartości właściwości
csharpusing System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Flow.Plugin.CommandLauncher
{
    public class CommandEntry
    {
        [JsonPropertyName("key")]
        [Required(ErrorMessage = "Klucz jest wymagany")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        [Required(ErrorMessage = "Kod jest wymagany")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("info")]
        public string Info { get; set; } = string.Empty;

        // Konstruktor bezparametrowy potrzebny do deserializacji
        public CommandEntry() { }

        public CommandEntry(string key, string code, string info)
        {
            Key = key?.Trim() ?? string.Empty;
            Code = code?.Trim() ?? string.Empty;
            Info = info?.Trim() ?? string.Empty;
        }

        // Metoda walidacji
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Key))
            {
                errorMessage = "Klucz nie może być pusty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Code))
            {
                errorMessage = "Kod nie może być pusty";
                return false;
            }

            return true;
        }
    }
}
Nowy plik SettingsViewModel.cs
Aby lepiej zaimplementować wzorzec MVVM, proponuję utworzenie dodatkowego pliku z modelem widoku dla ustawień:
csharpusing System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Flow.Launcher.Plugin;

namespace Flow.Plugin.CommandLauncher
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly PluginInitContext _context;
        private readonly CommandsManager _commandsManager;
        private CommandEntry? _selectedCommand;
        private string _editKey = string.Empty;
        private string _editCode = string.Empty;
        private string _editInfo = string.Empty;
        private bool _isItemSelected;

        public ObservableCollection<CommandEntry> Commands { get; }

        public CommandEntry? SelectedCommand
        {
            get => _selectedCommand;
            set
            {
                _selectedCommand = value;
                IsItemSelected = value != null;
                if (value != null)
                {
                    EditKey = value.Key;
                    EditCode = value.Code;
                    EditInfo = value.Info;
                }
                OnPropertyChanged();
            }
        }

        public string EditKey
        {
            get => _editKey;
            set
            {
                _editKey = value;
                OnPropertyChanged();
            }
        }

        public string EditCode
        {
            get => _editCode;
            set
            {
                _editCode = value;
                OnPropertyChanged();
            }
        }

        public string EditInfo
        {
            get => _editInfo;
            set
            {
                _editInfo = value;
                OnPropertyChanged();
            }
        }

        public bool IsItemSelected
        {
            get => _isItemSelected;
            set
            {
                _isItemSelected = value;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel(PluginInitContext context, CommandsManager commandsManager)
        {
            _context = context;
            _commandsManager = commandsManager;
            Commands = new ObservableCollection<CommandEntry>(_commandsManager.Commands);
        }

        public void AddCommand()
        {
            if (!ValidateInput(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newCommand = new CommandEntry(EditKey, EditCode, EditInfo);

            if (Commands.Any(c => c.Key.Equals(newCommand.Key, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Komenda o kluczu '{newCommand.Key}' już istnieje!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _commandsManager.AddCommand(newCommand);
            Commands.Add(newCommand);
            ClearFields();
        }

        public void UpdateCommand()
        {
            if (SelectedCommand == null) return;

            if (!ValidateInput(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updatedCommand = new CommandEntry(EditKey, EditCode, EditInfo);

            // Sprawdź czy nowy klucz już istnieje (jeśli został zmieniony)
            if (!SelectedCommand.Key.Equals(updatedCommand.Key, StringComparison.OrdinalIgnoreCase) &&
                Commands.Any(c => c.Key.Equals(updatedCommand.Key, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Komenda o kluczu '{updatedCommand.Key}' już istnieje!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _commandsManager.UpdateCommand(SelectedCommand, updatedCommand);

            var index = Commands.IndexOf(SelectedCommand);
            if (index != -1)
            {
                Commands[index] = updatedCommand;
            }

            ClearFields();
        }

        public void RemoveCommand()
        {
            if (SelectedCommand == null) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć komendę '{SelectedCommand.Key}' ({SelectedCommand.Info})?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _commandsManager.RemoveCommand(SelectedCommand);
                Commands.Remove(SelectedCommand);
                ClearFields();
            }
        }

        public void ClearFields()
        {
            EditKey = string.Empty;
            EditCode = string.Empty;
            EditInfo = string.Empty;
            SelectedCommand = null;
        }

        private bool ValidateInput(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(EditKey))
            {
                errorMessage = "Klucz jest wymagany!";
                return false;
            }

            if (string.IsNullOrWhiteSpace(EditCode))
            {
                errorMessage = "Komenda/Ścieżka jest wymagana!";
                return false;
            }

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}