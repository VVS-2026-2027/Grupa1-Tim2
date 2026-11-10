using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Models
{
    public class Recenzija
    {
        [Key]
        public int id { get; set; }
        public string idStudenta { get; set; } = string.Empty;
        public string idTutora { get; set; } = string.Empty;
        public int ocjena { get; set; }
        public string komentar { get; set; } = string.Empty;
        public DateTime datumRecenzije { get; set; } = DateTime.UtcNow;
    }
}