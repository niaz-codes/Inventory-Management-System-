using NHi.Inventory.Data;
using NHi.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace NHi.Inventory.Services;

public class EmployeeService
{
    public List<Employee> GetAll(bool activeOnly = true)
    {
        using var db = new AppDbContext();
        var query = db.Employees.AsQueryable();
        if (activeOnly) query = query.Where(e => e.IsActive);
        return query.OrderBy(e => e.Name).ToList();
    }

    public Employee? GetById(int id)
    {
        using var db = new AppDbContext();
        return db.Employees.Find(id);
    }

    public int Save(Employee employee)
    {
        using var db = new AppDbContext();
        if (employee.Id == 0) { employee.ShopId = AuthService.CurrentShopId ?? 1; employee.CreatedAt = DateTime.Now; db.Employees.Add(employee); }
        else db.Employees.Update(employee);
        db.SaveChanges();
        AuditLogService.Log(employee.Id == 0 ? "ADD_EMPLOYEE" : "UPDATE_EMPLOYEE", "Employee", employee.Id, employee.Name);
        return employee.Id;
    }

    public bool Delete(int id)
    {
        using var db = new AppDbContext();
        var employee = db.Employees.Find(id);
        if (employee != null)
        {
            employee.IsActive = false;
            db.SaveChanges();
            AuditLogService.Log("DELETE_EMPLOYEE", "Employee", id, employee.Name);
            return true;
        }
        return false;
    }

    public List<EmployeeLedger> GetLedger(int employeeId)
    {
        using var db = new AppDbContext();
        return db.EmployeeLedgers.Where(l => l.EmployeeId == employeeId).OrderBy(l => l.TransactionDate).ToList();
    }

    public List<Employee> Search(string searchText)
    {
        using var db = new AppDbContext();
        var text = searchText.ToLower();
        return db.Employees.Where(e => e.IsActive && (
                e.Name.ToLower().Contains(text) ||
                (e.Phone != null && e.Phone.Contains(searchText)) ||
                (e.Position != null && e.Position.ToLower().Contains(text))))
            .OrderBy(e => e.Name).ToList();
    }

    /// <summary>Records that the employee worked a given calendar day, crediting that day's
    /// wage (or an override amount) to their balance. One entry per employee per day.</summary>
    public int MarkWorkDay(int employeeId, DateTime date, decimal? amountOverride = null, string? notes = null)
    {
        var day = date.Date;
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            if (db.EmployeeAttendances.Any(a => a.EmployeeId == employeeId && a.WorkDate == day))
                throw new InvalidOperationException("This day is already marked as worked for this employee.");

            var amount = amountOverride ?? employee.DailyWage;

            var attendance = new EmployeeAttendance
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                WorkDate = day,
                Amount = amount,
                Notes = notes
            };
            db.EmployeeAttendances.Add(attendance);
            db.SaveChanges();

            employee.CurrentBalance += amount;
            var ledger = new EmployeeLedger
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                TransactionDate = day,
                Description = $"Daily Wage - {day:dd-MMM-yyyy}",
                Debit = amount,
                Credit = 0,
                Balance = employee.CurrentBalance,
                ReferenceId = attendance.Id,
                ReferenceType = "DailyWage"
            };
            db.EmployeeLedgers.Add(ledger);
            db.SaveChanges();

            attendance.LedgerId = ledger.Id;
            db.SaveChanges();

            transaction.Commit();
            AuditLogService.Log("MARK_WORK_DAY", "Employee", employeeId, $"{employee.Name} worked {day:dd-MMM-yyyy} - Rs. {amount:N0}");
            return attendance.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public List<EmployeeAttendance> GetAttendance(int employeeId, DateTime? from = null, DateTime? to = null)
    {
        using var db = new AppDbContext();
        var query = db.EmployeeAttendances.Where(a => a.EmployeeId == employeeId);
        if (from.HasValue) query = query.Where(a => a.WorkDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(a => a.WorkDate <= to.Value.Date);
        return query.OrderByDescending(a => a.WorkDate).ToList();
    }

    public EmployeeWorkSummary GetWorkSummary(int employeeId)
    {
        using var db = new AppDbContext();
        var employee = db.Employees.Find(employeeId);
        var attendance = db.EmployeeAttendances.Where(a => a.EmployeeId == employeeId).ToList();
        var ledger = db.EmployeeLedgers.Where(l => l.EmployeeId == employeeId).ToList();
        var paid = db.EmployeePayments.Where(p => p.EmployeeId == employeeId).ToList().Sum(p => p.Amount);

        var today = DateTime.Today;
        // Week = Monday..Sunday containing today.
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        var salaryType = employee?.SalaryType ?? "Daily";
        var monthlySalary = employee?.MonthlySalary ?? 0;

        decimal todayTotal, weekTotal, monthTotal;
        if (salaryType == "Monthly")
        {
            // Pro-rata split of the fixed monthly salary.
            monthTotal = monthlySalary;
            todayTotal = daysInMonth > 0 ? Math.Round(monthlySalary / daysInMonth) : 0;
            weekTotal = daysInMonth > 0 ? Math.Round(monthlySalary * 7m / daysInMonth) : 0;
        }
        else
        {
            todayTotal = attendance.Where(a => a.WorkDate.Date == today).Sum(a => a.Amount);
            weekTotal = attendance.Where(a => a.WorkDate.Date >= weekStart && a.WorkDate.Date <= weekEnd).Sum(a => a.Amount);
            monthTotal = attendance.Where(a => a.WorkDate.Date >= monthStart && a.WorkDate.Date <= monthEnd).Sum(a => a.Amount);
        }

        var totalDebit = ledger.Sum(l => l.Debit);
        var totalCredit = ledger.Sum(l => l.Credit);
        var status = totalDebit <= 0 ? "Paid"
                   : totalCredit <= 0 ? "Unpaid"
                   : totalCredit >= totalDebit ? "Paid"
                   : "Partial";

        var monthTag = today.Year * 100 + today.Month;

        return new EmployeeWorkSummary
        {
            TotalWorkedDays = attendance.Count,
            TotalPayable = totalDebit,
            TotalPaid = paid,
            RemainingBalance = employee?.CurrentBalance ?? 0,
            SalaryType = salaryType,
            DailyWage = employee?.DailyWage ?? 0,
            MonthlySalary = monthlySalary,
            TodayTotal = todayTotal,
            WeekTotal = weekTotal,
            MonthTotal = monthTotal,
            PaymentStatus = status,
            CurrentMonthSalaryPosted = ledger.Any(l => l.ReferenceType == "MonthlySalary" && l.ReferenceId == monthTag)
        };
    }

    /// <summary>
    /// Accrues a fixed monthly salary for the given period as a due on the employee ledger.
    /// Guarded so the same month can't be posted twice. Payments then settle it FIFO like
    /// any other due, so partial salary payments and payment history work automatically.
    /// </summary>
    public int PostMonthlySalary(int employeeId, int year, int month, decimal? amountOverride = null, string? notes = null)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            var tag = year * 100 + month;
            if (db.EmployeeLedgers.Any(l => l.EmployeeId == employeeId && l.ReferenceType == "MonthlySalary" && l.ReferenceId == tag))
                throw new InvalidOperationException("Salary for this month has already been posted.");

            var period = new DateTime(year, month, 1);
            var amount = amountOverride ?? employee.MonthlySalary;
            if (amount <= 0) throw new InvalidOperationException("Monthly salary amount must be greater than zero.");

            employee.CurrentBalance += amount;
            var entry = new EmployeeLedger
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                TransactionDate = DateTime.Now,
                Description = string.IsNullOrWhiteSpace(notes)
                    ? $"Salary - {period:MMMM yyyy}"
                    : $"Salary - {period:MMMM yyyy} ({notes})",
                Debit = amount,
                Credit = 0,
                Balance = employee.CurrentBalance,
                ReferenceId = tag,
                ReferenceType = "MonthlySalary"
            };
            db.EmployeeLedgers.Add(entry);
            db.SaveChanges();
            transaction.Commit();

            AuditLogService.Log("POST_MONTHLY_SALARY", "Employee", employeeId,
                $"{employee.Name} salary for {period:MMMM yyyy} - Rs. {amount:N0}");
            return entry.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>
    /// Per-day paid/partial/unpaid status for the employee's worked days. Walks the full
    /// ledger chronologically and applies payments FIFO against debits (same allocation
    /// idea as CustomerService.CalculateOverdue), then reports the leftover-unpaid amount
    /// against each day's originating "DailyWage" ledger entry. Read-only — never writes.
    /// </summary>
    public Dictionary<int, string> GetDayPaymentStatus(int employeeId)
    {
        using var db = new AppDbContext();
        var ledger = db.EmployeeLedgers.Where(l => l.EmployeeId == employeeId)
            .OrderBy(l => l.TransactionDate).ThenBy(l => l.Id).ToList();

        var openDebits = new List<(int? AttendanceId, decimal Original, decimal Remaining)>();
        decimal creditPool = 0;

        foreach (var e in ledger)
        {
            if (e.Debit > 0)
            {
                var remaining = e.Debit;
                if (creditPool > 0)
                {
                    var applied = Math.Min(creditPool, remaining);
                    creditPool -= applied;
                    remaining -= applied;
                }
                openDebits.Add((e.ReferenceType == "DailyWage" ? e.ReferenceId : null, e.Debit, remaining));
            }
            else if (e.Credit > 0)
            {
                var toApply = e.Credit;
                for (int i = 0; i < openDebits.Count && toApply > 0; i++)
                {
                    var (attendanceId, original, remaining) = openDebits[i];
                    if (remaining <= 0) continue;
                    var applied = Math.Min(remaining, toApply);
                    remaining -= applied;
                    toApply -= applied;
                    openDebits[i] = (attendanceId, original, remaining);
                }
                if (toApply > 0) creditPool += toApply;
            }
        }

        var result = new Dictionary<int, string>();
        foreach (var (attendanceId, original, remaining) in openDebits)
        {
            if (attendanceId == null) continue;
            result[attendanceId.Value] = remaining <= 0 ? "Paid" : remaining < original ? "Partial" : "Unpaid";
        }
        return result;
    }

    /// <summary>Records a new due owed to the employee (e.g. a period's salary or a bonus).</summary>
    public int AddDue(int employeeId, decimal amount, string description)
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            employee.CurrentBalance += amount;
            var entry = new EmployeeLedger
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                Description = description,
                Debit = amount,
                Credit = 0,
                Balance = employee.CurrentBalance,
                ReferenceType = "Due"
            };
            db.EmployeeLedgers.Add(entry);
            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("ADD_EMPLOYEE_DUE", "Employee", employeeId, $"{description} - Rs. {amount:N0}");
            return entry.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    /// <summary>Records a payment made to the employee against their outstanding dues.</summary>
    public int MakePayment(int employeeId, decimal amount, string notes = "")
    {
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        try
        {
            var employee = db.Employees.Find(employeeId);
            if (employee == null) return 0;

            var payment = new EmployeePayment
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                Notes = notes,
                CreatedBy = AuthService.CurrentUser?.Id
            };
            db.EmployeePayments.Add(payment);
            db.SaveChanges();

            employee.CurrentBalance -= amount;
            db.EmployeeLedgers.Add(new EmployeeLedger
            {
                ShopId = employee.ShopId,
                EmployeeId = employeeId,
                Description = "Payment Made",
                Debit = 0,
                Credit = amount,
                Balance = employee.CurrentBalance,
                ReferenceId = payment.Id,
                ReferenceType = "Payment"
            });

            db.CashTransactions.Add(new CashTransaction
            {
                ShopId = employee.ShopId,
                TransactionType = "EMPLOYEE_PAYMENT",
                Amount = amount,
                IsInflow = false,
                Description = $"Payment to {employee.Name}",
                ReferenceId = payment.Id,
                ReferenceType = "EmployeePayment"
            });

            db.SaveChanges();
            transaction.Commit();
            AuditLogService.Log("PAY_EMPLOYEE", "Employee", employeeId, $"Paid Rs. {amount:N0} to {employee.Name}");
            return payment.Id;
        }
        catch { transaction.Rollback(); throw; }
    }

    public decimal GetTotalDues()
    {
        using var db = new AppDbContext();
        return db.Employees.Where(e => e.IsActive).ToList().Sum(e => e.CurrentBalance);
    }
}

public class EmployeeWorkSummary
{
    public int TotalWorkedDays { get; set; }
    public decimal TotalPayable { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance { get; set; }

    public string SalaryType { get; set; } = "Daily";
    public decimal DailyWage { get; set; }
    public decimal MonthlySalary { get; set; }

    /// <summary>Today's / this week's / this month's earnings (per-day sums for daily-wage
    /// staff; a pro-rata split of the fixed salary for monthly staff).</summary>
    public decimal TodayTotal { get; set; }
    public decimal WeekTotal { get; set; }
    public decimal MonthTotal { get; set; }

    /// <summary>Overall settlement state: "Paid" / "Partial" / "Unpaid".</summary>
    public string PaymentStatus { get; set; } = "Unpaid";
    public bool CurrentMonthSalaryPosted { get; set; }
}
