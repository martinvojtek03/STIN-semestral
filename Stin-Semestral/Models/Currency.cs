using System.ComponentModel.DataAnnotations;

namespace Stin_Semestral.Models
{
    public class Currency
    {
        public int Id { get; set; } // Automatické ID
        [Required]
        public string Name { get; set; } // Např. "EUR"
        public decimal Rate { get; set; }    // Kurz vůči USD
        public DateTime Date { get; set; }   // Ke kterému dni kurz patří
    }
}