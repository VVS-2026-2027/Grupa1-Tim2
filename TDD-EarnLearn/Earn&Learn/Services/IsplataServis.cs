namespace Earn_Learn.Services
{
    // Pravila za isplatu zarade tutora (F1 - Isplata zarade tutora)
    public class IsplataServis : IIsplataServis
    {
        public const double ProcenatNaknade = 0.02;
        public const double MinimalnaNaknada = 1.0;
        public const double MinimalniIznosIsplate = 20.0;

        public const string PorukaMinimum = "Minimalan iznos isplate je 20 KM.";
        public const string PorukaNedovoljnoSredstava = "Nemate dovoljno sredstava za isplatu.";
        public const string PorukaJednomDnevno = "Dozvoljena je jedna isplata dnevno.";

        public double IzracunajNaknadu(double iznos)
        {
            if (iznos <= 0)
                throw new ArgumentOutOfRangeException(nameof(iznos), "Iznos isplate mora biti pozitivan.");

            return Math.Max(ZaokruziNaDvijeDecimale(iznos * ProcenatNaknade), MinimalnaNaknada);
        }

        public RezultatIsplate ProvjeriIsplatu(double stanje, double iznos, DateTime? zadnjaIsplata, DateTime sada)
        {
            if (iznos < MinimalniIznosIsplate)
                return RezultatIsplate.Odbijeno(PorukaMinimum);

            if (iznos > stanje)
                return RezultatIsplate.Odbijeno(PorukaNedovoljnoSredstava);

            if (VecIsplacenoDanas(zadnjaIsplata, sada))
                return RezultatIsplate.Odbijeno(PorukaJednomDnevno);

            double naknada = IzracunajNaknadu(iznos);
            return RezultatIsplate.Odobreno(naknada, ZaokruziNaDvijeDecimale(iznos - naknada));
        }

        private static bool VecIsplacenoDanas(DateTime? zadnjaIsplata, DateTime sada)
        {
            return zadnjaIsplata.HasValue && zadnjaIsplata.Value.Date == sada.Date;
        }

        private static double ZaokruziNaDvijeDecimale(double vrijednost)
        {
            return Math.Round(vrijednost, 2, MidpointRounding.AwayFromZero);
        }
    }
}
