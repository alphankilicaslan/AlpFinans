using System;

namespace AlpFinans.Models;

public class Income
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceType { get; set; } = "sirket_fatura";
    public double GrossAmount { get; set; }
    public int KdvRate { get; set; } = 20;
    public double KdvAmount { get; set; }
    public int StopajRate { get; set; } = 0;
    public double StopajAmount { get; set; }
    public double NetAmount { get; set; }
    public string InvoiceDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? ClientName { get; set; }
    public string? InvoiceNo { get; set; }
    public int IsPaid { get; set; } = 1;
    public int IsTaxable { get; set; } = 1;
    public string? Notes { get; set; }
    public string? Ettn { get; set; }
    public string Currency { get; set; } = "TRY";
    public double OriginalAmount { get; set; }
    public double ExchangeRate { get; set; } = 1.0;
    public string GroupName { get; set; } = string.Empty;

    public string FormattedGross => $"{GrossAmount:N2} ₺";
    public string FormattedNet => $"{NetAmount:N2} ₺";
    public string FormattedKdv => $"{KdvAmount:N2} ₺ (%{KdvRate})";
    public string SourceTypeDisplay => SourceType switch
    {
        "villa_hakedis" => "🏡 Villa Hakedişi",
        "sirket_fatura" => "🏢 Şirket Faturası",
        "serbest_meslek" => "📑 Serbest Meslek (SMM)",
        "maas" => "👤 Maaş / Bireysel",
        _ => "💰 Diğer Gelir"
    };
}
