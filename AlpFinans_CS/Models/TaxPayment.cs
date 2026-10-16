using System;

namespace AlpFinans.Models;

public class TaxPayment
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TaxType { get; set; } = "kdv";
    public double Amount { get; set; }
    public string PaymentDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? ReceiptNo { get; set; }
    public string? Notes { get; set; }

    public string FormattedAmount => $"{Amount:N2} ₺";
    public string TaxTypeDisplay => TaxType switch
    {
        "kdv" => "🏛️ KDV Ödemesi",
        "gelir_vergisi" => "📋 Gelir Vergisi",
        "gecici_vergi" => "⏱️ Geçici Vergi",
        "stopaj" => "✂️ Muhtasar / Stopaj",
        _ => "💳 " + TaxType
    };
}
