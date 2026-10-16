using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using AlpFinans.Models;

namespace AlpFinans.Services;

public class InvoiceScannerService
{
    private static readonly Regex EttnRegex = new(@"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", RegexOptions.IgnoreCase);
    private static readonly Regex InvoiceNoRegex = new(@"([A-Z0-9]{3}202[0-9]{9,11})", RegexOptions.IgnoreCase);
    private static readonly Regex DateRegex = new(@"([0-3]?[0-9])[\.\/\-]([0-1]?[0-9])[\.\/\-](202[0-9])");

    public static async Task<ParsedInvoice> ScanInvoiceFileAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            var parsed = new ParsedInvoice { FilePath = filePath };
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            string rawText = "";

            if (ext == ".pdf")
            {
                try
                {
                    using var document = PdfDocument.Open(filePath);
                    var sb = new System.Text.StringBuilder();
                    foreach (var page in document.GetPages())
                    {
                        sb.AppendLine(page.Text);
                    }
                    rawText = sb.ToString();
                }
                catch (Exception ex)
                {
                    rawText = $"PDF Okuma Hatası: {ex.Message}";
                }
            }
            else
            {
                // Fallback or Image: inspect filename and metadata
                rawText = Path.GetFileNameWithoutExtension(filePath);
            }

            parsed.RawText = rawText;
            ExtractFields(parsed, rawText, Path.GetFileName(filePath));
            return parsed;
        });
    }

    private static void ExtractFields(ParsedInvoice invoice, string rawText, string filename)
    {
        string norm = rawText.ToLower(new CultureInfo("tr-TR"));

        // 1. ETTN Extraction
        var ettnMatch = EttnRegex.Match(rawText);
        if (ettnMatch.Success) invoice.Ettn = ettnMatch.Groups[1].Value;

        // 2. Invoice No Extraction
        var invMatch = InvoiceNoRegex.Match(rawText);
        if (invMatch.Success)
        {
            invoice.InvoiceNo = invMatch.Groups[1].Value.ToUpperInvariant();
        }
        else
        {
            // Utility serial check e.g. 800004356704
            var utilMatch = Regex.Match(rawText, @"(?:Fatura No|Sözleşme Hesap No|Belge No)[:\s]*([0-9A-Z]{8,20})", RegexOptions.IgnoreCase);
            if (utilMatch.Success) invoice.InvoiceNo = utilMatch.Groups[1].Value.Trim();
        }

        // 3. Date Extraction
        var dateMatch = DateRegex.Match(rawText);
        if (dateMatch.Success)
        {
            string day = dateMatch.Groups[1].Value.PadLeft(2, '0');
            string month = dateMatch.Groups[2].Value.PadLeft(2, '0');
            string year = dateMatch.Groups[3].Value;
            invoice.InvoiceDate = $"{year}-{month}-{day}";
        }
        else
        {
            invoice.InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd");
        }

        // 4. Vendor Extraction
        invoice.VendorName = DetectVendor(rawText, norm, filename);

        // 5. Amount Extraction
        invoice.GrossAmount = DetectGrossAmount(rawText, norm);
        if (invoice.GrossAmount <= 0)
        {
            // Check numbers in filename e.g. 25000.pdf
            var fnMatch = Regex.Match(filename, @"(\d+(?:[\.,]\d+)?)");
            if (fnMatch.Success && double.TryParse(fnMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double fnAmt))
            {
                invoice.GrossAmount = fnAmt;
            }
        }

        // 6. KDV Rate Detection
        invoice.KdvRate = 20;
        if (norm.Contains("%10") || norm.Contains("kdv (%10)") || norm.Contains("kdv%10") || norm.Contains("aydem") || norm.Contains("elektrik"))
        {
            invoice.KdvRate = 10;
        }
        else if (norm.Contains("%1") || norm.Contains("kdv (%1)") || norm.Contains("kdv%1"))
        {
            invoice.KdvRate = 1;
        }
        else if (norm.Contains("%0") || norm.Contains("kdv (%0)") || norm.Contains("muaf"))
        {
            invoice.KdvRate = 0;
        }

        // Calculations
        if (invoice.GrossAmount > 0)
        {
            invoice.BaseAmount = invoice.GrossAmount / (1.0 + (invoice.KdvRate / 100.0));
            invoice.KdvAmount = invoice.GrossAmount - invoice.BaseAmount;
            invoice.TaxShieldAmount = (invoice.BaseAmount * 0.25) + invoice.KdvAmount;
        }

        // 7. Category Detection
        invoice.Category = DetectCategory(norm, invoice.VendorName);

        // 8. Title Formulation
        invoice.Title = string.IsNullOrWhiteSpace(invoice.VendorName)
            ? $"Fatura: {Path.GetFileNameWithoutExtension(filename)}"
            : $"{invoice.VendorName} Faturası";
    }

    private static string DetectVendor(string rawText, string norm, string filename)
    {
        if (norm.Contains("aydem")) return "Aydem Elektrik Perakende Satış A.Ş.";
        if (norm.Contains("muski") || norm.Contains("muğla su")) return "MUSKİ Muğla Su ve Kanalizasyon";
        if (norm.Contains("onat kırbaş") || norm.Contains("onat kirbas")) return "Onat Kırbaş";
        if (norm.Contains("aydınlar ticaret") || norm.Contains("osman aydın")) return "Aydınlar Ticaret Osman Aydın";
        if (norm.Contains("mevlüt özdil") || norm.Contains("ortaca yapı")) return "Ortaca Yapımarket Mevlüt Özdil";
        if (norm.Contains("ss yazilim") || norm.Contains("ss otomotiv")) return "SS YAZILIM OTOMOTIV TICARET LTD";
        if (norm.Contains("turkcell")) return "Turkcell İletişim Hizmetleri";
        if (norm.Contains("vodafone")) return "Vodafone Telekomünikasyon";
        if (norm.Contains("türk telekom") || norm.Contains("turk telekom")) return "Türk Telekom A.Ş.";
        if (norm.Contains("apple")) return "Apple Teknoloji";
        if (norm.Contains("trendyol")) return "Trendyol / DSM Grup";
        if (norm.Contains("amazon")) return "Amazon Turkey";
        if (norm.Contains("hepsiburada")) return "D-Market Hepsiburada";

        // Try to take first line with company suffix (A.Ş. / LTD / TİC)
        var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var l = line.Trim();
            if (l.Length > 4 && l.Length < 60 && (l.Contains("A.Ş") || l.Contains("LTD") || l.Contains("TİC") || l.Contains("SANAYİ") || l.Contains("ŞTİ")))
            {
                return l;
            }
        }

        return lines.Length > 0 && lines[0].Trim().Length < 50 ? lines[0].Trim() : "";
    }

    private static double DetectGrossAmount(string rawText, string norm)
    {
        // 1. Regex around total keywords
        var patterns = new[]
        {
            @"(?:Ödenecek\s+Tutar|ÖDENECEK\s+TUTAR|Fatura\s+Tutarı|FATURA\s+TUTARI|Genel\s+Toplam|GENEL\s+TOPLAM|Toplam\s+Tutar|TOPLAM\s+TUTAR)[:\s]*([0-9]{1,3}(?:\.[0-9]{3})*(?:\,[0-9]{2})|[0-9]+(?:\,[0-9]{2}))",
            @"(?:TOPLAM|Toplam)[:\s]*([0-9]{1,3}(?:\.[0-9]{3})*(?:\,[0-9]{2})|[0-9]+(?:\,[0-9]{2}))\s*(?:TL|₺|TRY)?",
            @"([0-9]{1,3}(?:\.[0-9]{3})*\,[0-9]{2})\s*(?:TL|₺|TRY)"
        };

        foreach (var p in patterns)
        {
            var m = Regex.Match(rawText, p, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string clean = m.Groups[1].Value.Replace(".", "").Replace(",", ".");
                if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) && val > 0)
                {
                    return val;
                }
            }
        }

        return 0.0;
    }

    private static string DetectCategory(string norm, string vendor)
    {
        string vNorm = vendor.ToLower(new CultureInfo("tr-TR"));
        if (norm.Contains("elektrik") || norm.Contains(" su ") || norm.Contains("su faturası") || norm.Contains("muski") || norm.Contains("aydem") || norm.Contains("internet") || norm.Contains("telekom"))
            return "faturalar";

        if (norm.Contains("korkuluk") || norm.Contains("alüminyum") || norm.Contains("boya") || norm.Contains("badana") || norm.Contains("çimento") || norm.Contains("demir") || norm.Contains("inşaat") || norm.Contains("tadilat") || norm.Contains("montaj"))
            return "tadilat_insaat";

        if (norm.Contains("havuz") || norm.Contains("klor") || norm.Contains("pompa"))
            return "havuz_ekipmani";

        if (norm.Contains("yazılım") || norm.Contains("yazilim") || norm.Contains("bilgisayar") || norm.Contains("laptop") || norm.Contains("hosting") || norm.Contains("domain") || norm.Contains("donanım"))
            return "teknoloji_yazilim";

        if (norm.Contains("mobilya") || norm.Contains("koltuk") || norm.Contains("yatak") || norm.Contains("masa"))
            return "villa_esya_mobilya";

        if (norm.Contains("beyaz eşya") || norm.Contains("klima") || norm.Contains("buzdolabı"))
            return "beyaz_esya";

        if (norm.Contains("akaryakıt") || norm.Contains("benzin") || norm.Contains("motorin") || norm.Contains("petrol") || norm.Contains("shell") || norm.Contains("opet"))
            return "ulasim_yakit";

        return "dogrudan_gider";
    }
}
