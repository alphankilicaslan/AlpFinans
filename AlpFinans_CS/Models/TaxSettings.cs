namespace AlpFinans.Models;

public class TaxSettings
{
    public long Id { get; set; } = 1;
    public string CompanyType { get; set; } = "sahis"; // 'sahis', 'limited', 'anonim'
    public double FixedIncomeTaxRate { get; set; } = 0.20;
    public int IsYoungEntrepreneur { get; set; } = 1;
}
