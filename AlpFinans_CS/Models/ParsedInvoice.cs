using System;

namespace AlpFinans.Models;

public class ParsedInvoice
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string Title { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string Ettn { get; set; } = string.Empty;
    public string InvoiceDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public double GrossAmount { get; set; }
    public double BaseAmount { get; set; }
    public int KdvRate { get; set; } = 20;
    public double KdvAmount { get; set; }
    public string Category { get; set; } = "dogrudan_gider";
    public string ExpenseType { get; set; } = "sirket_gideri";
    public double TaxShieldAmount { get; set; }
    public string RawText { get; set; } = string.Empty;

    public string FormattedGross => $"{GrossAmount:N2} ₺";
    public string FormattedBase => $"{BaseAmount:N2} ₺";
    public string FormattedKdv => $"{KdvAmount:N2} ₺ (%{KdvRate})";
    public string FormattedTaxShield => $"{TaxShieldAmount:N2} ₺";
}
