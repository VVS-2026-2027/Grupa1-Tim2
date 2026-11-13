namespace Earn_Learn.Services
{
    // Rezultat provjere zahtjeva za isplatu
    public class RezultatIsplate
    {
        public bool Dozvoljeno { get; set; }
        public string Poruka { get; set; } = string.Empty;
        public double Naknada { get; set; }
        public double IznosZaBanku { get; set; }
    }
}
