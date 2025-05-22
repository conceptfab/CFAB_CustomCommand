using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using System.Linq;

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
