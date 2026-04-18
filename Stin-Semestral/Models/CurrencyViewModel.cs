namespace Stin_Semestral.Models
{
    public class CurrencyViewModel
    {
        public string Name { get; set; }          // Např. "EUR"
        public decimal CurrentRate { get; set; }  // Kurz přepočtený vůči ZVOLENÉ měně (ne USD!)
        public decimal Average30Days { get; set; } // Vypočítaný aritmetický průměr
        public DateTime Date { get; set; }        // Datum poslední aktualizace

        // Pomocná vlastnost pro graf (uložíme si pár posledních hodnot jako text)
        public string HistoryJson { get; set; } = string.Empty;
    }
}