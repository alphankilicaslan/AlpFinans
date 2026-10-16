using System;

namespace AlpFinans.Models;

public class TaxAccrual
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TaxType { get; set; } = "damga_vergisi";
    public double Amount { get; set; }
    public string DueDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string? Period { get; set; }
    public int IsPaid { get; set; } = 0;
    public string? PaidDate { get; set; }
    public string? ReceiptFile { get; set; }
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string? BelgeNo { get; set; }
    public double PrincipalAmount { get; set; }
    public double InterestAmount { get; set; }

    public string FormattedAmount => $"{Amount:N2} ₺";
    public bool StatusPaid => IsPaid == 1;
    public string StatusDisplay => StatusPaid ? "✅ Ödendi" : "⏳ Ödenecek / Bekliyor";
}
