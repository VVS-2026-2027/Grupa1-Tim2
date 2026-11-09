namespace Earn_Learn.Models
{
    public class TutorProfilViewModel
    {
        public Korisnik Tutor { get; set; } = null!;
        public List<Predmet> Predmeti { get; set; } = new();
        public List<RecenzijaViewModel> Recenzije { get; set; } = new();
        public List<Termin> DostupniTermini { get; set; } = new();
        public double ProsjecnaOcjena { get; set; }
        public int UkupnoRecenzija { get; set; }
    }

    public class RecenzijaViewModel
    {
        public Recenzija Recenzija { get; set; } = null!;
        public string ImeStudenta { get; set; } = "Student";
    }
}