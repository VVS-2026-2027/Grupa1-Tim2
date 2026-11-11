namespace Earn_Learn.Models
{
    public class TutorDashboardViewModel
    {
        public string Ime { get; set; } = "";
        public int BrojCasova { get; set; }
        public double ProsjecnaOcjena { get; set; }
        public double Balans { get; set; }
        public List<TerminDetaljiViewModel> NadolazaciTermini { get; set; } = new();
    }
}