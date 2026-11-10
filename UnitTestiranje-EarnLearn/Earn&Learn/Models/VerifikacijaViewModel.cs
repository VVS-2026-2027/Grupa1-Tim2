namespace Earn_Learn.Models
{
    public class VerifikacijaViewModel
    {
        public List<TutorSaPredmetima> TutoriNaCekanju { get; set; } = new();
        public int BrojVerifikovanih { get; set; }
    }

    public class TutorSaPredmetima
    {
        public Korisnik Tutor { get; set; } = null!;
        public List<Predmet> Predmeti { get; set; } = new();
        public List<Predmet> PredmetiNaCekanju { get; set; } = new();
        public Predmet? PredmetNaCekanju { get; set; }
    }
}