using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using System.Linq;
using System.IO;

namespace Flow.Plugin.CommandLauncher
{
    public partial class SettingsControl : UserControl, INotifyPropertyChanged
    {
        private readonly PluginInitContext _context;
        private readonly CommandsManager _commandsManager;
        private ObservableCollection<CommandEntry> _commands = new();
        private CommandEntry? _selectedCommand;
        private string _editKey = string.Empty;
        private string _editCode = string.Empty;
        private string _editInfo = string.Empty;
        private bool _isItemSelected;

        public ObservableCollection<CommandEntry> Commands
        {
            get => _commands;
            set
            {
                _commands = value;
                OnPropertyChanged();
            }
        }

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

        public SettingsControl(PluginInitContext context, CommandsManager commandsManager)
        {
            InitializeComponent();
            _context = context;
            _commandsManager = commandsManager;
            Commands = new ObservableCollection<CommandEntry>(_commandsManager.Commands);
            DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
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
            _commandsManager.LoadCommands();
        }

        private bool TryCreateValidCommand(out CommandEntry command, out string errorMessage)
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
        }

        private bool CommandExists(string key) =>
            Commands.Any(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        private void ShowError(string message) =>
            MessageBox.Show(message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void SaveEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand == null) return;

            var updatedCommand = new CommandEntry
            {
                Key = EditKey,
                Code = EditCode,
                Info = EditInfo
            };

            _commandsManager.UpdateCommand(SelectedCommand, updatedCommand);
            var index = Commands.IndexOf(SelectedCommand);
            if (index != -1)
            {
                Commands[index] = updatedCommand;
            }
            ClearFields();
            _commandsManager.LoadCommands();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
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
                    _commandsManager.LoadCommands();
                }
            }
        }

        private void ClearFieldsButton_Click(object sender, RoutedEventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            EditKey = string.Empty;
            EditCode = string.Empty;
            EditInfo = string.Empty;
            SelectedCommand = null;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Pliki wykonywalne (*.exe)|*.exe|Skróty (*.lnk)|*.lnk|Wszystkie pliki (*.*)|*.*",
                Title = "Wybierz aplikację"
            };

            if (dialog.ShowDialog() == true)
            {
                EditCode = dialog.FileName;
            }
        }
    }
}
