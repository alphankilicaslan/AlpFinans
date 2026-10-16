using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using AlpFinans.Models;

namespace AlpFinans.Services;

public class DatabaseService
{
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    public string DbPath { get; private set; } = string.Empty;
    public string ConnectionString => $"Data Source={DbPath};";

    public DatabaseService()
    {
        InitializeDatabasePath();
        CreateSafetyBackup();
        EnsureTablesExist();
    }

    private void InitializeDatabasePath()
    {
        // 1. Check primary real live database location
        string primary = @"C:\Users\Alphan.MonsterAlphan\Desktop\AlpFinans\Veriler_ve_Faturalar\finans_veritabani.sqlite";
        if (File.Exists(primary))
        {
            DbPath = primary;
            return;
        }

        // 2. Fallbacks
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate1 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Veriler_ve_Faturalar", "finans_veritabani.sqlite"));
        if (File.Exists(candidate1))
        {
            DbPath = candidate1;
            return;
        }

        string candidate2 = Path.Combine(baseDir, "Veriler_ve_Faturalar", "finans_veritabani.sqlite");
        if (File.Exists(candidate2))
        {
            DbPath = candidate2;
            return;
        }

        // Default to primary and ensure directory exists
        string dir = Path.GetDirectoryName(primary)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        DbPath = primary;
    }

    private void CreateSafetyBackup()
    {
        try
        {
            if (!File.Exists(DbPath)) return;
            string dir = Path.GetDirectoryName(DbPath)!;
            string backupDir = Path.Combine(dir, "Otomatik_Guvenlik_Yedekleri");
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            string backupFile = Path.Combine(backupDir, $"finans_veritabani_autobackup_{DateTime.Now:yyyyMMdd_HHmmss}.sqlite");
            File.Copy(DbPath, backupFile, overwrite: false);
        }
        catch
        {
            // Do not block app start if backup file already exists or locked
        }
    }

    private void EnsureTablesExist()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS incomes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            source_type TEXT NOT NULL,
            gross_amount REAL NOT NULL,
            kdv_rate INTEGER DEFAULT 20,
            kdv_amount REAL DEFAULT 0.0,
            stopaj_rate INTEGER DEFAULT 0,
            stopaj_amount REAL DEFAULT 0.0,
            net_amount REAL NOT NULL,
            invoice_date TEXT NOT NULL,
            created_at TEXT DEFAULT (datetime('now', 'localtime')),
            client_name TEXT,
            invoice_no TEXT,
            is_paid INTEGER DEFAULT 1,
            is_taxable INTEGER DEFAULT 1,
            notes TEXT,
            ettn TEXT,
            currency TEXT DEFAULT 'TRY',
            original_amount REAL DEFAULT 0.0,
            exchange_rate REAL DEFAULT 1.0,
            group_name TEXT DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS expenses (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            category TEXT NOT NULL,
            expense_type TEXT NOT NULL,
            gross_amount REAL NOT NULL,
            base_amount REAL NOT NULL,
            kdv_rate INTEGER DEFAULT 20,
            kdv_amount REAL NOT NULL,
            deduction_ratio INTEGER DEFAULT 100,
            is_asset_demirbas INTEGER DEFAULT 0,
            asset_category TEXT,
            useful_years INTEGER DEFAULT 1,
            annual_depreciation_amount REAL DEFAULT 0.0,
            tax_shield_amount REAL DEFAULT 0.0,
            invoice_date TEXT NOT NULL,
            created_at TEXT DEFAULT (datetime('now', 'localtime')),
            vendor_name TEXT,
            invoice_no TEXT,
            receipt_file TEXT,
            notes TEXT,
            ettn TEXT,
            currency TEXT DEFAULT 'TRY',
            original_amount REAL DEFAULT 0.0,
            exchange_rate REAL DEFAULT 1.0,
            group_name TEXT DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS tax_settings (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            company_type TEXT DEFAULT 'sahis',
            fixed_income_tax_rate REAL DEFAULT 0.20,
            is_young_entrepreneur INTEGER DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS tax_payments (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            tax_type TEXT NOT NULL,
            amount REAL NOT NULL,
            payment_date TEXT NOT NULL,
            created_at TEXT DEFAULT (datetime('now', 'localtime')),
            receipt_no TEXT,
            notes TEXT
        );

        CREATE TABLE IF NOT EXISTS tax_accruals (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            tax_type TEXT NOT NULL,
            amount REAL NOT NULL,
            due_date TEXT NOT NULL,
            period TEXT,
            is_paid INTEGER DEFAULT 0,
            paid_date TEXT,
            receipt_file TEXT,
            notes TEXT,
            created_at TEXT DEFAULT (datetime('now', 'localtime')),
            belge_no TEXT,
            principal_amount REAL DEFAULT 0.0,
            interest_amount REAL DEFAULT 0.0
        );
        ";
        cmd.ExecuteNonQuery();
    }

    public List<Income> GetIncomes()
    {
        var list = new List<Income>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM incomes ORDER BY invoice_date DESC, id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Income
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Title = r["title"]?.ToString() ?? "",
                SourceType = r["source_type"]?.ToString() ?? "sirket_fatura",
                GrossAmount = Convert.ToDouble(r["gross_amount"]),
                KdvRate = Convert.ToInt32(r["kdv_rate"]),
                KdvAmount = Convert.ToDouble(r["kdv_amount"]),
                StopajRate = Convert.ToInt32(r["stopaj_rate"]),
                StopajAmount = Convert.ToDouble(r["stopaj_amount"]),
                NetAmount = Convert.ToDouble(r["net_amount"]),
                InvoiceDate = r["invoice_date"]?.ToString() ?? "",
                CreatedAt = r["created_at"]?.ToString() ?? "",
                ClientName = r["client_name"]?.ToString(),
                InvoiceNo = r["invoice_no"]?.ToString(),
                IsPaid = Convert.ToInt32(r["is_paid"]),
                IsTaxable = r.GetOrdinal("is_taxable") >= 0 && !r.IsDBNull(r.GetOrdinal("is_taxable")) ? Convert.ToInt32(r["is_taxable"]) : 1,
                Notes = r["notes"]?.ToString(),
                Ettn = r["ettn"]?.ToString(),
                Currency = r["currency"]?.ToString() ?? "TRY",
                OriginalAmount = Convert.ToDouble(r["original_amount"]),
                ExchangeRate = Convert.ToDouble(r["exchange_rate"]),
                GroupName = r["group_name"]?.ToString() ?? ""
            });
        }
        return list;
    }

    public void AddIncome(Income inc)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO incomes (
            title, source_type, gross_amount, kdv_rate, kdv_amount, stopaj_rate, stopaj_amount,
            net_amount, invoice_date, client_name, invoice_no, is_paid, is_taxable, notes,
            ettn, currency, original_amount, exchange_rate, group_name
        ) VALUES (
            @title, @source_type, @gross_amount, @kdv_rate, @kdv_amount, @stopaj_rate, @stopaj_amount,
            @net_amount, @invoice_date, @client_name, @invoice_no, @is_paid, @is_taxable, @notes,
            @ettn, @currency, @original_amount, @exchange_rate, @group_name
        )";
        cmd.Parameters.AddWithValue("@title", inc.Title);
        cmd.Parameters.AddWithValue("@source_type", inc.SourceType);
        cmd.Parameters.AddWithValue("@gross_amount", inc.GrossAmount);
        cmd.Parameters.AddWithValue("@kdv_rate", inc.KdvRate);
        cmd.Parameters.AddWithValue("@kdv_amount", inc.KdvAmount);
        cmd.Parameters.AddWithValue("@stopaj_rate", inc.StopajRate);
        cmd.Parameters.AddWithValue("@stopaj_amount", inc.StopajAmount);
        cmd.Parameters.AddWithValue("@net_amount", inc.NetAmount);
        cmd.Parameters.AddWithValue("@invoice_date", inc.InvoiceDate);
        cmd.Parameters.AddWithValue("@client_name", (object?)inc.ClientName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@invoice_no", (object?)inc.InvoiceNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_paid", inc.IsPaid);
        cmd.Parameters.AddWithValue("@is_taxable", inc.IsTaxable);
        cmd.Parameters.AddWithValue("@notes", (object?)inc.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ettn", (object?)inc.Ettn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@currency", inc.Currency);
        cmd.Parameters.AddWithValue("@original_amount", inc.OriginalAmount > 0 ? inc.OriginalAmount : inc.GrossAmount);
        cmd.Parameters.AddWithValue("@exchange_rate", inc.ExchangeRate > 0 ? inc.ExchangeRate : 1.0);
        cmd.Parameters.AddWithValue("@group_name", inc.GroupName);
        cmd.ExecuteNonQuery();
    }

    public void DeleteIncome(long id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM incomes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<Expense> GetExpenses()
    {
        var list = new List<Expense>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM expenses ORDER BY invoice_date DESC, id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Expense
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Title = r["title"]?.ToString() ?? "",
                Category = r["category"]?.ToString() ?? "dogrudan_gider",
                ExpenseType = r["expense_type"]?.ToString() ?? "sirket_gideri",
                GrossAmount = Convert.ToDouble(r["gross_amount"]),
                BaseAmount = Convert.ToDouble(r["base_amount"]),
                KdvRate = Convert.ToInt32(r["kdv_rate"]),
                KdvAmount = Convert.ToDouble(r["kdv_amount"]),
                DeductionRatio = Convert.ToInt32(r["deduction_ratio"]),
                IsAssetDemirbas = Convert.ToInt32(r["is_asset_demirbas"]),
                AssetCategory = r["asset_category"]?.ToString(),
                UsefulYears = Convert.ToInt32(r["useful_years"]),
                AnnualDepreciationAmount = Convert.ToDouble(r["annual_depreciation_amount"]),
                TaxShieldAmount = Convert.ToDouble(r["tax_shield_amount"]),
                InvoiceDate = r["invoice_date"]?.ToString() ?? "",
                CreatedAt = r["created_at"]?.ToString() ?? "",
                VendorName = r["vendor_name"]?.ToString(),
                InvoiceNo = r["invoice_no"]?.ToString(),
                ReceiptFile = r["receipt_file"]?.ToString(),
                Notes = r["notes"]?.ToString(),
                Ettn = r["ettn"]?.ToString(),
                Currency = r["currency"]?.ToString() ?? "TRY",
                OriginalAmount = Convert.ToDouble(r["original_amount"]),
                ExchangeRate = Convert.ToDouble(r["exchange_rate"]),
                GroupName = r["group_name"]?.ToString() ?? ""
            });
        }
        return list;
    }

    public void AddExpense(Expense exp)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO expenses (
            title, category, expense_type, gross_amount, base_amount, kdv_rate, kdv_amount,
            deduction_ratio, is_asset_demirbas, asset_category, useful_years, annual_depreciation_amount,
            tax_shield_amount, invoice_date, vendor_name, invoice_no, receipt_file, notes,
            ettn, currency, original_amount, exchange_rate, group_name
        ) VALUES (
            @title, @category, @expense_type, @gross_amount, @base_amount, @kdv_rate, @kdv_amount,
            @deduction_ratio, @is_asset_demirbas, @asset_category, @useful_years, @annual_depreciation_amount,
            @tax_shield_amount, @invoice_date, @vendor_name, @invoice_no, @receipt_file, @notes,
            @ettn, @currency, @original_amount, @exchange_rate, @group_name
        )";
        cmd.Parameters.AddWithValue("@title", exp.Title);
        cmd.Parameters.AddWithValue("@category", exp.Category);
        cmd.Parameters.AddWithValue("@expense_type", exp.ExpenseType);
        cmd.Parameters.AddWithValue("@gross_amount", exp.GrossAmount);
        cmd.Parameters.AddWithValue("@base_amount", exp.BaseAmount);
        cmd.Parameters.AddWithValue("@kdv_rate", exp.KdvRate);
        cmd.Parameters.AddWithValue("@kdv_amount", exp.KdvAmount);
        cmd.Parameters.AddWithValue("@deduction_ratio", exp.DeductionRatio);
        cmd.Parameters.AddWithValue("@is_asset_demirbas", exp.IsAssetDemirbas);
        cmd.Parameters.AddWithValue("@asset_category", (object?)exp.AssetCategory ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@useful_years", exp.UsefulYears);
        cmd.Parameters.AddWithValue("@annual_depreciation_amount", exp.AnnualDepreciationAmount);
        cmd.Parameters.AddWithValue("@tax_shield_amount", exp.TaxShieldAmount);
        cmd.Parameters.AddWithValue("@invoice_date", exp.InvoiceDate);
        cmd.Parameters.AddWithValue("@vendor_name", (object?)exp.VendorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@invoice_no", (object?)exp.InvoiceNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@receipt_file", (object?)exp.ReceiptFile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", (object?)exp.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ettn", (object?)exp.Ettn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@currency", exp.Currency);
        cmd.Parameters.AddWithValue("@original_amount", exp.OriginalAmount > 0 ? exp.OriginalAmount : exp.GrossAmount);
        cmd.Parameters.AddWithValue("@exchange_rate", exp.ExchangeRate > 0 ? exp.ExchangeRate : 1.0);
        cmd.Parameters.AddWithValue("@group_name", exp.GroupName);
        cmd.ExecuteNonQuery();
    }

    public void DeleteExpense(long id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM expenses WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public (bool IsDuplicate, string Reason, Expense? ExistingRecord) CheckDuplicateInvoice(
        string? invoiceNo,
        string? ettn,
        string? vendor,
        double gross,
        string? dateStr,
        string? category)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        // 1. Exact Invoice No Match
        if (!string.IsNullOrWhiteSpace(invoiceNo))
        {
            string inv = invoiceNo.Trim();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM expenses WHERE UPPER(TRIM(invoice_no)) = UPPER(@inv) LIMIT 1";
            cmd.Parameters.AddWithValue("@inv", inv);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                var existing = MapExpenseFromReader(r);
                return (true, $"Fatura No ({inv}) sistemde zaten kayıtlı!", existing);
            }
        }

        // 2. Exact ETTN Match
        if (!string.IsNullOrWhiteSpace(ettn))
        {
            string e = ettn.Trim();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM expenses WHERE UPPER(TRIM(ettn)) = UPPER(@ettn) LIMIT 1";
            cmd.Parameters.AddWithValue("@ettn", e);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                var existing = MapExpenseFromReader(r);
                return (true, $"ETTN Kodu ({e}) sistemde zaten kayıtlı!", existing);
            }
        }

        // 3. Vendor + Date + Amount Match
        if (!string.IsNullOrWhiteSpace(vendor) && gross > 0 && !string.IsNullOrWhiteSpace(dateStr))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM expenses 
                WHERE UPPER(TRIM(vendor_name)) = UPPER(TRIM(@vendor))
                  AND invoice_date = @date
                  AND ABS(gross_amount - @gross) < 0.05
                LIMIT 1";
            cmd.Parameters.AddWithValue("@vendor", vendor.Trim());
            cmd.Parameters.AddWithValue("@date", dateStr.Trim());
            cmd.Parameters.AddWithValue("@gross", gross);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                var existing = MapExpenseFromReader(r);
                return (true, $"{vendor} firmasına ait {dateStr} tarihli ₺{gross:N2} tutarındaki fatura zaten kayıtlı!", existing);
            }
        }

        // 4. Utility Bill (Elektrik, Su, vb.) Same Month + Amount Match
        if (!string.IsNullOrWhiteSpace(vendor) && gross > 0 && !string.IsNullOrWhiteSpace(dateStr) && dateStr.Length >= 7)
        {
            string vLower = vendor.ToLowerInvariant();
            bool isUtility = category == "faturalar" || vLower.Contains("aydem") || vLower.Contains("enerjisa") || vLower.Contains("muski") || vLower.Contains("su") || vLower.Contains("telekom");
            if (isUtility)
            {
                string monthStr = dateStr.Substring(0, 7);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT * FROM expenses 
                    WHERE (UPPER(TRIM(vendor_name)) = UPPER(TRIM(@vendor)) OR category = 'faturalar')
                      AND strftime('%Y-%m', invoice_date) = @month
                      AND ABS(gross_amount - @gross) < 0.05
                    LIMIT 1";
                cmd.Parameters.AddWithValue("@vendor", vendor.Trim());
                cmd.Parameters.AddWithValue("@month", monthStr);
                cmd.Parameters.AddWithValue("@gross", gross);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    var existing = MapExpenseFromReader(r);
                    return (true, $"{vendor} firmasına ait {monthStr} dönemi faturası (₺{gross:N2}) bu ay için zaten kayıtlı!", existing);
                }
            }
        }

        return (false, string.Empty, null);
    }

    private Expense MapExpenseFromReader(SqliteDataReader r)
    {
        return new Expense
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r["title"]?.ToString() ?? "",
            Category = r["category"]?.ToString() ?? "dogrudan_gider",
            ExpenseType = r["expense_type"]?.ToString() ?? "sirket_gideri",
            GrossAmount = Convert.ToDouble(r["gross_amount"]),
            BaseAmount = Convert.ToDouble(r["base_amount"]),
            KdvRate = Convert.ToInt32(r["kdv_rate"]),
            KdvAmount = Convert.ToDouble(r["kdv_amount"]),
            InvoiceDate = r["invoice_date"]?.ToString() ?? "",
            VendorName = r["vendor_name"]?.ToString(),
            InvoiceNo = r["invoice_no"]?.ToString(),
            Ettn = r["ettn"]?.ToString()
        };
    }

    public List<TaxPayment> GetTaxPayments()
    {
        var list = new List<TaxPayment>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tax_payments ORDER BY payment_date DESC, id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new TaxPayment
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Title = r["title"]?.ToString() ?? "",
                TaxType = r["tax_type"]?.ToString() ?? "kdv",
                Amount = Convert.ToDouble(r["amount"]),
                PaymentDate = r["payment_date"]?.ToString() ?? "",
                CreatedAt = r["created_at"]?.ToString() ?? "",
                ReceiptNo = r["receipt_no"]?.ToString(),
                Notes = r["notes"]?.ToString()
            });
        }
        return list;
    }

    public void AddTaxPayment(TaxPayment pay)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        INSERT INTO tax_payments (title, tax_type, amount, payment_date, receipt_no, notes)
        VALUES (@title, @tax_type, @amount, @payment_date, @receipt_no, @notes)";
        cmd.Parameters.AddWithValue("@title", pay.Title);
        cmd.Parameters.AddWithValue("@tax_type", pay.TaxType);
        cmd.Parameters.AddWithValue("@amount", pay.Amount);
        cmd.Parameters.AddWithValue("@payment_date", pay.PaymentDate);
        cmd.Parameters.AddWithValue("@receipt_no", (object?)pay.ReceiptNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@notes", (object?)pay.Notes ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void DeleteTaxPayment(long id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tax_payments WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<TaxAccrual> GetTaxAccruals()
    {
        var list = new List<TaxAccrual>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tax_accruals ORDER BY due_date DESC, id DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new TaxAccrual
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Title = r["title"]?.ToString() ?? "",
                TaxType = r["tax_type"]?.ToString() ?? "damga_vergisi",
                Amount = Convert.ToDouble(r["amount"]),
                DueDate = r["due_date"]?.ToString() ?? "",
                Period = r["period"]?.ToString(),
                IsPaid = Convert.ToInt32(r["is_paid"]),
                PaidDate = r["paid_date"]?.ToString(),
                ReceiptFile = r["receipt_file"]?.ToString(),
                Notes = r["notes"]?.ToString(),
                CreatedAt = r["created_at"]?.ToString() ?? "",
                BelgeNo = r["belge_no"]?.ToString(),
                PrincipalAmount = Convert.ToDouble(r["principal_amount"]),
                InterestAmount = Convert.ToDouble(r["interest_amount"])
            });
        }
        return list;
    }

    public void ToggleAccrualPaid(long id, bool isPaid)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tax_accruals SET is_paid = @is_paid, paid_date = @paid_date WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@is_paid", isPaid ? 1 : 0);
        cmd.Parameters.AddWithValue("@paid_date", isPaid ? DateTime.Today.ToString("yyyy-MM-dd") : DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public TaxSettings GetTaxSettings()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tax_settings LIMIT 1";
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            return new TaxSettings
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                CompanyType = r["company_type"]?.ToString() ?? "sahis",
                FixedIncomeTaxRate = Convert.ToDouble(r["fixed_income_tax_rate"]),
                IsYoungEntrepreneur = r.GetOrdinal("is_young_entrepreneur") >= 0 && !r.IsDBNull(r.GetOrdinal("is_young_entrepreneur"))
                    ? Convert.ToInt32(r["is_young_entrepreneur"]) : 1
            };
        }
        return new TaxSettings();
    }

    public void SaveTaxSettings(TaxSettings s)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        UPDATE tax_settings SET
            company_type = @company_type,
            fixed_income_tax_rate = @fixed_income_tax_rate,
            is_young_entrepreneur = @is_young_entrepreneur
        WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", s.Id);
        cmd.Parameters.AddWithValue("@company_type", s.CompanyType);
        cmd.Parameters.AddWithValue("@fixed_income_tax_rate", s.FixedIncomeTaxRate);
        cmd.Parameters.AddWithValue("@is_young_entrepreneur", s.IsYoungEntrepreneur);
        if (cmd.ExecuteNonQuery() == 0)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = "INSERT INTO tax_settings (company_type, fixed_income_tax_rate, is_young_entrepreneur) VALUES (@company_type, @fixed_income_tax_rate, @is_young_entrepreneur)";
            insertCmd.Parameters.AddWithValue("@company_type", s.CompanyType);
            insertCmd.Parameters.AddWithValue("@fixed_income_tax_rate", s.FixedIncomeTaxRate);
            insertCmd.Parameters.AddWithValue("@is_young_entrepreneur", s.IsYoungEntrepreneur);
            insertCmd.ExecuteNonQuery();
        }
    }

    public DashboardSummary CalculateSummary()
    {
        var summary = new DashboardSummary();
        var incomes = GetIncomes();
        var expenses = GetExpenses();
        var accruals = GetTaxAccruals();
        var payments = GetTaxPayments();
        var settings = GetTaxSettings();

        double taxableGross = 0;
        double taxableKdv = 0;
        double taxableStopaj = 0;
        double taxableNet = 0;
        double exemptGross = 0;

        foreach (var inc in incomes)
        {
            summary.TotalGrossIncome += inc.GrossAmount;
            summary.TotalNetIncome += inc.NetAmount;
            if (inc.IsTaxable == 1)
            {
                taxableGross += inc.GrossAmount;
                taxableKdv += inc.KdvAmount;
                taxableStopaj += inc.StopajAmount;
                taxableNet += inc.NetAmount;
                summary.CalculatedKdv += inc.KdvAmount;
            }
            else
            {
                exemptGross += inc.GrossAmount;
            }
        }
        summary.TaxableIncomeGross = taxableGross;
        summary.TaxExemptIncomeGross = exemptGross;

        double directExpBase = 0;
        double directExpKdv = 0;
        double directExpShield = 0;
        double assetExpGross = 0;
        double assetExpKdv = 0;

        foreach (var exp in expenses)
        {
            if (exp.IsCompanyExpense)
            {
                summary.TotalCompanyExpensesGross += exp.GrossAmount;
                double usableRatio = (exp.DeductionRatio > 0 ? exp.DeductionRatio : 100) / 100.0;

                if (exp.IsAssetDemirbas == 1)
                {
                    assetExpGross += exp.GrossAmount;
                    assetExpKdv += exp.KdvAmount * usableRatio;
                }
                else
                {
                    directExpBase += exp.BaseAmount * usableRatio;
                    directExpKdv += exp.KdvAmount * usableRatio;
                    directExpShield += exp.TaxShieldAmount > 0 ? exp.TaxShieldAmount : (exp.BaseAmount * usableRatio * (settings.FixedIncomeTaxRate > 0 ? settings.FixedIncomeTaxRate : 0.20));
                }

                summary.TotalCompanyExpensesBase += exp.BaseAmount * usableRatio;
                summary.DeductibleKdv += exp.KdvAmount * usableRatio;
                summary.TaxShieldSaved += exp.TaxShieldAmount > 0 ? exp.TaxShieldAmount : (exp.BaseAmount * usableRatio * 0.25);
            }
            else
            {
                summary.TotalPersonalExpenses += exp.GrossAmount;
            }
        }

        // KDV Dengesi
        double totalDeductibleKdv = directExpKdv + assetExpKdv;
        double kdvDiff = taxableKdv - totalDeductibleKdv;
        double payableKdv = Math.Max(0, kdvDiff);
        summary.NetKdvBalance = kdvDiff;

        // Gelir / Matrah Hesaplama
        double incomeBase = taxableGross - taxableKdv;
        double taxableProfit = Math.Max(0, incomeBase - directExpBase);
        summary.TaxableProfit = taxableProfit;

        // Genç Girişimci İstisnası (230,000 TL)
        double taxableAfterExemption = taxableProfit;
        if (settings.IsYoungEntrepreneur == 1)
        {
            taxableAfterExemption = Math.Max(0, taxableProfit - summary.YoungEntrepreneurExemption);
        }

        // Vergi Hesaplama
        double taxRate = settings.FixedIncomeTaxRate > 0 ? settings.FixedIncomeTaxRate : 0.20;
        summary.EstimatedIncomeTax = taxableAfterExemption * taxRate;
        double remainingIncomeTax = Math.Max(0, summary.EstimatedIncomeTax - taxableStopaj);

        // Resmi Tahakkuklar
        double officialKdvAccruals = 0;
        double officialGeciciAccruals = 0;
        double damgaAccruals = 0;
        double otherTaxAccruals = 0;

        foreach (var a in accruals)
        {
            if (a.TaxType == "kdv") officialKdvAccruals += a.Amount;
            else if (a.TaxType == "gecici_vergi" || a.TaxType == "gelir_vergisi") officialGeciciAccruals += a.Amount;
            else if (a.TaxType == "damga_vergisi" || a.TaxType == "harc") damgaAccruals += a.Amount;
            else if (a.TaxType == "konaklama_vergisi" || a.TaxType == "turizm_payi" || a.TaxType == "diger") otherTaxAccruals += a.Amount;
        }

        double totalDamgaAccruals = damgaAccruals + otherTaxAccruals;
        double finalKdv = Math.Max(payableKdv, officialKdvAccruals);
        double finalIncomeTax = Math.Max(remainingIncomeTax, officialGeciciAccruals);
        double totalTaxLiability = finalKdv + finalIncomeTax + totalDamgaAccruals;

        // Ödenen Vergiler & Kalan Borç
        double totalPaidTaxes = 0;
        foreach (var p in payments)
        {
            totalPaidTaxes += p.Amount;
        }
        summary.TotalPaidTaxes = totalPaidTaxes;
        summary.TotalTaxLiability = totalTaxLiability;
        summary.RemainingTaxToPay = Math.Max(0, totalTaxLiability - totalPaidTaxes);

        // Net Cebe Kalan Kâr / Kasa:
        // ŞİRKET GİDERİ DÜŞÜLMEZ (Ana Python uygulamasının birebir aynısı):
        // net_pocket_profit = max(0.0, total_income_net - total_tax_liability - total_personal_expenses)
        double totalIncomeNet = taxableNet + exemptGross;
        summary.NetPocketProfit = Math.Max(0.0, totalIncomeNet - totalTaxLiability - summary.TotalPersonalExpenses);

        return summary;
    }

    private static double CalculateProgressiveTax(double matrah)
    {
        if (matrah <= 0) return 0;
        if (matrah <= 158000) return matrah * 0.15;
        if (matrah <= 330000) return 23700 + (matrah - 158000) * 0.20;
        if (matrah <= 800000) return 58100 + (matrah - 330000) * 0.27;
        if (matrah <= 4300000) return 185000 + (matrah - 800000) * 0.35;
        return 1410000 + (matrah - 4300000) * 0.40;
    }
}
