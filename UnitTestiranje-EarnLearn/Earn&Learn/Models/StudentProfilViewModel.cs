using Earn_Learn.Models;

namespace Earn_Learn.Models
{
    public class StudentProfilViewModel
    {
        public Korisnik Student { get; set; } = null!;
        public List<Predmet> Predmeti { get; set; } = new();
        public List<Termin> Termini { get; set; } = new();
        public List<RecenzijaViewModel> Recenzije { get; set; } = new();
    }
}