using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NHi.Inventory.Services;
using OpenCvSharp;
using ZXing;
using ZXing.Common;
// OpenCvSharp also defines a "Window" type — keep bare "Window" meaning the WPF one.
using Window = System.Windows.Window;

namespace NHi.Inventory.Views.POS;

/// <summary>
/// Live camera barcode scanner. Enumerates the machine's cameras, streams the selected one into
/// the preview, and decodes every few frames with ZXing. Each distinct barcode (deduped for two
/// seconds so a held item isn't added repeatedly) is raised via <see cref="BarcodeScanned"/> —
/// the POS screen adds it to the cart. Works with built-in / USB webcams and phone-as-webcam
/// bridges (DroidCam, Iriun, EpocCam), which need a few seconds to start streaming and often
/// only deliver MJPG — both are handled below. The window stays open for continuous scanning
/// until the cashier presses Close.
/// </summary>
public partial class CameraScanWindow : Window
{
    /// <summary>Raised on the UI thread for every accepted barcode read.</summary>
    public event Action<string>? BarcodeScanned;

    /// <summary>The most recent barcode, for callers that just want a single scan.</summary>
    public string? ScannedBarcode { get; private set; }

    private readonly BarcodeReaderGeneric _reader = new()
    {
        AutoRotate = true,
        Options = new DecodingOptions
        {
            TryHarder = true,
            PossibleFormats = new List<BarcodeFormat>
            {
                BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
                BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93,
                BarcodeFormat.ITF, BarcodeFormat.CODABAR, BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX
            }
        }
    };

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private VideoCapture? _cap;          // held so Stop can Release() it and unblock a stuck Read
    private readonly object _capLock = new();

    private WriteableBitmap? _bitmap;
    private byte[]? _frameBuffer;

    private string? _lastCode;
    private DateTime _lastCodeAt;

    public CameraScanWindow()
    {
        InitializeComponent();
        FlowDirection = LanguageManager.CurrentFlowDirection;
        Loaded += CameraScanWindow_Loaded;
        Closed += (_, _) => StopCamera();
    }

    private async void CameraScanWindow_Loaded(object sender, RoutedEventArgs e) => await RefreshCameraList();

    private async void BtnRefreshCameras_Click(object sender, RoutedEventArgs e) => await RefreshCameraList();

    private async Task RefreshCameraList()
    {
        var current = (cmbCamera.SelectedItem as CameraChoice)?.Index ?? -1;
        StopCamera();
        imgPreview.Source = null;
        _bitmap = null;
        guide.Visibility = Visibility.Collapsed;
        cmbCamera.IsEnabled = false;
        cmbCamera.ItemsSource = null;
        txtStatus.Text = LanguageManager.GetString("DetectingCameras");

        var cameras = await Task.Run(DetectCameras);

        if (cameras.Count == 0)
        {
            txtStatus.Text = LanguageManager.GetString("NoCameraFound");
            return;
        }

        cmbCamera.IsEnabled = true;
        cmbCamera.ItemsSource = cameras;

        // Keep the previous pick if it's still present; otherwise default to a rear/back-facing
        // camera when the OS names one, else the first device.
        var keep = cameras.FirstOrDefault(c => c.Index == current);
        var rear = cameras.FirstOrDefault(c =>
            c.Label.Contains("back", StringComparison.OrdinalIgnoreCase) ||
            c.Label.Contains("rear", StringComparison.OrdinalIgnoreCase) ||
            c.Label.Contains("environment", StringComparison.OrdinalIgnoreCase));
        cmbCamera.SelectedItem = keep ?? rear ?? cameras[0];   // triggers CmbCamera_SelectionChanged -> StartCamera
    }

    /// <summary>
    /// Lists every video-input device by real name (DirectShow enumerator — doesn't touch the
    /// camera stream, so a second/third webcam is reported reliably). Falls back to probing
    /// indices 0..9 only if the name enumeration comes back empty.
    /// </summary>
    private static List<CameraChoice> DetectCameras()
    {
        var choices = new List<CameraChoice>();
        try
        {
            foreach (var d in CameraEnumerator.List())
                choices.Add(new CameraChoice(d.Index, d.Name));
        }
        catch { /* fall through to probing */ }

        if (choices.Count > 0) return choices;

        var label = LanguageManager.GetString("SelectCamera");
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (probe.IsOpened()) choices.Add(new CameraChoice(i, $"{label} {i + 1}"));
            }
            catch { /* index not present */ }
        }
        return choices;
    }

    private void CmbCamera_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbCamera.SelectedItem is CameraChoice choice)
            StartCamera(choice.Index);
    }

    private void StartCamera(int index)
    {
        StopCamera();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        txtStatus.Text = LanguageManager.GetString("CameraStarting");
        guide.Visibility = Visibility.Visible;

        _worker = Task.Run(() =>
        {
            var mat = new Mat();
            VideoCapture? cap = null;
            try
            {
                // DirectShow needs a beat to fully release a device between open/close cycles.
                Thread.Sleep(200);

                cap = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                lock (_capLock) _cap = cap;

                if (!cap.IsOpened())
                {
                    Post(() => txtStatus.Text = LanguageManager.GetString("NoCameraFound"));
                    return;
                }

                // Ask for 720p but DON'T fail if the device (e.g. DroidCam free = 640x480)
                // ignores it — forcing an unsupported size is a classic cause of black frames.
                cap.Set(VideoCaptureProperties.FrameWidth, 1280);
                cap.Set(VideoCaptureProperties.FrameHeight, 720);

                var openedAt = DateTime.UtcNow;
                bool triedMjpg = false, warnedNoVideo = false;
                int frames = 0, decodeTick = 0;

                while (!token.IsCancellationRequested)
                {
                    bool got = false;
                    try { got = cap.Grab() && cap.Retrieve(mat) && !mat.Empty(); }
                    catch { got = false; }   // Release() from Stop unblocks Grab and lands here

                    if (!got)
                    {
                        var waited = (DateTime.UtcNow - openedAt).TotalSeconds;

                        // Some phone/USB cams only stream MJPG under DirectShow.
                        if (!triedMjpg && frames == 0 && waited > 2.5)
                        {
                            triedMjpg = true;
                            try { cap.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G')); }
                            catch { /* not supported – ignore */ }
                        }

                        if (!warnedNoVideo && frames == 0 && waited > 6)
                        {
                            warnedNoVideo = true;
                            Post(() => txtStatus.Text = LanguageManager.GetString("CameraNoVideo"));
                        }

                        Thread.Sleep(20);
                        continue;
                    }

                    if (frames == 0) Post(() => txtStatus.Text = "");
                    frames++;

                    RenderFrame(mat);
                    if (decodeTick++ % 3 == 0) TryDecode(mat);
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Post(() => txtStatus.Text = ex.Message);
            }
            finally
            {
                lock (_capLock) { if (ReferenceEquals(_cap, cap)) _cap = null; }
                try { cap?.Release(); } catch { }
                cap?.Dispose();
                mat.Dispose();
            }
        }, token);
    }

    private void StopCamera()
    {
        try
        {
            _cts?.Cancel();
            // Releasing the capture from here aborts a Grab()/Retrieve() that's blocked waiting
            // for a phone webcam that hasn't started streaming — without this the worker never
            // returns and the camera can't be reopened.
            lock (_capLock) { try { _cap?.Release(); } catch { } }
            _worker?.Wait(2500);
        }
        catch { /* ignore shutdown races */ }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _worker = null;
            lock (_capLock) _cap = null;
        }
    }

    private void Post(Action a)
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(a);
    }

    private void RenderFrame(Mat bgr)
    {
        int w = bgr.Width, h = bgr.Height;
        int stride = (int)bgr.Step();
        int len = stride * h;

        if (_frameBuffer == null || _frameBuffer.Length != len)
            _frameBuffer = new byte[len];
        Marshal.Copy(bgr.Data, _frameBuffer, 0, len);
        var snapshot = _frameBuffer;

        Post(() =>
        {
            if (_bitmap == null || _bitmap.PixelWidth != w || _bitmap.PixelHeight != h)
            {
                _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr24, null);
                imgPreview.Source = _bitmap;
            }
            _bitmap.WritePixels(new Int32Rect(0, 0, w, h), snapshot, stride, 0);
        });
    }

    private void TryDecode(Mat bgr)
    {
        try
        {
            int w = bgr.Width, h = bgr.Height;
            int stride = (int)bgr.Step();
            var buf = new byte[stride * h];
            Marshal.Copy(bgr.Data, buf, 0, buf.Length);

            var source = new RGBLuminanceSource(buf, w, h, RGBLuminanceSource.BitmapFormat.BGR24);
            var result = _reader.Decode(source);
            var text = result?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            Post(() => AcceptCode(text));
        }
        catch { /* a bad frame is not worth surfacing */ }
    }

    private void AcceptCode(string code)
    {
        var now = DateTime.Now;
        if (code == _lastCode && (now - _lastCodeAt).TotalMilliseconds < 2000) return;
        _lastCode = code;
        _lastCodeAt = now;

        ScannedBarcode = code;
        lastScanBar.Visibility = Visibility.Visible;
        txtLastScan.Text = $"✓  {code}";
        try { System.Media.SystemSounds.Beep.Play(); } catch { }

        BarcodeScanned?.Invoke(code);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        StopCamera();
        DialogResult = ScannedBarcode != null;
        Close();
    }

    private sealed record CameraChoice(int Index, string Label)
    {
        public override string ToString() => Label;
    }
}
