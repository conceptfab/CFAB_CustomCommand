using System.Collections.ObjectModel;
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
