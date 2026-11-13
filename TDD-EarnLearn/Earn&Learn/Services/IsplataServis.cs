namespace Earn_Learn.Services
{
    // Pravila za isplatu zarade tutora (F1 - Isplata zarade tutora)
    public class IsplataServis
    {
        public double IzracunajNaknadu(double iznos)
        {
            if (iznos <= 0)
                throw new ArgumentOutOfRangeException(nameof(iznos));

            double naknada = Math.Round(iznos * 0.02, 2, MidpointRounding.AwayFromZero);
            return Math.Max(naknada, 1.0);
        }
    }
}
