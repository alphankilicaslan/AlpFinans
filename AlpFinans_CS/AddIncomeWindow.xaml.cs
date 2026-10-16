using System;
using System.Windows;
using System.Windows.Controls;
using AlpFinans.Models;
using AlpFinans.Services;

namespace AlpFinans;

public partial class AddIncomeWindow : Window
{
    public AddIncomeWindow()
    {
        InitializeComponent();
        DpDate.SelectedDate = DateTime.Today;
    }

    private void CalculateNet_Event(object sender, EventArgs e)
    {
        if (TxtNetCalcDisplay == null) return;
        if (double.TryParse(TxtGross.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double gross))
        {
            int kdvRate = CmbKdv.SelectedItem is ComboBoxItem itemK ? Convert.ToInt32(itemK.Tag) : 20;
            int stopajRate = CmbStopaj.SelectedItem is ComboBoxItem itemS ? Convert.ToInt32(itemS.Tag) : 0;

            double baseAmt = gross / (1.0 + (kdvRate / 100.0));
            double stopajAmt = baseAmt * (stopajRate / 100.0);
            double net = gross - stopajAmt;

            TxtNetCalcDisplay.Text = $"Hesaplanan Net Tahsilat: {net:N2} ₺ (Stopaj: {stopajAmt:N2} ₺)";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text))
        {
            MessageBox.Show("Lütfen gelir başlığı girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TxtGross.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double gross) || gross <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir tutar girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int kdvRate = CmbKdv.SelectedItem is ComboBoxItem itemK ? Convert.ToInt32(itemK.Tag) : 20;
        int stopajRate = CmbStopaj.SelectedItem is ComboBoxItem itemS ? Convert.ToInt32(itemS.Tag) : 0;
        string sourceType = CmbSourceType.SelectedItem is ComboBoxItem itemSrc ? itemSrc.Tag.ToString()! : "sirket_fatura";

        double baseAmt = gross / (1.0 + (kdvRate / 100.0));
        double kdvAmt = gross - baseAmt;
        double stopajAmt = baseAmt * (stopajRate / 100.0);
        double net = gross - stopajAmt;

        var inc = new Income
        {
            Title = TxtTitle.Text.Trim(),
            SourceType = sourceType,
            GrossAmount = gross,
            KdvRate = kdvRate,
            KdvAmount = kdvAmt,
            StopajRate = stopajRate,
            StopajAmount = stopajAmt,
            NetAmount = net,
            InvoiceDate = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
            ClientName = TxtClient.Text.Trim(),
            InvoiceNo = TxtInvoiceNo.Text.Trim()
        };

        DatabaseService.Instance.AddIncome(inc);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
