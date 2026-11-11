namespace Earn_Learn.Models
{
    public class StudentDashboardViewModel
    {
        public string Ime { get; set; } = "";
        public string Prezime { get; set; } = "";
        public List<TerminSaTutorom> NadolazaciTermini { get; set; } = new();
        public List<Korisnik> TopTutori { get; set; } = new();
    }

    public class TerminSaTutorom
    {
        public Termin Termin { get; set; } = null!;
        public Korisnik Tutor { get; set; } = null!;
        public Predmet? Predmet { get; set; }
    }
}