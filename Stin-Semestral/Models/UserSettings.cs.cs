using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <--- Toto přidej

namespace Stin_Semestral.Models
{
    [Table("Settings")] // <--- Mapování na název v DB
    public class UserSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string BaseCurrency { get; set; } = "EUR";

        [Required]
        public string SelectedCurrencies { get; set; } = "CZK,USD";
    }
}