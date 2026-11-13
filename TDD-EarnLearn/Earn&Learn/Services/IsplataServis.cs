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

        private static double ZaokruziNaDvijeDecimale(double vrijednost)
        {
            return Math.Round(vrijednost, 2, MidpointRounding.AwayFromZero);
        }
    }
}
