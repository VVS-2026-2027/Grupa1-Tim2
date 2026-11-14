
namespace Earn_Learn.Services
{
    // Interfejs omogućava da se servis ubaci u kontroler (DI)
    // i zamijeni mockom u testovima
    public interface IPopustServis
    {
        int IzracunajProcenatPopusta(int brojOdrzanihCasova);

        double IzracunajCijenuSaPopustom(
            double cijena,
            int brojOdrzanihCasova);
    }
}
