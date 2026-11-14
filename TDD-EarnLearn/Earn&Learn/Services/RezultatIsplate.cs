namespace Earn_Learn.Services
{
    // Rezultat provjere zahtjeva za isplatu
    public class RezultatIsplate
    {
        public bool Dozvoljeno { get; set; }
        public string Poruka { get; set; } = string.Empty;
        public double Naknada { get; set; }
        public double IznosZaBanku { get; set; }

        public static RezultatIsplate Odbijeno(string poruka)
        {
            return new RezultatIsplate { Dozvoljeno = false, Poruka = poruka };
        }

        public static RezultatIsplate Odobreno(double naknada, double iznosZaBanku)
        {
            return new RezultatIsplate { Dozvoljeno = true, Naknada = naknada, IznosZaBanku = iznosZaBanku };
        }
    }
}
