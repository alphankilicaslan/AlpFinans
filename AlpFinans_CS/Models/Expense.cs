using System;

namespace AlpFinans.Models;

public class Expense
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "dogrudan_gider";
    public string ExpenseType { get; set; } = "sirket_gideri"; // 'sirket_gideri', 'kisisel_harcama'
    public double GrossAmount { get; set; }
    public double BaseAmount { get; set; }
    public int KdvRate { get; set; } = 20;
    public double KdvAmount { get; set; }
    public int DeductionRatio { get; set; } = 100;
    public int IsAssetDemirbas { get; set; } = 0;
    public string? AssetCategory { get; set; }
    public int UsefulYears { get; set; } = 1;
    public double AnnualDepreciationAmount { get; set; }
    public double TaxShieldAmount { get; set; }
    public string InvoiceDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? VendorName { get; set; }
    public string? InvoiceNo { get; set; }
    public string? ReceiptFile { get; set; }
    public string? Notes { get; set; }
    public string? Ettn { get; set; }
    public string Currency { get; set; } = "TRY";
    public double OriginalAmount { get; set; }
    public double ExchangeRate { get; set; } = 1.0;
    public string GroupName { get; set; } = string.Empty;

    public string FormattedGross => $"{GrossAmount:N2} ₺";
    public string FormattedBase => $"{BaseAmount:N2} ₺";
    public string FormattedKdv => $"{KdvAmount:N2} ₺ (%{KdvRate})";
    public string FormattedTaxShield => $"{TaxShieldAmount:N2} ₺";
    public bool IsCompanyExpense => ExpenseType == "sirket_gideri";
    public string ExpenseTypeDisplay => IsCompanyExpense ? "🏢 Şirket Gideri" : "👤 Kişisel Gider";
    public string CategoryDisplay => Category switch
    {
        "teknoloji_yazilim" => "💻 Teknoloji & Yazılım",
        "tadilat_insaat" => "🔨 Tadilat & Bakım",
        "beyaz_esya" => "🧊 Beyaz Eşya",
        "villa_esya_mobilya" => "🛋️ Mobilya & Yaşam",
        "havuz_ekipmani" => "🏊 Havuz Ekipmanı",
        "dogrudan_gider" => "📦 Genel Ofis / İş",
        "ulasim_yakit" => "⛽ Ulaşım & Yakıt",
        "yemek_temsil" => "🍽️ Temsil & Ağırlama",
        "faturalar" => "⚡ Su, Elektrik, Net",
        _ => "📄 " + Category
    };
}
