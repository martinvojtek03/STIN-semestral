using System;
using System.ComponentModel.DataAnnotations;

namespace Stin_Semestral.Models
{
    public class ExchangeLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Typ (např. "Error" nebo "Info")
        public string Level { get; set; } = "Info";

        // Popis chyby nebo události
        public string Message { get; set; } = string.Empty;
    }
}