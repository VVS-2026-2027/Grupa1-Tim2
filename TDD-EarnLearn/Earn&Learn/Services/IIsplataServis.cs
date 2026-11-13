namespace Earn_Learn.Services
{
    // Interfejs omogucava da se servis ubaci u kontroler (DI) i zamijeni mockom u testovima
    public interface IIsplataServis
    {
        double IzracunajNaknadu(double iznos);
        RezultatIsplate ProvjeriIsplatu(double stanje, double iznos, DateTime? zadnjaIsplata, DateTime sada);
    }
}
