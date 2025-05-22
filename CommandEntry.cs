using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization; // Potrzebne dla atrybutów, jeśli używasz System.Text.Json

namespace Flow.Plugin.CommandLauncher
{
    public class CommandEntry
    {
        // Użyj JsonPropertyName jeśli nazwy w JSON mają być inne niż w C#
        // lub jeśli używasz System.Text.Json. Dla Newtonsoft.Json nie jest to konieczne
        // jeśli nazwy są takie same (case-insensitive domyślnie).
        // Dla spójności i jasności, dodajmy je.

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
