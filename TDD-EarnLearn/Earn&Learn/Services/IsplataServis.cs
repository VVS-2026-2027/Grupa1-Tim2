namespace Earn_Learn.Services
{
    // Pravila za isplatu zarade tutora (F1 - Isplata zarade tutora)
    public class IsplataServis
    {
        public const double ProcenatNaknade = 0.02;
        public const double MinimalnaNaknada = 1.0;

        public double IzracunajNaknadu(double iznos)
        {
            if (iznos <= 0)
                throw new ArgumentOutOfRangeException(nameof(iznos), "Iznos isplate mora biti pozitivan.");

            return Math.Max(ZaokruziNaDvijeDecimale(iznos * ProcenatNaknade), MinimalnaNaknada);
        }

        public RezultatIsplate ProvjeriIsplatu(double stanje, double iznos, DateTime? zadnjaIsplata, DateTime sada)
        {
            if (iznos < 20)
                return new RezultatIsplate { Dozvoljeno = false, Poruka = "Minimalan iznos isplate je 20 KM." };

            if (iznos > stanje)
                return new RezultatIsplate { Dozvoljeno = false, Poruka = "Nemate dovoljno sredstava za isplatu." };

            if (zadnjaIsplata != null && zadnjaIsplata.Value.Date == sada.Date)
                return new RezultatIsplate { Dozvoljeno = false, Poruka = "Dozvoljena je jedna isplata dnevno." };

            double naknada = IzracunajNaknadu(iznos);
            return new RezultatIsplate
            {
                Dozvoljeno = true,
                Naknada = naknada,
                IznosZaBanku = Math.Round(iznos - naknada, 2, MidpointRounding.AwayFromZero)
            };
        }

        private static double ZaokruziNaDvijeDecimale(double vrijednost)
        {
            return Math.Round(vrijednost, 2, MidpointRounding.AwayFromZero);
        }
    }
}
