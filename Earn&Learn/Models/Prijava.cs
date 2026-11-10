using Earn_Learn.Enums;

namespace Earn_Learn.Models
{
    public class Prijava
    {
        public int Id { get; set; }
        public string Naslov { get; set; } = string.Empty;
        public string Opis { get; set; } = string.Empty;
        public string? IdPrijavitelja { get; set; }
        public DateTime DatumPrijave { get; set; } = DateTime.UtcNow;
        public StatusPrijave Status { get; set; } = StatusPrijave.Prijavljeno;

        // Ako je prijava na tutora, ovdje ide tutor ID
        public string? PrijavljeniTutorId { get; set; }

        // Ako je prijava na recenziju, ovdje ide recenzija ID
        public int? RecenzijaId { get; set; }
    }
}