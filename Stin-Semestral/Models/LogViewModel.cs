namespace Stin_Semestral.Models
{
    public class LogViewModel
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } // INFO, ERROR, WARNING
        public string Message { get; set; }
    }
}