using System.ComponentModel.DataAnnotations; // Toto je důležité pro [Key]

namespace Stin_Semestral.Models
{
    public class Currency
    {
        [Key] 
        public string name { get; set; }

        public decimal price { get; set; }
    }
}