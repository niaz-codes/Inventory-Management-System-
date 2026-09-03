using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class EmployeeDetailView : Window
{
    private readonly int _employeeId;
    private readonly EmployeeService _service = new();
    private Employee? _employee;
    private bool _dataChanged;

    public EmployeeDetailView(int employeeId)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _employeeId = employeeId;

        LoadHeader();
        LoadData();
    }

    /// <summary>True if any action taken while this window was open changed the employee's data, so the caller knows to refresh its own list.</summary>
    public bool DataChanged => _dataChanged;

    private void LoadHeader()
    {
        _employee = _service.GetById(_employeeId);
        if (_employee == null) { Close(); return; }

        Title = $"{LanguageManager.GetString("EmployeeStatement")} - {_employee.Name}";
        txtName.Text = _employee.Name;

        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_employee.Position)) detailParts.Add(_employee.Position);
        if (!string.IsNullOrWhiteSpace(_employee.Phone)) detailParts.Add(_employee.Phone);
        if (!string.IsNullOrWhiteSpace(_employee.Address)) detailParts.Add(_employee.Address);
        txtDetails.Text = string.Join("   |   ", detailParts);

        var isMonthly = _employee.SalaryType == "Monthly";
        txtSalTypeBadge.Text = LanguageManager.GetString(isMonthly ? "MonthlySalaryOption" : "DailyWageOption");
        txtWageLine.Text = isMonthly
            ? $"{LanguageManager.GetString("MonthlySalary")}: Rs. {_employee.MonthlySalary:N0}"
            : $"{LanguageManager.GetString("DailyWage")}: Rs. {_employee.DailyWage:N0}";

        btnPostSalary.Visibility = isMonthly ? Visibility.Visible : Visibility.Collapsed;
        btnMarkWorkDay.Visibility = isMonthly ? Visibility.Collapsed : Visibility.Visible;

        var balance = _employee.CurrentBalance;
        txtBalance.Text = $"Rs. {Math.Abs(balance):N0}";

        var status = _service.GetWorkSummary(_employeeId).PaymentStatus;
        var (statusKey, badgeColor) = status switch
        {
            "Paid" => ("StatusPaid", "#4CAF50"),
            "Partial" => ("StatusPartiallyPaid", "#2196F3"),
            _ => ("StatusUnpaid", "#FF9800"),
        };
        txtStatusBadge.Text = LanguageManager.GetString(statusKey);
        badgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeColor));
        if (balance == 0)
            txtBalance.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        else
            txtBalance.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));
    }

    private void LoadData()
    {
        var attendance = _service.GetAttendance(_employeeId);
        var statusByAttendance = _service.GetDayPaymentStatus(_employeeId);

        dgAttendance.ItemsSource = attendance.Select(a =>
        {
            var status = statusByAttendance.GetValueOrDefault(a.Id, "Unpaid");
            return new AttendanceRow
            {
                Id = a.Id,
                WorkDate = a.WorkDate,
                Amount = a.Amount,
                Notes = a.Notes ?? "",
                Status = status,
                StatusText = LanguageManager.GetString(status switch
                {
                    "Paid" => "StatusPaid",
                    "Partial" => "StatusPartiallyPaid",
                    _ => "StatusUnpaid"
                })
            };
        }).ToList();

        dgLedger.ItemsSource = _service.GetLedger(_employeeId);

        var summary = _service.GetWorkSummary(_employeeId);
        if (summary.SalaryType == "Monthly")
        {
            lblCol0.Text = LanguageManager.GetString("MonthlySalary");
            txtWorkedDays.Text = $"Rs. {summary.MonthlySalary:N0}";
        }
        else
        {
            lblCol0.Text = LanguageManager.GetString("WorkedDays");
            txtWorkedDays.Text = summary.TotalWorkedDays.ToString();
        }
        txtToday.Text = $"Rs. {summary.TodayTotal:N0}";
        txtWeek.Text = $"Rs. {summary.WeekTotal:N0}";
        txtMonth.Text = $"Rs. {summary.MonthTotal:N0}";
        txtTotalPaid.Text = $"Rs. {summary.TotalPaid:N0}";
        txtRemaining.Text = $"Rs. {summary.RemainingBalance:N0}";
    }

    private void Refresh()
    {
        _dataChanged = true;
        LoadHeader();
        LoadData();
    }

    private void BtnMarkWorkDay_Click(object sender, RoutedEventArgs e)
    {
        if (_employee == null) return;
        var dialog = new MarkWorkDayDialog(_employeeId, _employee.DailyWage) { Owner = this };
        if (dialog.ShowDialog() == true) Refresh();
    }

    private void BtnPostSalary_Click(object sender, RoutedEventArgs e)
    {
        if (_employee == null) return;
        var dialog = new PostSalaryDialog(_employeeId, _employee.MonthlySalary) { Owner = this };
        if (dialog.ShowDialog() == true) Refresh();
    }

    private void BtnAddDue_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDueDialog(_employeeId) { Owner = this };
        if (dialog.ShowDialog() == true) Refresh();
    }

    private void BtnPay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Khata.PaymentDialog(_employeeId, "employee") { Owner = this };
        if (dialog.ShowDialog() == true) Refresh();
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_employee == null) return;
        var attendance = _service.GetAttendance(_employeeId);
        var statusByAttendance = _service.GetDayPaymentStatus(_employeeId);
        if (!ReceiptService.PrintEmployeeStatement(_employee, _service.GetLedger(_employeeId), attendance, statusByAttendance))
            MessageBox.Show(LanguageManager.GetString("ErrorOccurred"), LanguageManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_employee == null) return;
        var dialog = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Employee_{_employee.Name}.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            ExcelExportService.ExportEmployeeStatement(dialog.FileName, _employeeId);
            MessageBox.Show(LanguageManager.GetString("SavedSuccessfully"));
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_employee == null) return;
        var dialog = new EmployeeDialog(_employee) { Owner = this };
        if (dialog.ShowDialog() == true) Refresh();
    }
}

public class AttendanceRow
{
    public int Id { get; set; }
    public DateTime WorkDate { get; set; }
    public decimal Amount { get; set; }
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusText { get; set; } = "";
}

public class AttendanceStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = (value as string) switch
        {
            "Paid" => "#4CAF50",
            "Partial" => "#2196F3",
            _ => "#F44336"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
