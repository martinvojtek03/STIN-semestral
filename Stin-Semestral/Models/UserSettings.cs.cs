using System.ComponentModel.DataAnnotations;

namespace Stin_Semestral.Models
{
    public class UserSettings
    {
        [Key]
        public int Id { get; set; }

        // Výchozí měna, vůči které se počítá (např. EUR)
        [Required]
        public string BaseCurrency { get; set; } = "EUR";

        // Seznam měn, které chce uživatel sledovat (např. "CZK,USD")
        [Required]
        public string SelectedCurrencies { get; set; } = "CZK,USD";
    }
}