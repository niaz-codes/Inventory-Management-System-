using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NHi.Inventory.Data;
using NHi.Inventory.Services;
using NHi.Inventory.Views.Admin;

namespace NHi.Inventory.Views;

public partial class LoginWindow : Window
{
    private bool _passwordVisible;

    // --- Rotating tagline typing animation (right-side branding panel) ---
    private static readonly string[] RotatingPhrases =
    {
        "Smart Inventory. Smarter Business.",
        "Manage Stock, Sales & Purchases Easily.",
        "Powerful POS for Your Business.",
        "Track Customers, Products & Khata in One Place.",
        "Simple, Fast & Reliable Inventory Management."
    };
    private const int TypingIntervalMs = 45;
    private const int DeletingIntervalMs = 28;
    private DispatcherTimer? _typingTimer;
    private int _phraseIndex;
    private int _charIndex;
    private bool _isDeleting;
    private int _pauseTicks;

    public LoginWindow()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        // Reflect the persisted language choice (applied at app startup) on the login selector.
        rbUrdu.IsChecked = LanguageManager.CurrentLanguage == "ur";
        rbEnglish.IsChecked = LanguageManager.CurrentLanguage != "ur";
        txtUsername.Focus();
        StartTypingAnimation();
    }

    /// <summary>Drives the rotating tagline: types a phrase in, pauses, deletes it, then
    /// moves to the next phrase — looping forever. Runs on a single tick-counting timer
    /// (rather than swapping Interval mid-flight) to keep the state machine simple.</summary>
    private void StartTypingAnimation()
    {
        _typingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TypingIntervalMs) };
        _typingTimer.Tick += TypingTimer_Tick;
        _typingTimer.Start();
        Closed += (_, _) => _typingTimer?.Stop();
    }

    private void TypingTimer_Tick(object? sender, EventArgs e)
    {
        if (_pauseTicks > 0)
        {
            _pauseTicks--;
            return;
        }

        var phrase = RotatingPhrases[_phraseIndex];

        if (!_isDeleting)
        {
            _charIndex++;
            txtTyping.Text = phrase[.._charIndex];
            _typingTimer!.Interval = TimeSpan.FromMilliseconds(TypingIntervalMs);

            if (_charIndex >= phrase.Length)
            {
                _isDeleting = true;
                _pauseTicks = 1400 / TypingIntervalMs; // brief pause once the sentence is fully typed
            }
        }
        else
        {
            _charIndex--;
            txtTyping.Text = phrase[.._charIndex];
            _typingTimer!.Interval = TimeSpan.FromMilliseconds(DeletingIntervalMs);

            if (_charIndex <= 0)
            {
                _isDeleting = false;
                _phraseIndex = (_phraseIndex + 1) % RotatingPhrases.Length;
                _pauseTicks = 350 / DeletingIntervalMs; // brief pause before the next sentence starts
            }
        }
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        var username = txtUsername.Text.Trim();
        var password = _passwordVisible ? txtPasswordVisible.Text : txtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            txtError.Text = LanguageManager.GetString("FillRequired");
            return;
        }

        if (AuthService.Login(username, password))
        {
            if (AuthService.CurrentUser!.ShopId == null)
            {
                // Main Admin isn't tied to a single shop — they manage shops/users instead
                // of the normal operational screens (POS/Inventory/Khata).
                var shopManagement = new ShopManagementView();
                shopManagement.Show();
                this.Close();
                return;
            }

            using (var db = new AppDbContext())
            {
                var theme = db.Shops.FirstOrDefault(s => s.Id == AuthService.CurrentShopId)?.Theme ?? "light";
                ThemeManager.SetTheme(theme);
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
        else
        {
            txtError.Text = LanguageManager.GetString("LoginFailed");
        }
    }

    private void BtnToggleEye_Click(object sender, RoutedEventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        if (_passwordVisible)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPasswordVisible.Visibility = Visibility.Visible;
            txtPassword.Visibility = Visibility.Collapsed;
            iconEye.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
        }
        else
        {
            txtPassword.Password = txtPasswordVisible.Text;
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            iconEye.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
        }
    }

    private void Lang_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var lang = rbUrdu.IsChecked == true ? "ur" : "en";
        LanguageManager.SetLanguage(lang);
    }
}
