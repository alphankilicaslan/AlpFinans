using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using UglyToad.PdfPig;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PDFtoImage;
using AlpFinans.Models;
using AlpFinans.Services;

namespace AlpFinans;

public partial class MainWindow : Window
{
    private List<Income> _allIncomes = new();
    private List<Expense> _allExpenses = new();
    private List<TaxAccrual> _allAccruals = new();
    private string _activeExpenseChip = string.Empty;
    private string _activeIncomeChip = string.Empty;
    private string _currentScannedPath = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private static string GetAlpTogetherDeviceHwid()
    {
        try
        {
            string raw = $"ALPTOGETHER_SALT_2026_DEV_{Environment.MachineName.ToUpperInvariant()}_{Environment.UserName.ToUpperInvariant()}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            string hex = Convert.ToHexString(hash);
            return $"AT-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
        }
        catch
        {
            return "AT-UNKNOWN";
        }
    }

    private static bool IsPrimaryDevMachine()
    {
        try
        {
            string machine = Environment.MachineName;
            string user = Environment.UserName;
            if (machine.Contains("MONSTER", StringComparison.OrdinalIgnoreCase) ||
                machine.Contains("ALPHAN", StringComparison.OrdinalIgnoreCase))
                return true;

            if (user.Contains("Alphan", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }
        return false;
    }

    private async Task<bool> CheckCloudWhitelistAsync()
    {
        if (IsPrimaryDevMachine()) return true;

        try
        {
            string hwid = GetAlpTogetherDeviceHwid();
            string firebaseUrl = $"https://alpnet-d0bc4-default-rtdb.europe-west1.firebasedatabase.app/alptogether/users/{hwid}.json?auth=ExNWZwF2CkByKFILhfQ2BbTpuxYFlFAUO8quWwfc";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var http = new HttpClient();
            var resp = await http.GetAsync(firebaseUrl, cts.Token);
            if (!resp.IsSuccessStatusCode) return false;

            string json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null") return false;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("appPermissions", out var perms) &&
                perms.TryGetProperty("alpfinans", out var finansPerm))
            {
                bool hasAccess = finansPerm.TryGetProperty("hasAccess", out var acc) && acc.GetBoolean();
                if (hasAccess)
                {
                    string expiresAt = finansPerm.TryGetProperty("expiresAt", out var exp) ? exp.GetString() ?? "" : "";
                    int durationDays = finansPerm.TryGetProperty("durationDays", out var dur) ? dur.GetInt32() : 0;
                    if (durationDays >= 9999 || expiresAt.StartsWith("9999"))
                        return true;

                    if (DateTime.TryParse(expiresAt, out DateTime expDate))
                    {
                        if (DateTime.Now <= expDate) return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        bool isWhitelisted = await CheckCloudWhitelistAsync();
        if (!isWhitelisted)
        {
            string hwid = GetAlpTogetherDeviceHwid();
            MessageBox.Show(
                "⛔ AlpFinans Bulut Whitelist Doğrulanamadı!\n\n" +
                "Bu bilgisayar için aktif bir AlpFinans kullanım yetkisi bulunamadı veya süresi dolmuş.\n\n" +
                "Lütfen AlpTogether üzerinden yetkinizi kontrol ediniz veya yönetici (Alphan) ile iletişime geçiniz.\n\n" +
                $"Cihaz HWID: {hwid}",
                "Kullanım İzni Gerekli", MessageBoxButton.OK, MessageBoxImage.Warning);
            Application.Current.Shutdown();
            return;
        }

        SwitchView("dashboard");
        LoadAllData();
    }

    #region Navigation
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string viewName)
        {
            SwitchView(viewName);
        }
    }

    private void SwitchView(string viewName)
    {
        ViewDashboard.Visibility = viewName == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ViewScanner.Visibility = viewName == "scanner" ? Visibility.Visible : Visibility.Collapsed;
        ViewExpenses.Visibility = viewName == "expenses" ? Visibility.Visible : Visibility.Collapsed;
        ViewIncomes.Visibility = viewName == "incomes" ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = viewName == "settings" ? Visibility.Visible : Visibility.Collapsed;

        SetNavActive(BtnNavDashboard, viewName == "dashboard");
        SetNavActive(BtnNavScanner, viewName == "scanner");
        SetNavActive(BtnNavExpenses, viewName == "expenses");
        SetNavActive(BtnNavIncomes, viewName == "incomes");
        SetNavActive(BtnNavSettings, viewName == "settings");

        TxtHeaderTitle.Text = viewName switch
        {
            "dashboard" => "📋  Genel Bakış & Vergi Raporu",
            "scanner" => "📸  Fatura & PDF Okuyucu",
            "expenses" => "📄  Faturalar & Gider Hakları",
            "incomes" => "💰  Gelirler & Hakediş",
            "settings" => "⚙️  Şirket & Vergi Profili",
            _ => "AlpFinans"
        };
    }

    private void SetNavActive(Button btn, bool isActive)
    {
        if (isActive)
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.BorderBrush = Brushes.Transparent;
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        }
    }

    private void BtnNavToAccruals_Click(object sender, RoutedEventArgs e)
    {
        SwitchView("settings");
    }
    #endregion

    #region Data Loading & Dashboard
    public void LoadAllData()
    {
        try
        {
            var db = DatabaseService.Instance;
            var settings = db.GetTaxSettings();

            // Status Badge in Sidebar
            TxtHeaderTaxRate.Text = settings.IsYoungEntrepreneur == 1
                ? "☁️ Genç Girişimci (%20 Vergi + ₺230K Muafiyet)"
                : (settings.CompanyType == "sahis" ? "☁️ Villa & Şahıs (%20 Sabit Vergi)" : "🏢 Kurumlar Vergisi (%25)");

            // Load Entities
            _allIncomes = db.GetIncomes();
            _allExpenses = db.GetExpenses();
            _allAccruals = db.GetTaxAccruals();

            // Calculate Summary
            var summary = db.CalculateSummary();

            // Card 1: Gelir, Gider & Net Nakit Kasa (Matching Image 2)
            TxtTotalGrossIncome.Text = summary.FormattedGrossIncome;
            TxtSubGrossTicari.Text = $"Ticari: {summary.FormattedTaxableIncomeGross}";
            TxtCompanyExpenses.Text = summary.FormattedCompanyExpenses;
            TxtExemptIncome.Text = summary.FormattedTaxExemptIncomeGross;
            TxtNetPocketProfit.Text = summary.FormattedNetPocketProfit;

            // Card 2: Resmi Vergi Dairesi & Borç Tablosu (Matching Image 2)
            TxtUnshieldedTax.Text = summary.FormattedUnshieldedTax;
            TxtTaxShield.Text = $"- {summary.FormattedTaxShield}";
            TxtTotalCalculatedTax.Text = summary.FormattedTotalTaxLiability;
            TxtRemainingTax.Text = summary.FormattedRemainingTaxToPay;
            TxtSubRemainingTax.Text = summary.RemainingTaxToPay > 0 ? "Ödenmesi Gereken Net Borç" : "Ödenecek Borç Yok";

            // Card 3: Middle Interactive Donut Chart & Breakdown
            DrawDonutChart(summary);

            // Card 4: Bottom 3 Boxes (KDV, Gelir Vergisi, Resmi Vergi Takvimi)
            TxtBottomKdvStatus.Text = $"{summary.NetKdvStatusText}: {summary.FormattedNetKdv}";
            TxtBottomKdvDetails.Text = $"Kestiğiniz Satış KDV: {summary.FormattedCalculatedKdv} | İndirilecek Gider KDV: {summary.FormattedDeductibleKdv}";
            TxtBottomTaxableBase.Text = $"Vergi Matrahı: {summary.FormattedTaxableProfit}";
            TxtBottomEstimatedTax.Text = $"Hesaplanan Gelir Vergisi: {summary.FormattedEstimatedTax} (%20)";
            TxtCalP3Badge.Text = summary.RemainingTaxToPay > 0
                ? $"⏳ Ödenecek: {summary.FormattedRemainingTaxToPay}"
                : "✓ Borç Yok";

            // Bind Grids
            ApplyExpenseFilter();
            ApplyIncomeFilter();
            GridAccruals.ItemsSource = _allAccruals;

            // Settings Form values
            CmbCompanyType.SelectedIndex = settings.CompanyType == "limited" ? 1 : (settings.CompanyType == "anonim" ? 2 : 0);
            TxtFixedTaxRate.Text = (settings.FixedIncomeTaxRate * 100).ToString("F0");
            ChkYoungEntrepreneur.IsChecked = settings.IsYoungEntrepreneur == 1;

            TxtStatus.Text = $"✅ Aktif: {System.IO.Path.GetFileName(db.DbPath)} ({_allIncomes.Count} Gelir, {_allExpenses.Count} Fatura)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Veri yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DrawDonutChart(DashboardSummary summary)
    {
        DonutCanvas.Children.Clear();

        double taxableInc = summary.TaxableIncomeGross;
        double exemptInc = summary.TaxExemptIncomeGross;
        double compExp = summary.TotalCompanyExpensesGross;
        double taxLiability = summary.RemainingTaxToPay > 0 ? summary.RemainingTaxToPay : summary.TotalTaxLiability;

        double totalVal = taxableInc + exemptInc + compExp + taxLiability;
        if (totalVal <= 0) totalVal = 1.0;

        int p1 = (int)Math.Round(taxableInc / totalVal * 100.0);
        int p2 = (int)Math.Round(exemptInc / totalVal * 100.0);
        int p3 = (int)Math.Round(compExp / totalVal * 100.0);
        int p4 = Math.Max(0, 100 - (p1 + p2 + p3));

        var slices = new List<(double Value, string ColorHex, string PctText)>
        {
            (taxableInc, "#10B981", $"%{p1}"),
            (exemptInc, "#38BDF8", $"%{p2}"),
            (compExp, "#F59E0B", $"%{p3}"),
            (taxLiability > 0 ? taxLiability : 1.0, "#8B5CF6", $"%{p4}")
        };

        double total = slices.Sum(s => s.Value);
        if (total <= 0) total = 1.0;

        double cx = 190.0;
        double cy = 160.0;
        double outerR = 115.0;
        double innerR = 70.0;
        double currentAngle = -90.0;

        foreach (var slice in slices)
        {
            double sweepAngle = (slice.Value / total) * 360.0;
            if (sweepAngle < 1.0) sweepAngle = 1.0;
            if (sweepAngle >= 360.0) sweepAngle = 359.99;
            double endAngle = currentAngle + sweepAngle;

            var path = CreateDonutSlice(cx, cy, innerR, outerR, currentAngle, endAngle, slice.ColorHex);
            DonutCanvas.Children.Add(path);

            // Percentage label positioned at slice midpoint
            double midAngle = currentAngle + (sweepAngle / 2.0);
            double midR = (innerR + outerR) / 2.0;
            double rad = midAngle * Math.PI / 180.0;
            double px = cx + midR * Math.Cos(rad);
            double py = cy + midR * Math.Sin(rad);

            var pctBlock = new TextBlock
            {
                Text = slice.PctText,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            pctBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(pctBlock, px - (pctBlock.DesiredSize.Width / 2.0));
            Canvas.SetTop(pctBlock, py - (pctBlock.DesiredSize.Height / 2.0));
            DonutCanvas.Children.Add(pctBlock);

            currentAngle = endAngle;
        }

        // Center Text in Donut Hole
        TxtChartCenterAmount.Text = $"{summary.NetPocketProfit:N0} ₺";
        TxtChartCenterLabel.Text = "Net Kâr";

        // Right Breakdown Stats Table
        TxtStatTaxableInc.Text = summary.FormattedTaxableIncomeGross;
        TxtStatExemptInc.Text = summary.FormattedTaxExemptIncomeGross;
        TxtStatCompExp.Text = summary.FormattedCompanyExpenses;
        TxtStatTaxCalc.Text = summary.FormattedTotalTaxLiability;
        TxtStatTaxPaid.Text = summary.FormattedTotalPaidTaxes;
        TxtStatTaxRem.Text = summary.FormattedRemainingTaxToPay;
        TxtStatNetProfit.Text = summary.FormattedNetPocketProfit;
        TxtStatProfitMargin.Text = summary.FormattedProfitMargin;

        // Smooth subtle opening rotation animation on load/open
        var rotate = new RotateTransform(0, cx, cy);
        DonutCanvas.RenderTransform = rotate;
        var anim = new DoubleAnimation
        {
            From = -90,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(750),
            EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    private System.Windows.Shapes.Path CreateDonutSlice(double cx, double cy, double innerR, double outerR, double startAngle, double endAngle, string colorHex)
    {
        double startRad = startAngle * Math.PI / 180.0;
        double endRad = endAngle * Math.PI / 180.0;

        Point p1 = new(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
        Point p2 = new(cx + outerR * Math.Cos(endRad), cy + outerR * Math.Sin(endRad));
        Point p3 = new(cx + innerR * Math.Cos(endRad), cy + innerR * Math.Sin(endRad));
        Point p4 = new(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));

        bool isLargeArc = (endAngle - startAngle) > 180.0;

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, isLargeArc, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, isLargeArc, SweepDirection.Counterclockwise, true));

        var geom = new PathGeometry();
        geom.Figures.Add(fig);

        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
        return new System.Windows.Shapes.Path
        {
            Data = geom,
            Fill = brush,
            Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString("#090E1A")!,
            StrokeThickness = 2
        };
    }
    #endregion

    #region Fatura & PDF Okuyucu (Scanner - Image 3)
    private void BtnScannerSelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Fatura veya PDF Belgesi Seç",
            Filter = "Fatura Belgeleri (*.pdf;*.png;*.jpg;*.jpeg)|*.pdf;*.png;*.jpg;*.jpeg|Tüm Dosyalar (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            ProcessScannedFile(dlg.FileName);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Keyboard.FocusedElement is not TextBox)
            {
                BtnScannerPaste_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }

    private void BtnScannerPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                if (files.Count > 0)
                {
                    ProcessScannedFile(files[0]!);
                    return;
                }
            }

            if (Clipboard.ContainsImage())
            {
                var imageSource = Clipboard.GetImage();
                if (imageSource != null)
                {
                    string invoicesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Veriler_ve_Faturalar", "Faturalar");
                    Directory.CreateDirectory(invoicesDir);
                    string targetFile = System.IO.Path.Combine(invoicesDir, $"fatura_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.png");

                    using (var fileStream = new FileStream(targetFile, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(imageSource));
                        encoder.Save(fileStream);
                    }

                    ProcessScannedFile(targetFile);
                    return;
                }
            }

            MessageBox.Show("Panoda fatura dosyası veya kopyalanmış resim bulunamadı. Lütfen bir fatura kopyalayıp (Ctrl+C) tekrar deneyin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Panodan yapıştırılırken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnShowImgPreview_Click(object sender, RoutedEventArgs e)
    {
        ScrollImgPreview.Visibility = Visibility.Visible;
        TxtScannerRawPreview.Visibility = Visibility.Collapsed;
        BtnShowImgPreview.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        BtnShowImgPreview.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        BtnShowImgPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        BtnShowTextPreview.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        BtnShowTextPreview.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        BtnShowTextPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    private void BtnShowTextPreview_Click(object sender, RoutedEventArgs e)
    {
        ScrollImgPreview.Visibility = Visibility.Collapsed;
        TxtScannerRawPreview.Visibility = Visibility.Visible;
        BtnShowTextPreview.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        BtnShowTextPreview.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        BtnShowTextPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        BtnShowImgPreview.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        BtnShowImgPreview.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        BtnShowImgPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    private void BtnScannerOpenOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentScannedPath) && File.Exists(_currentScannedPath))
        {
            try { Process.Start(new ProcessStartInfo(_currentScannedPath) { UseShellExecute = true }); } catch { }
        }
        else
        {
            MessageBox.Show("Açılacak aktif bir belge bulunmuyor.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BorderScannerDropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                ProcessScannedFile(files[0]);
            }
        }
    }

    private void BorderScannerDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryParseTurkishAmount(string? input, out double amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().Replace("TL", "", StringComparison.OrdinalIgnoreCase).Replace("₺", "").Trim();
        if (s.Contains('.') && s.Contains(','))
        {
            s = s.Replace(".", "").Replace(',', '.');
        }
        else if (s.Contains(','))
        {
            s = s.Replace(',', '.');
        }
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
    }

    private void ProcessScannedFile(string filePath)
    {
        try
        {
            _currentScannedPath = filePath;
            TxtScanFileName.Text = System.IO.Path.GetFileName(filePath);
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            string rawText = string.Empty;
            var allLines = new List<string>();

            // 1. Visual Document Preview Rendering
            if (ext == ".pdf")
            {
                using (var inStream = File.OpenRead(filePath))
                using (var ms = new MemoryStream())
                {
                    Conversion.SavePng(ms, inStream, page: 0);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ImgScannerPreview.Source = bitmap;
                }

                ScrollImgPreview.Visibility = Visibility.Visible;
                TxtScannerRawPreview.Visibility = Visibility.Collapsed;
                TxtPreviewStatusBadge.Text = "✓ PDF Sayfası Render Edildi";

                using var document = PdfDocument.Open(filePath);
                foreach (var page in document.GetPages())
                {
                    var lines = page.GetWords()
                        .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0) * 4.0)
                        .OrderByDescending(g => g.Key)
                        .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)).Trim())
                        .Where(l => !string.IsNullOrWhiteSpace(l));
                    allLines.AddRange(lines);
                }
                rawText = string.Join(Environment.NewLine, allLines);
            }
            else if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp")
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();

                ImgScannerPreview.Source = bitmap;
                ScrollImgPreview.Visibility = Visibility.Visible;
                TxtScannerRawPreview.Visibility = Visibility.Collapsed;
                TxtPreviewStatusBadge.Text = "✓ Resim Belgesi Yüklendi";

                rawText = $"[Görüntü Dosyası: {System.IO.Path.GetFileName(filePath)}]";
                allLines.Add(rawText);
            }
            else
            {
                rawText = $"[Belge Dosyası: {System.IO.Path.GetFileName(filePath)}]";
                allLines.Add(rawText);
            }

            PanelScannerEmptyPlaceholder.Visibility = Visibility.Collapsed;
            GridPreviewSwitcher.Visibility = Visibility.Visible;
            TxtScannerRawPreview.Text = rawText;

            // Reset switcher button colors
            BtnShowImgPreview.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
            BtnShowImgPreview.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            BtnShowImgPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            BtnShowTextPreview.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            BtnShowTextPreview.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            BtnShowTextPreview.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));

            // Auto-extract fields
            AutoExtractFieldsFromText(rawText, filePath, allLines);
            ScanForm_Recalculate(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Belge işlenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutoExtractFieldsFromText(string text, string filePath, List<string> lines)
    {
        // 1. Total Gross Amount
        double gross = 0;
        var amtMatch = Regex.Match(text, @"(?:Ödenecek\s*Tutar|Vergiler\s*Dahil\s*Toplam\s*Tutar|Genel\s*Toplam)\s*[:\s]*([\d\.]+(?:,\d{2})?)", RegexOptions.IgnoreCase);
        if (!amtMatch.Success)
        {
            amtMatch = Regex.Match(text, @"(?:Toplam\s*Tutar|Ödenecek)\s*[:\s]*([\d\.]+(?:,\d{2})?)", RegexOptions.IgnoreCase);
        }
        if (!amtMatch.Success)
        {
            amtMatch = Regex.Match(text, @"(?:TOPLAM|TUTAR)[^\d]{1,15}([\d\.\,]+)", RegexOptions.IgnoreCase);
        }

        if (amtMatch.Success)
        {
            string cleanAmt = amtMatch.Groups[1].Value;
            if (TryParseTurkishAmount(cleanAmt, out double val))
            {
                gross = val;
                TxtScanGross.Text = gross.ToString("N2", new CultureInfo("tr-TR"));
            }
        }

        // 2. Invoice Date
        var dateMatch = Regex.Match(text, @"(?:Fatura\s*Tarihi|Tarih)\s*[:\s]*(\d{2}[-/.]\d{2}[-/.]\d{4})", RegexOptions.IgnoreCase);
        if (dateMatch.Success)
        {
            if (DateTime.TryParseExact(dateMatch.Groups[1].Value.Replace('/', '-').Replace('.', '-'), "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                TxtScanDate.Text = dt.ToString("yyyy-MM-dd");
            }
        }
        else
        {
            var genDate = Regex.Match(text, @"(\d{4}[-/.]\d{2}[-/.]\d{2})|(\d{2}[-/.]\d{2}[-/.]\d{4})");
            if (genDate.Success && DateTime.TryParse(genDate.Value, out DateTime dt))
            {
                TxtScanDate.Text = dt.ToString("yyyy-MM-dd");
            }
        }

        // 3. Invoice No
        var invNoMatch = Regex.Match(text, @"Fatura\s*No\s*[:\s]*([A-Z0-9]{13,16})", RegexOptions.IgnoreCase);
        if (invNoMatch.Success)
        {
            TxtScanInvNo.Text = invNoMatch.Groups[1].Value;
        }
        else
        {
            var noMatch = Regex.Match(text, @"(?:Fatura No|Belge No|İrsaliye No)[^\w]{0,5}([A-Z0-9]{10,16})", RegexOptions.IgnoreCase);
            if (noMatch.Success)
            {
                TxtScanInvNo.Text = noMatch.Groups[1].Value;
            }
        }

        // 4. Vendor Name (Satıcı)
        string vendor = "";
        foreach (var line in lines)
        {
            string lTrim = line.Trim();
            if (lTrim.StartsWith("SAYIN", StringComparison.OrdinalIgnoreCase) || lTrim.Contains("ALPHAN KILIÇASLAN"))
                break; // Stop at buyer!

            if (lTrim.Contains("e-Belge") || lTrim.Contains("e-Arşiv") || lTrim.Contains("file:///") ||
                lTrim.StartsWith("ATATÜRK") || lTrim.StartsWith("CAD") || lTrim.StartsWith("SOK") || lTrim.StartsWith("MAH") ||
                lTrim.StartsWith("Tel:") || lTrim.StartsWith("Fax:") || lTrim.StartsWith("Web") || lTrim.StartsWith("E-Posta:") ||
                lTrim.StartsWith("Vergi Dairesi:") || lTrim.StartsWith("TCKN:") || lTrim.StartsWith("VKN:") ||
                Regex.IsMatch(lTrim, @"^\d{1,2}[\.-]\d{1,2}[\.-]\d{4}"))
            {
                continue;
            }

            if (lTrim.Length >= 3 && Regex.IsMatch(lTrim, @"[A-Za-zÇĞİÖŞÜçğıöşü]"))
            {
                vendor = lTrim;
                break;
            }
        }

        if (string.IsNullOrEmpty(vendor))
        {
            string tLowerVendor = text.ToLowerInvariant();
            if (tLowerVendor.Contains("aydem") || tLowerVendor.Contains("enerjisa")) vendor = "Aydem Elektrik Dağıtım";
            else if (tLowerVendor.Contains("muski") || tLowerVendor.Contains("su kanalizasyon")) vendor = "MUSKİ Su ve Kanalizasyon";
            else if (tLowerVendor.Contains("telekom") || tLowerVendor.Contains("turkcell") || tLowerVendor.Contains("vodafone")) vendor = "Türk Telekom / İletişim";
            else vendor = System.IO.Path.GetFileNameWithoutExtension(filePath);
        }
        TxtScanVendor.Text = vendor;

        // 5. Item Title (Mal/Hizmet Açıklaması)
        string itemTitle = "";
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("Mal Hizmet") || lines[i].Contains("Mal/Hizmet"))
            {
                for (int j = i + 1; j < Math.Min(lines.Count, i + 10); j++)
                {
                    string candidate = lines[j];
                    if (candidate.StartsWith("Mal Hizmet Toplam", StringComparison.OrdinalIgnoreCase) || candidate.StartsWith("Toplam", StringComparison.OrdinalIgnoreCase))
                        break;

                    var m = Regex.Match(candidate, @"^\d*\s*([A-ZÇĞİÖŞÜa-zçğıöşü\s\-]{3,40}?)\s+(?:\d+(?:[\.,]\d+)?\s*(?:m|metre|adet|kg|lt|saat|gün|kutu|paket|tl))", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        itemTitle = m.Groups[1].Value.Trim();
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(itemTitle)) break;
            }
        }
        if (string.IsNullOrEmpty(itemTitle))
        {
            itemTitle = "İşletme Gider Faturası";
        }
        TxtScanTitle.Text = itemTitle;

        // 6. Category Selection
        string combined = (vendor + " " + itemTitle + " " + text).ToLowerInvariant();
        if (combined.Contains("korkuluk") || combined.Contains("montaj") || combined.Contains("tadilat") || 
            combined.Contains("inşaat") || combined.Contains("aliminyum") || combined.Contains("alüminyum") || 
            combined.Contains("demir") || combined.Contains("çimento") || combined.Contains("boya") || combined.Contains("seramik"))
        {
            CmbScanCategory.SelectedIndex = 4; // Villa Tadilat & İnşaat
        }
        else if (combined.Contains("aydem") || combined.Contains("enerjisa") || combined.Contains("elektrik"))
        {
            CmbScanCategory.SelectedIndex = 0; // Elektrik
        }
        else if (combined.Contains("muski") || combined.Contains("su kanalizasyon") || combined.Contains("iski"))
        {
            CmbScanCategory.SelectedIndex = 1; // Su
        }
        else if (combined.Contains("telekom") || combined.Contains("turkcell") || combined.Contains("vodafone") || combined.Contains("internet"))
        {
            CmbScanCategory.SelectedIndex = 2; // İnternet
        }
        else if (combined.Contains("apple") || combined.Contains("laptop") || combined.Contains("bilgisayar") || combined.Contains("donanım") || combined.Contains("monitör"))
        {
            CmbScanCategory.SelectedIndex = 3; // Bilgisayar & Donanım
        }
        else if (combined.Contains("yakıt") || combined.Contains("benzin") || combined.Contains("mazot") || combined.Contains("dizel") || combined.Contains("petrol") || combined.Contains("shell") || combined.Contains("opet"))
        {
            CmbScanCategory.SelectedIndex = 8; // Araç & Ulaşım
        }
        else if (combined.Contains("mobilya") || combined.Contains("koltuk") || combined.Contains("yatak") || combined.Contains("dolap") || combined.Contains("masa"))
        {
            CmbScanCategory.SelectedIndex = 6; // Villa Eşya & Mobilya
        }
        else if (combined.Contains("havuz") || combined.Contains("klor") || combined.Contains("pompa") || combined.Contains("filtre"))
        {
            CmbScanCategory.SelectedIndex = 7; // Havuz Ekipmanı
        }
        else
        {
            CmbScanCategory.SelectedIndex = 5; // Doğrudan Sarf
        }

        // 7. VAT (KDV) Rate & Water Bill Multi-rate
        bool isMuski = combined.Contains("muski") || combined.Contains("su ve kanalizasyon") || combined.Contains("atik su") || combined.Contains("kati atik") || combined.Contains("su faturasi");
        bool isAydem = combined.Contains("aydem") || combined.Contains("enerjisa") || combined.Contains("gediz") || combined.Contains("elektrik perakende");

        if (isMuski)
        {
            CmbScanKdv.SelectedIndex = 4; // 💧 Karma KDV (Su Faturası: %1 + %10 + %20)
        }
        else if (isAydem)
        {
            CmbScanKdv.SelectedIndex = 1; // %10 KDV (Elektrik vb.)
        }
        else
        {
            // Standard Turkish Invoice Priority: Check %20 first!
            string normText = text.ToLowerInvariant();
            if (normText.Contains("hesaplanan kdv(%20)") || normText.Contains("hesaplanan kdv (%20)") ||
                normText.Contains("kdv(%20)") || normText.Contains("kdv (%20)") || normText.Contains("%20 kdv") ||
                normText.Contains("kdv %20") || normText.Contains("kdv: %20") || normText.Contains("kdv orani: %20") ||
                normText.Contains("kdv orani %20") || normText.Contains("kdv: % 20") ||
                Regex.IsMatch(normText, @"(?:oran[ıi]|kdv)[^\d]{0,8}%?\s*20\b"))
            {
                CmbScanKdv.SelectedIndex = 0; // %20 KDV
            }
            else if (normText.Contains("hesaplanan kdv(%10)") || normText.Contains("hesaplanan kdv (%10)") ||
                     normText.Contains("kdv(%10)") || normText.Contains("kdv (%10)") || normText.Contains("%10 kdv") ||
                     normText.Contains("kdv %10") || normText.Contains("kdv: %10") || normText.Contains("kdv orani: %10") ||
                     normText.Contains("kdv orani %10") ||
                     Regex.IsMatch(normText, @"(?:oran[ıi]|kdv)[^\d]{0,8}%?\s*10\b"))
            {
                CmbScanKdv.SelectedIndex = 1; // %10 KDV
            }
            else if (normText.Contains("hesaplanan kdv(%1)") || normText.Contains("hesaplanan kdv (%1)") ||
                     normText.Contains("kdv(%1)") || normText.Contains("kdv (%1)") || normText.Contains("%1 kdv") ||
                     normText.Contains("kdv %1") || normText.Contains("kdv: %1") || normText.Contains("kdv orani: %1") ||
                     normText.Contains("kdv orani %1") ||
                     Regex.IsMatch(normText, @"(?:oran[ıi]|kdv)[^\d]{0,8}%?\s*1\b"))
            {
                CmbScanKdv.SelectedIndex = 2; // %1 KDV
            }
            else if (normText.Contains("istisna kapsamında") || normText.Contains("kdv muaf") ||
                     normText.Contains("hesaplanan kdv(%0)") || normText.Contains("hesaplanan kdv (%0)") ||
                     normText.Contains("kdv istisna") ||
                     Regex.IsMatch(normText, @"hesaplanan\s*kdv\s*[:\s]*0[,\.]00\s*tl"))
            {
                CmbScanKdv.SelectedIndex = 3; // %0 KDV (Muaf / İstisna)
            }
            else
            {
                CmbScanKdv.SelectedIndex = 0; // %20 KDV (Standard Default)
            }
        }

        CmbScanType.SelectedIndex = 0; // Şirket / Villa Gideri (Vergi Kalkanı)
    }

    private (bool IsDuplicate, string Reason, Expense? ExistingRecord) CheckAndShowDuplicateWarning()
    {
        if (BorderDuplicateWarning == null || BtnSaveScannedExpense == null) 
            return (false, string.Empty, null);

        TryParseTurkishAmount(TxtScanGross?.Text, out double gross);
        string? invNo = TxtScanInvNo?.Text?.Trim();
        string? vendor = TxtScanVendor?.Text?.Trim();
        string? dateStr = TxtScanDate?.Text?.Trim();
        string? category = CmbScanCategory?.SelectedIndex switch
        {
            0 or 1 or 2 => "faturalar",
            _ => null
        };

        if (gross > 0 && (!string.IsNullOrWhiteSpace(invNo) || !string.IsNullOrWhiteSpace(vendor)))
        {
            var dup = DatabaseService.Instance.CheckDuplicateInvoice(invNo, invNo, vendor, gross, dateStr, category);
            if (dup.IsDuplicate)
            {
                BorderDuplicateWarning.Visibility = Visibility.Visible;
                TxtDuplicateWarningReason.Text = $"{dup.Reason}\nBu fatura {dup.ExistingRecord?.InvoiceDate} tarihinde {dup.ExistingRecord?.GrossAmount:N2} ₺ tutarıyla sisteme eklenmiştir.";
                BtnSaveScannedExpense.Background = new SolidColorBrush(Color.FromRgb(225, 29, 72)); // Rose red
                BtnSaveScannedExpense.Content = "⚠️ Mükerrer Fatura (Zaten Kayıtlı) - Yine de Kaydet?";
                return dup;
            }
        }

        BorderDuplicateWarning.Visibility = Visibility.Collapsed;
        BtnSaveScannedExpense.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
        BtnSaveScannedExpense.Content = "💾 Faturayı Kaydet ve Vergi Kalkanına Ekle";
        return (false, string.Empty, null);
    }

    private void ScanForm_Recalculate(object sender, EventArgs e)
    {
        if (TxtScanTaxShield == null || TxtScanGross == null || CmbScanKdv == null || CmbScanType == null) return;

        TryParseTurkishAmount(TxtScanGross.Text, out double gross);
        int kdvIndex = CmbScanKdv.SelectedIndex;
        bool isCompany = CmbScanType.SelectedIndex == 0;

        double baseAmt;
        double kdvAmt;
        int effectiveKdvRate;

        if (kdvIndex == 4) // 💧 Karma KDV (Su Faturası: %1 + %10 + %20)
        {
            effectiveKdvRate = 4;
            // Standard MUSKİ water bill breakdown: İçme Suyu (%1) + Atıksu (%10) + Katı Atık (%20)
            // Exactly ~264.15 ₺ on 6,234.85 ₺ (ratio ~ 0.0423666)
            kdvAmt = Math.Round(gross * (264.15 / 6234.85), 2);
            baseAmt = Math.Round(gross - kdvAmt, 2);
        }
        else
        {
            effectiveKdvRate = kdvIndex switch
            {
                1 => 10,
                2 => 1,
                3 => 0,
                _ => 20
            };
            baseAmt = Math.Round(gross / (1.0 + (effectiveKdvRate / 100.0)), 2);
            kdvAmt = Math.Round(gross - baseAmt, 2);
        }

        if (gross > 0 && isCompany)
        {
            double incomeTaxSaving = Math.Round(baseAmt * 0.20, 2); // %20 Gelir Vergisi Tasarrufu
            double shield = Math.Round(incomeTaxSaving + kdvAmt, 2);
            if (kdvIndex == 4)
            {
                TxtScanTaxShield.Text = $"🛡️ Su Faturası Vergi Kalkanı: {shield:N2} ₺ (💧 Karma KDV [%1+%10+%20]: {kdvAmt:N2} ₺ + Gelir Vergisi: {incomeTaxSaving:N2} ₺)";
            }
            else
            {
                TxtScanTaxShield.Text = $"🛡️ Fatura Vergi Kalkanı: {shield:N2} ₺ (%{effectiveKdvRate} KDV: {kdvAmt:N2} ₺ + Gelir Vergisi: {incomeTaxSaving:N2} ₺)";
            }
        }
        else if (gross > 0 && !isCompany)
        {
            TxtScanTaxShield.Text = "🛡️ Fatura Vergi Kalkanı: ₺0,00 (Kişisel Harcamada Vergi Kalkanı Yoktur)";
        }
        else
        {
            TxtScanTaxShield.Text = "🛡️ Fatura Vergi Kalkanı: ₺0,00 (KDV + Gelir Vergisi Tasarrufu)";
        }

        // Live duplicate detection on every keystroke/selection
        CheckAndShowDuplicateWarning();
    }

    private void BtnSaveScannedExpense_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtScanTitle.Text))
        {
            MessageBox.Show("Lütfen fatura başlığı veya malzeme açıklaması girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseTurkishAmount(TxtScanGross.Text, out double gross) || gross <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir fatura tutarı girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check duplicate protection before saving
        var dup = CheckAndShowDuplicateWarning();
        if (dup.IsDuplicate)
        {
            var res = MessageBox.Show(
                $"⚠️ DİKKAT: MÜKERRER FATURA TESPİT EDİLDİ!\n\n" +
                $"• {dup.Reason}\n" +
                $"• Sistemde kayıtlı tarih: {dup.ExistingRecord?.InvoiceDate}\n" +
                $"• Sistemde kayıtlı tutar: {dup.ExistingRecord?.GrossAmount:N2} ₺\n\n" +
                $"Bu faturanın mükerrer olarak YİNE DE sisteme kaydedilmesini istiyor musunuz?",
                "Mükerrer Fatura Koruması",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes)
            {
                return;
            }
        }

        int kdvIndex = CmbScanKdv.SelectedIndex;
        int kdvRate;
        double baseAmt;
        double kdvAmt;
        string notes = "";

        if (kdvIndex == 4) // Karma KDV (Su Faturası)
        {
            kdvRate = 4;
            kdvAmt = Math.Round(gross * (264.15 / 6234.85), 2);
            baseAmt = Math.Round(gross - kdvAmt, 2);
            notes = "💧 Çoklu KDV (MUSKİ Su Faturası: %1 İçme Suyu + %10 Atıksu + %20 Katı Atık)";
        }
        else
        {
            kdvRate = kdvIndex switch
            {
                1 => 10,
                2 => 1,
                3 => 0,
                _ => 20
            };
            baseAmt = Math.Round(gross / (1.0 + (kdvRate / 100.0)), 2);
            kdvAmt = Math.Round(gross - baseAmt, 2);
        }

        string expType = CmbScanType.SelectedIndex == 0 ? "sirket_gideri" : "kisisel_harcama";
        string category = CmbScanCategory.SelectedIndex switch
        {
            0 => "faturalar",
            1 => "faturalar",
            2 => "faturalar",
            3 => "bilgisayar_laptop",
            4 => "tadilat_insaat",
            6 => "villa_esya_mobilya",
            7 => "havuz_ekipmani",
            8 => "ulasim_yakit",
            _ => "dogrudan_gider"
        };

        double shield = expType == "sirket_gideri" ? Math.Round((baseAmt * 0.20) + kdvAmt, 2) : 0;

        var exp = new Expense
        {
            Title = TxtScanTitle.Text.Trim(),
            ExpenseType = expType,
            Category = category,
            GrossAmount = gross,
            BaseAmount = baseAmt,
            KdvRate = kdvRate,
            KdvAmount = kdvAmt,
            TaxShieldAmount = shield,
            InvoiceDate = string.IsNullOrWhiteSpace(TxtScanDate.Text) ? DateTime.Today.ToString("yyyy-MM-dd") : TxtScanDate.Text.Trim(),
            VendorName = TxtScanVendor.Text.Trim(),
            InvoiceNo = TxtScanInvNo.Text.Trim(),
            Ettn = TxtScanInvNo.Text.Trim(),
            ReceiptFile = _currentScannedPath,
            Notes = notes
        };

        DatabaseService.Instance.AddExpense(exp);
        LoadAllData();
        MessageBox.Show($"'{exp.Title}' faturası {gross:N2} ₺ tutarla başarıyla kaydedildi ve vergi kalkanına eklendi!", "Fatura Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Expenses Tab
    private void ExpenseChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _activeExpenseChip = tag;
            ApplyExpenseFilter();
        }
    }

    private void TxtExpenseFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyExpenseFilter();
    }

    private void ApplyExpenseFilter()
    {
        string textFilter = TxtExpenseFilter?.Text?.Trim().ToLowerInvariant() ?? "";
        var query = _allExpenses.AsEnumerable();

        if (!string.IsNullOrEmpty(_activeExpenseChip))
        {
            if (_activeExpenseChip == "sirket_gideri")
                query = query.Where(e => e.IsCompanyExpense);
            else if (_activeExpenseChip == "kisisel_harcama")
                query = query.Where(e => !e.IsCompanyExpense);
            else
                query = query.Where(e => (e.Category != null && e.Category.ToLowerInvariant().Contains(_activeExpenseChip)) ||
                                         (e.Title != null && e.Title.ToLowerInvariant().Contains(_activeExpenseChip)));
        }

        if (!string.IsNullOrEmpty(textFilter))
        {
            query = query.Where(e => (e.Title != null && e.Title.ToLowerInvariant().Contains(textFilter)) ||
                                     (e.VendorName != null && e.VendorName.ToLowerInvariant().Contains(textFilter)) ||
                                     (e.InvoiceNo != null && e.InvoiceNo.ToLowerInvariant().Contains(textFilter)));
        }

        GridExpenses.ItemsSource = query.ToList();
    }

    private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
    {
        var win = new AddExpenseWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            LoadAllData();
        }
    }

    private void BtnDeleteExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long id)
        {
            var res = MessageBox.Show("Bu fatura kaydını silmek istediğinize emin misiniz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeleteExpense(id);
                LoadAllData();
            }
        }
    }
    #endregion

    #region Incomes Tab
    private void IncomeChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            _activeIncomeChip = tag;
            ApplyIncomeFilter();
        }
    }

    private void TxtIncomeFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyIncomeFilter();
    }

    private void ApplyIncomeFilter()
    {
        string textFilter = TxtIncomeFilter?.Text?.Trim().ToLowerInvariant() ?? "";
        var query = _allIncomes.AsEnumerable();

        if (!string.IsNullOrEmpty(_activeIncomeChip))
        {
            if (_activeIncomeChip == "vergisiz_kazanc")
                query = query.Where(i => i.IsTaxable == 0);
            else
                query = query.Where(i => i.SourceType != null && i.SourceType.ToLowerInvariant().Contains(_activeIncomeChip));
        }

        if (!string.IsNullOrEmpty(textFilter))
        {
            query = query.Where(i => (i.Title != null && i.Title.ToLowerInvariant().Contains(textFilter)) ||
                                     (i.ClientName != null && i.ClientName.ToLowerInvariant().Contains(textFilter)) ||
                                     (i.InvoiceNo != null && i.InvoiceNo.ToLowerInvariant().Contains(textFilter)));
        }

        GridIncomes.ItemsSource = query.ToList();
    }

    private void BtnAddIncome_Click(object sender, RoutedEventArgs e)
    {
        var win = new AddIncomeWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            LoadAllData();
        }
    }

    private void BtnDeleteIncome_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long id)
        {
            var res = MessageBox.Show("Bu gelir kaydını silmek istediğinize emin misiniz?", "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeleteIncome(id);
                LoadAllData();
            }
        }
    }
    #endregion

    #region Settings Tab & Actions
    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string compType = CmbCompanyType.SelectedIndex switch
            {
                1 => "limited",
                2 => "anonim",
                _ => "sahis"
            };
            double.TryParse(TxtFixedTaxRate.Text.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double ratePct);
            double fixedRate = ratePct > 0 ? ratePct / 100.0 : 0.20;
            int isYoung = ChkYoungEntrepreneur.IsChecked == true ? 1 : 0;

            var settings = new TaxSettings
            {
                CompanyType = compType,
                FixedIncomeTaxRate = fixedRate,
                IsYoungEntrepreneur = isYoung
            };

            DatabaseService.Instance.SaveTaxSettings(settings);
            LoadAllData();
            MessageBox.Show("Vergi ve şirket ayarları başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ayarlar kaydedilirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnToggleAccrualPaid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long id)
        {
            var accrual = _allAccruals.FirstOrDefault(a => a.Id == id);
            if (accrual != null)
            {
                bool newStatus = accrual.IsPaid == 0;
                DatabaseService.Instance.ToggleAccrualPaid(id, newStatus);
                LoadAllData();
            }
        }
    }

    private void Period_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Period filter handler
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadAllData();
    }

    private void BtnOpenSimulator_Click(object sender, RoutedEventArgs e)
    {
        var win = new SimulatorWindow { Owner = this };
        win.ShowDialog();
    }
    #endregion
}
