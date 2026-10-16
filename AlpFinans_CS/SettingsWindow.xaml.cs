using System;
using System.Windows;
using System.Windows.Controls;
using AlpFinans.Models;
using AlpFinans.Services;

namespace AlpFinans;

public partial class SettingsWindow : Window
{
    private readonly TaxSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = DatabaseService.Instance.GetTaxSettings();
        TxtDbPath.Text = DatabaseService.Instance.DbPath;

        if (_settings.CompanyType == "limited") CmbCompanyType.SelectedIndex = 1;
        else if (_settings.CompanyType == "anonim") CmbCompanyType.SelectedIndex = 2;
        else CmbCompanyType.SelectedIndex = 0;

        ChkYoungEntrepreneur.IsChecked = _settings.IsYoungEntrepreneur == 1;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string type = CmbCompanyType.SelectedItem is ComboBoxItem item ? item.Tag.ToString()! : "sahis";
        _settings.CompanyType = type;
        _settings.IsYoungEntrepreneur = (ChkYoungEntrepreneur.IsChecked == true) ? 1 : 0;

        DatabaseService.Instance.SaveTaxSettings(_settings);
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
