using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NHi.Inventory.Services;
using NHi.Inventory.Views.POS;
using NHi.Inventory.Views.Inventory;
using NHi.Inventory.Views.Khata;
using NHi.Inventory.Views.More;
using NHi.Inventory.Views.Reports;
using NHi.Inventory.Views.Settings;

namespace NHi.Inventory.Views;

public partial class DashboardView : UserControl
{
    private readonly ReportService _reportService = new();

    public DashboardView()
    {
        InitializeComponent();
        Loaded += DashboardView_Loaded;
    }

    private void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        LoadGreeting();
        LoadStats();
    }

    private void LoadGreeting()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? LanguageManager.GetString("GoodMorning") :
                       hour < 17 ? LanguageManager.GetString("GoodAfternoon") :
                       LanguageManager.GetString("GoodEvening");
        txtGreeting.Text = $"{greeting} 👋";
        txtDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
    }

    private void LoadStats()
    {
        var currency = "Rs.";
        txtTodaySales.Text = $"{currency} {_reportService.GetTodaySalesTotal():N0}";
        txtTodayProfit.Text = $"{currency} {_reportService.GetTodayProfit():N0}";
        txtReceivables.Text = $"{currency} {_reportService.GetTotalReceivables():N0}";
        txtPayables.Text = $"{currency} {_reportService.GetTotalPayables():N0}";
        txtTodayPurchases.Text = $"{currency} {_reportService.GetTodayPurchases():N0}";
        txtTodayExpenses.Text = $"{currency} {new ExpenseService().GetTodayExpenses():N0}";
        txtCurrentCash.Text = $"{currency} {_reportService.GetCurrentCash():N0}";
        txtLowStock.Text = _reportService.GetLowStockCount().ToString();
        txtTotalProducts.Text = _reportService.GetTotalProducts().ToString();
        txtTotalCustomers.Text = _reportService.GetTotalCustomers().ToString();
        txtTotalSuppliers.Text = _reportService.GetTotalSuppliers().ToString();

        LoadMonthlyStats(currency);
    }

    private void LoadMonthlyStats(string currency)
    {
        var thisMonth = DateTime.Today;
        var lastMonth = DateTime.Today.AddMonths(-1);

        var monthlySales = _reportService.GetMonthlySales(thisMonth);
        var monthlyPurchases = _reportService.GetMonthlyPurchases(thisMonth);
        var monthlyProfit = _reportService.GetMonthlyProfit(thisMonth);
        var monthlyExpenses = _reportService.GetMonthlyExpenses(thisMonth);
        var monthlyRecords = _reportService.GetMonthlyRecordsCount(thisMonth);

        var lastMonthSales = _reportService.GetMonthlySales(lastMonth);
        var lastMonthPurchases = _reportService.GetMonthlyPurchases(lastMonth);
        var lastMonthProfit = _reportService.GetMonthlyProfit(lastMonth);
        var lastMonthExpenses = _reportService.GetMonthlyExpenses(lastMonth);
        var lastMonthRecords = _reportService.GetMonthlyRecordsCount(lastMonth);

        txtMonthlySales.Text = $"{currency} {monthlySales:N0}";
        txtMonthlyPurchases.Text = $"{currency} {monthlyPurchases:N0}";
        txtMonthlyProfit.Text = $"{currency} {monthlyProfit:N0}";
        txtMonthlyExpenses.Text = $"{currency} {monthlyExpenses:N0}";
        txtMonthlyRecords.Text = monthlyRecords.ToString();

        SetTrend(iconMonthlySalesTrend, txtMonthlySalesTrend, monthlySales, lastMonthSales);
        SetTrend(iconMonthlyPurchasesTrend, txtMonthlyPurchasesTrend, monthlyPurchases, lastMonthPurchases);
        SetTrend(iconMonthlyProfitTrend, txtMonthlyProfitTrend, monthlyProfit, lastMonthProfit);
        // Expenses going up is a decline, not an improvement, so its trend colors are inverted.
        SetTrend(iconMonthlyExpensesTrend, txtMonthlyExpensesTrend, monthlyExpenses, lastMonthExpenses, invertColors: true);
        SetTrend(iconMonthlyRecordsTrend, txtMonthlyRecordsTrend, monthlyRecords, lastMonthRecords);
    }

    private void SetTrend(MaterialDesignThemes.Wpf.PackIcon icon, TextBlock text, decimal current, decimal previous, bool invertColors = false)
    {
        decimal change = previous == 0
            ? (current == 0 ? 0 : current > 0 ? 100 : -100)
            : (current - previous) / Math.Abs(previous) * 100;

        var isUp = change > 0.05m;
        var isDown = change < -0.05m;
        var isGood = invertColors ? isDown : isUp;
        var isBad = invertColors ? isUp : isDown;

        if (isUp)
        {
            icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.TrendingUp;
            text.Text = $"+{change:0.#}%";
        }
        else if (isDown)
        {
            icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.TrendingDown;
            text.Text = $"{change:0.#}%";
        }
        else
        {
            icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.TrendingNeutral;
            text.Text = "0%";
        }

        Brush brush;
        if (isGood) brush = (Brush)Application.Current.Resources["SuccessBrush"];
        else if (isBad) brush = (Brush)Application.Current.Resources["DangerBrush"];
        else { text.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush"); icon.SetResourceReference(MaterialDesignThemes.Wpf.PackIcon.ForegroundProperty, "TextTertiaryBrush"); return; }
        icon.Foreground = brush;
        text.Foreground = brush;
    }

    private void LowStock_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateTo("Inventory");
    }

    private void NavigateTo(string section)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavTo(section);
    }

    // ======================= Dashboard calculator =======================
    // A small immediate-execution calculator (like the Windows "Standard" mode).
    // Both the on-screen buttons and the physical keyboard route through HandleCalcKey.

    private string _calcEntry = "0";        // the number currently being typed / shown
    private double _calcAccumulator;        // the running left-hand value
    private char _calcPendingOp;            // '\0' = none, else + - * /
    private bool _calcJustEvaluated;        // last action was '=' — next digit starts fresh
    private bool _calcError;                // e.g. divide by zero — only 'C' recovers

    private void CalcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string t || t.Length == 0) return;
        HandleCalcKey(t == "back" ? '\b' : t[0]);
    }

    private void Dashboard_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't hijack typing when a real input control is focused.
        if (Keyboard.FocusedElement is TextBox or PasswordBox or ComboBox) return;

        var k = MapCalcKey(e.Key, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        if (k == '\0') return;
        HandleCalcKey(k);
        e.Handled = true;
    }

    private static char MapCalcKey(Key key, bool shift)
    {
        if (!shift && key is >= Key.D0 and <= Key.D9) return (char)('0' + (key - Key.D0));
        if (key is >= Key.NumPad0 and <= Key.NumPad9) return (char)('0' + (key - Key.NumPad0));
        return key switch
        {
            Key.Add => '+',
            Key.Subtract or Key.OemMinus => '-',
            Key.Multiply => '*',
            Key.Divide or Key.OemQuestion => '/',
            Key.OemPlus when shift => '+',
            Key.OemPlus or Key.Return or Key.Enter => '=',
            Key.Decimal or Key.OemPeriod => '.',
            Key.Back => '\b',
            Key.Escape or Key.Delete => 'C',
            Key.D5 when shift => '%',
            _ => '\0'
        };
    }

    private void HandleCalcKey(char k)
    {
        if (_calcError && k != 'C') return;

        switch (k)
        {
            case >= '0' and <= '9':
                if (_calcJustEvaluated) { _calcEntry = "0"; _calcJustEvaluated = false; }
                _calcEntry = _calcEntry == "0" ? k.ToString() : _calcEntry + k;
                break;

            case '.':
                if (_calcJustEvaluated) { _calcEntry = "0"; _calcJustEvaluated = false; }
                if (!_calcEntry.Contains('.')) _calcEntry += ".";
                break;

            case '\b':
                if (_calcJustEvaluated) break;
                _calcEntry = _calcEntry.Length > 1 ? _calcEntry[..^1] : "0";
                break;

            case 'C':
                _calcEntry = "0"; _calcAccumulator = 0; _calcPendingOp = '\0';
                _calcJustEvaluated = false; _calcError = false;
                break;

            case '%':
            {
                var v = ParseEntry();
                var basis = _calcPendingOp != '\0' ? _calcAccumulator : 1.0;
                _calcEntry = FormatNum(_calcPendingOp != '\0' ? basis * v / 100.0 : v / 100.0);
                _calcJustEvaluated = false;
                break;
            }

            case '+' or '-' or '*' or '/':
                if (_calcPendingOp != '\0' && !_calcJustEvaluated) ApplyPendingOp();
                else _calcAccumulator = ParseEntry();
                if (!_calcError)
                {
                    _calcPendingOp = k;
                    _calcJustEvaluated = false;
                    _calcEntry = "0";
                }
                break;

            case '=':
                if (_calcPendingOp != '\0')
                {
                    ApplyPendingOp();
                    _calcPendingOp = '\0';
                    _calcJustEvaluated = true;
                }
                break;
        }

        UpdateCalcDisplay();
    }

    private void ApplyPendingOp()
    {
        var rhs = ParseEntry();
        double res = _calcPendingOp switch
        {
            '+' => _calcAccumulator + rhs,
            '-' => _calcAccumulator - rhs,
            '*' => _calcAccumulator * rhs,
            '/' => rhs == 0 ? double.NaN : _calcAccumulator / rhs,
            _ => rhs
        };

        if (double.IsNaN(res) || double.IsInfinity(res))
        {
            _calcError = true;
            _calcEntry = "Error";
            _calcAccumulator = 0;
            _calcPendingOp = '\0';
            return;
        }

        _calcAccumulator = res;
        _calcEntry = FormatNum(res);
    }

    private double ParseEntry() =>
        double.TryParse(_calcEntry, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string FormatNum(double d) =>
        d.ToString("0.##########", CultureInfo.InvariantCulture);

    private static string OpSymbol(char op) => op switch
    {
        '+' => "+", '-' => "−", '*' => "×", '/' => "÷", _ => ""
    };

    private void UpdateCalcDisplay()
    {
        if (calcDisplay == null) return;   // during InitializeComponent

        calcDisplay.Text = _calcEntry;
        calcExpr.Text = !_calcError && _calcPendingOp != '\0' && !_calcJustEvaluated
            ? $"{FormatNum(_calcAccumulator)} {OpSymbol(_calcPendingOp)}"
            : "";
    }
}
