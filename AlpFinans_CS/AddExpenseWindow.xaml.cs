using System;
using System.Windows;
using System.Windows.Controls;
using AlpFinans.Models;
using AlpFinans.Services;

namespace AlpFinans;

public partial class AddExpenseWindow : Window
{
    public AddExpenseWindow(string prefillPath = "")
    {
        InitializeComponent();
        DpDate.SelectedDate = DateTime.Today;
        if (!string.IsNullOrEmpty(prefillPath))
        {
            TxtReceiptFile.Text = prefillPath;
            TxtTitle.Text = System.IO.Path.GetFileNameWithoutExtension(prefillPath);
        }
    }

    private void CalculateShield_Event(object sender, EventArgs e)
    {
        if (TxtShieldCalcDisplay == null) return;
        if (double.TryParse(TxtGross.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double gross))
        {
            int kdvRate = CmbKdv.SelectedItem is ComboBoxItem itemK ? Convert.ToInt32(itemK.Tag) : 20;
            double baseAmt = gross / (1.0 + (kdvRate / 100.0));
            double kdvAmt = gross - baseAmt;
            double shield = (baseAmt * 0.25) + kdvAmt; // Matrah %25 + KDV iadesi/indirimi

            TxtShieldCalcDisplay.Text = $"🛡️ Tahmini Vergi Tasarrufu: {shield:N2} ₺ (KDV: {kdvAmt:N2} ₺ + Gelir Vergisi Kalkanı: {(baseAmt * 0.25):N2} ₺)";
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text))
        {
            MessageBox.Show("Lütfen fatura açıklaması girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TxtGross.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double gross) || gross <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir tutar girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int kdvRate = CmbKdv.SelectedItem is ComboBoxItem itemK ? Convert.ToInt32(itemK.Tag) : 20;
        string expType = CmbExpenseType.SelectedItem is ComboBoxItem itemT ? itemT.Tag.ToString()! : "sirket_gideri";
        string category = CmbCategory.SelectedItem is ComboBoxItem itemC ? itemC.Tag.ToString()! : "dogrudan_gider";

        double baseAmt = gross / (1.0 + (kdvRate / 100.0));
        double kdvAmt = gross - baseAmt;
        double shield = expType == "sirket_gideri" ? ((baseAmt * 0.25) + kdvAmt) : 0;

        var exp = new Expense
        {
            Title = TxtTitle.Text.Trim(),
            ExpenseType = expType,
            Category = category,
            GrossAmount = gross,
            BaseAmount = baseAmt,
            KdvRate = kdvRate,
            KdvAmount = kdvAmt,
            TaxShieldAmount = shield,
            InvoiceDate = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"),
            VendorName = TxtVendor.Text.Trim(),
            InvoiceNo = TxtInvoiceNo.Text.Trim(),
            ReceiptFile = TxtReceiptFile.Text.Trim()
        };

        DatabaseService.Instance.AddExpense(exp);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
