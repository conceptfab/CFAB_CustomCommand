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
        [StringLength(50, ErrorMessage = "Klucz nie może być dłuższy niż 50 znaków")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        [Required(ErrorMessage = "Kod jest wymagany")]
        [StringLength(500, ErrorMessage = "Kod nie może być dłuższy niż 500 znaków")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("info")]
        [StringLength(200, ErrorMessage = "Opis nie może być dłuższy niż 200 znaków")]
        public string Info { get; set; } = string.Empty;

        // Konstruktor bezparametrowy potrzebny do deserializacji
        public CommandEntry() { }

        public CommandEntry(string key, string code, string info = "")
        {
            Key = key?.Trim() ?? string.Empty;
            Code = code?.Trim() ?? string.Empty;
            Info = info?.Trim() ?? string.Empty;
        }

        // Rozszerzona walidacja
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Code))
                return false;

            // Sprawdź długość
            if (Key.Length > 50 || Code.Length > 500 || Info.Length > 200)
                return false;

            // Sprawdź czy klucz nie zawiera niedozwolonych znaków
            var invalidChars = new char[] { ' ', '\t', '\n', '\r', '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            if (Key.IndexOfAny(invalidChars) >= 0)
                return false;

            return true;
        }

        // Nowa metoda do walidacji z szczegółowymi komunikatami
        public ValidationResult ValidateDetailed()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return new ValidationResult("Klucz jest wymagany");

            if (string.IsNullOrWhiteSpace(Code))
                return new ValidationResult("Kod jest wymagany");

            if (Key.Length > 50)
                return new ValidationResult("Klucz nie może być dłuższy niż 50 znaków");

            if (Code.Length > 500)
                return new ValidationResult("Kod nie może być dłuższy niż 500 znaków");

            if (Info.Length > 200)
                return new ValidationResult("Opis nie może być dłuższy niż 200 znaków");

            var invalidChars = new char[] { ' ', '\t', '\n', '\r', '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            if (Key.IndexOfAny(invalidChars) >= 0)
                return new ValidationResult("Klucz zawiera niedozwolone znaki");

            return ValidationResult.Success!;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }
        public static ValidationResult Success { get; } = new ValidationResult(true, string.Empty);

        public ValidationResult(string errorMessage) : this(false, errorMessage) { }

        private ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}
