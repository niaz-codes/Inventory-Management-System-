using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NHi.Inventory.Models;
using NHi.Inventory.Services;

namespace NHi.Inventory.Views.Khata;

/// <summary>Read-only per-customer log of every Reminder/Receipt send attempt (one row per
/// language+channel), newest first, with Retry for anything that failed.</summary>
public partial class MessageHistoryWindow : Window
{
    private readonly Customer _customer;
    private readonly WhatsAppService _whatsApp = new();

    public MessageHistoryWindow(Customer customer)
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        _customer = customer;
        txtSubtitle.Text = customer.Name;
        LoadHistory();
    }

    private void LoadHistory()
    {
        historyPanel.Children.Clear();
        var history = _whatsApp.GetHistory(_customer.Id);

        if (history.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = LanguageManager.GetString("NoMessagesYet"),
                FontSize = 12,
                Margin = new Thickness(0, 10, 0, 0)
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            historyPanel.Children.Add(empty);
            return;
        }

        foreach (var log in history) historyPanel.Children.Add(BuildRow(log));
    }

    private UIElement BuildRow(MessageLog log)
    {
        var (badgeText, badgeHex) = log.Status switch
        {
            "Opened" => (LanguageManager.GetString("WhatsAppOpenedBadge"), "#2E7D32"),
            "Failed" => (LanguageManager.GetString("StatusFailed"), "#C62828"),
            _ => (LanguageManager.GetString("StatusNotAvailable"), (string?)null)
        };

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(0, 0, 0, 6)
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var stack = new StackPanel();
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var typeLabel = new TextBlock
        {
            Text = $"{LanguageManager.GetString(log.MessageType == "Invoice" ? "InvoiceLabel" : "SendReminder")} — {LanguageManager.GetString("WhatsApp")}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        typeLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        Grid.SetColumn(typeLabel, 0);

        var badge = new TextBlock
        {
            Text = badgeText,
            FontSize = 11,
            FontWeight = FontWeights.Bold
        };
        if (badgeHex != null)
            badge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeHex));
        else
            badge.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        Grid.SetColumn(badge, 1);
        topRow.Children.Add(typeLabel);
        topRow.Children.Add(badge);

        if (log.Status == "Failed")
        {
            var retryBtn = new Button
            {
                Content = LanguageManager.GetString("Retry"),
                FontSize = 10,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = log.Id
            };
            retryBtn.SetResourceReference(Button.BackgroundProperty, "HoverBrush");
            retryBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            retryBtn.Click += (s, _) =>
            {
                if (s is not Button btn || btn.Tag is not int logId) return;
                btn.IsEnabled = false;
                _whatsApp.Retry(logId);
                LoadHistory();
            };
            Grid.SetColumn(retryBtn, 2);
            topRow.Children.Add(retryBtn);
        }

        stack.Children.Add(topRow);
        var timestamp = new TextBlock
        {
            Text = log.CreatedAt.ToString("dd-MMM-yyyy hh:mm tt"),
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0)
        };
        timestamp.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        stack.Children.Add(timestamp);

        border.Child = stack;
        return border;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
