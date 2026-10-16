using System;

namespace AlpFinans.Models;

public class DashboardSummary
{
    public double TotalGrossIncome { get; set; }
    public double TotalNetIncome { get; set; }
    public double TaxableIncomeGross { get; set; }
    public double TaxExemptIncomeGross { get; set; }
    public double TotalCompanyExpensesGross { get; set; }
    public double TotalCompanyExpensesBase { get; set; }
    public double TotalPersonalExpenses { get; set; }
    public double CalculatedKdv { get; set; }
    public double DeductibleKdv { get; set; }
    public double NetKdvBalance { get; set; }
    public double TaxableProfit { get; set; }
    public double EstimatedIncomeTax { get; set; }
    public double TaxShieldSaved { get; set; }
    public double TotalTaxLiability { get; set; }
    public double TotalPaidTaxes { get; set; }
    public double RemainingTaxToPay { get; set; }
    public double NetPocketProfit { get; set; }
    public double YoungEntrepreneurExemption { get; set; } = 230000.0;

    public string FormattedGrossIncome => $"{TotalGrossIncome:N2} ₺";
    public string FormattedNetIncome => $"{TotalNetIncome:N2} ₺";
    public string FormattedCompanyExpenses => $"{TotalCompanyExpensesGross:N2} ₺";
    public string FormattedPersonalExpenses => $"{TotalPersonalExpenses:N2} ₺";
    public string FormattedCalculatedKdv => $"{CalculatedKdv:N2} ₺";
    public string FormattedDeductibleKdv => $"{DeductibleKdv:N2} ₺";
    public string FormattedNetKdv => $"{Math.Abs(NetKdvBalance):N2} ₺";
    public string NetKdvStatusText => NetKdvBalance >= 0 ? "Ödenecek KDV" : "Devreden KDV";
    public bool IsKdvPayable => NetKdvBalance >= 0;
    public string FormattedTaxableProfit => $"{TaxableProfit:N2} ₺";
    public string FormattedEstimatedTax => $"{EstimatedIncomeTax:N2} ₺";
    public string FormattedTaxShield => $"{TaxShieldAmountOrFormula:N2} ₺";
    public double TaxShieldAmountOrFormula => TaxShieldSaved > 0 ? TaxShieldSaved : (TotalCompanyExpensesBase * 0.25);
    public string FormattedTotalTaxLiability => $"{TotalTaxLiability:N2} ₺";
    public string FormattedTotalPaidTaxes => $"{TotalPaidTaxes:N2} ₺";
    public string FormattedRemainingTaxToPay => $"{RemainingTaxToPay:N2} ₺";
    public string FormattedNetPocketProfit => $"{NetPocketProfit:N2} ₺";

    public double UnshieldedTax => TotalTaxLiability + TaxShieldAmountOrFormula;
    public double ProfitMargin => TotalGrossIncome > 0 ? (NetPocketProfit / TotalGrossIncome * 100.0) : 0.0;

    public string FormattedTaxableIncomeGross => $"{TaxableIncomeGross:N2} ₺";
    public string FormattedTaxExemptIncomeGross => $"{TaxExemptIncomeGross:N2} ₺";
    public string FormattedUnshieldedTax => $"{UnshieldedTax:N2} ₺";
    public string FormattedProfitMargin => $"%{ProfitMargin:F1}";

}
