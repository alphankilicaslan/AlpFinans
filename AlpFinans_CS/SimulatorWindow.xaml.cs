using System;
using System.Windows;

namespace AlpFinans;

public partial class SimulatorWindow : Window
{
    public SimulatorWindow()
    {
        InitializeComponent();
        Simulate();
    }

    private void Simulate_Event(object sender, RoutedEventArgs e) => Simulate();
    private void Simulate_Event(object sender, EventArgs e) => Simulate();

    private void Simulate()
    {
        if (TxtSimKdv == null) return;
        double.TryParse(TxtSimGross.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double gross);
        double.TryParse(TxtSimExpenses.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double exp);

        double kdvIncome = gross * (20.0 / 120.0);
        double kdvExp = exp * (20.0 / 120.0);
        double netKdv = Math.Max(0, kdvIncome - kdvExp);

        double baseIncome = gross - kdvIncome;
        double baseExp = exp - kdvExp;
        double profit = Math.Max(0, baseIncome - baseExp);

        double matrah = profit;
        if (ChkSimYoung.IsChecked == true)
        {
            matrah = Math.Max(0, profit - 230000.0);
        }

        // GVK 103
        double tax = 0;
        if (matrah <= 158000) tax = matrah * 0.15;
        else if (matrah <= 330000) tax = 23700 + (matrah - 158000) * 0.20;
        else if (matrah <= 800000) tax = 58100 + (matrah - 330000) * 0.27;
        else if (matrah <= 4300000) tax = 185000 + (matrah - 800000) * 0.35;
        else tax = 1410000 + (matrah - 4300000) * 0.40;

        double shield = (baseExp * 0.25) + kdvExp;
        // Şirket gideri cebe kalan net kardan düşülmez (Ana uygulama kuralı):
        double pocket = gross - tax - netKdv;

        TxtSimKdv.Text = $"{netKdv:N2} ₺";
        TxtSimMatrah.Text = $"{matrah:N2} ₺";
        TxtSimTax.Text = $"{tax:N2} ₺";
        TxtSimShield.Text = $"{shield:N2} ₺";
        TxtSimPocket.Text = $"{pocket:N2} ₺";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
