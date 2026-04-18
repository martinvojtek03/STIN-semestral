using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 
namespace Stin_Semestral.Models
{
    [Table("Settings")] 
    public class UserSettings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string BaseCurrency { get; set; } = "USD";

        [Required]
        public string SelectedCurrencies { get; set; } = "CZK,EUR";
    }
}