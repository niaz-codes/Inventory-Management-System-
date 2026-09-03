using System.Windows;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.More;

public partial class MarkWorkDayDialog : Window
{
    private readonly int _employeeId;

    public MarkWorkDayDialog(int employeeId, decimal defaultDailyWage)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _employeeId = employeeId;
        dpDate.SelectedDate = DateTime.Today;
        txtAmount.Text = defaultDailyWage.ToString("0.##");
    }

    private void ChkRange_Changed(object sender, RoutedEventArgs e)
    {
        pnlToDate.Visibility = chkRange.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (dpDate.SelectedDate == null || !decimal.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show(LanguageManager.GetString("FillRequired"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fromDate = dpDate.SelectedDate.Value.Date;
        var toDate = chkRange.IsChecked == true && dpToDate.SelectedDate != null ? dpToDate.SelectedDate.Value.Date : fromDate;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var service = new EmployeeService();
        var marked = 0;
        var skipped = 0;
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            try
            {
                service.MarkWorkDay(_employeeId, day, amount, txtNotes.Text.Trim());
                marked++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        if (marked == 0)
        {
            MessageBox.Show(LanguageManager.GetString("DayAlreadyMarked"), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        this.DialogResult = true;
        this.Close();
    }
}
