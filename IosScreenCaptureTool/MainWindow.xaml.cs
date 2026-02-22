using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using WpfColor = System.Windows.Media.Color;

public partial class MainWindow : Window
{
    private const string defaultAppTitle = "iOS Screen Capture Tool";
    private const string defaultPipeName = "IosScreenCaptureTool.CommandPipe.v1";

    private readonly SemaphoreSlim frameAccessSemaphore = new(1, 1);
    private readonly object frameLock = new();
    private readonly bool isDesignMode;
    private string appTitle = defaultAppTitle;
    private string pipeName = defaultPipeName;
    private string frameDirectory = string.Empty;
    private AppSettings appSettings = new();
    private DrawingIcon appIcon = DrawingSystemIcons.Application;
    private bool ownsAppIcon;
    private byte[]? latestFrameBytes;
    private Forms.NotifyIcon? trayIcon;
    private CancellationTokenSource? commandCancellationTokenSource;
    private Task? commandServerTask;
    private CancellationTokenSource? streamCancellationTokenSource;
    private Task? streamTask;
    private bool started;
    private bool exiting;
    private bool startMinimizedAtLaunch;
    private bool useLightTheme;
    private bool suppressStartupCheckboxChanged;
    private volatile int targetFps = 30;
    private int maxObservedFps = 1;
    private bool suppressFpsSelectionChanged;

    public System.Collections.ObjectModel.ObservableCollection<CapturedFrame> CapturedFrames { get; } = new();

    public MainWindow()
        : this(defaultAppTitle, defaultPipeName, false, isDesignerConstructor: true)
    {
    }

    public MainWindow(string appTitle, string pipeName, bool startMinimizedAtLaunch)
        : this(appTitle, pipeName, startMinimizedAtLaunch, isDesignerConstructor: false)
    {
    }

    private MainWindow(string appTitle, string pipeName, bool startMinimizedAtLaunch, bool isDesignerConstructor)
    {
        isDesignMode = isDesignerConstructor || DesignerProperties.GetIsInDesignMode(new DependencyObject());
        this.appTitle = appTitle;
        this.pipeName = pipeName;
        this.startMinimizedAtLaunch = startMinimizedAtLaunch;

        InitializeComponent();
        InitializeVisualConfiguration();

        if (!isDesignMode)
        {
            InitializeRuntimeConfiguration();
        }

        Loaded += OnWindowLoaded;
        StateChanged += OnWindowStateChanged;
        Closing += OnWindowClosing;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (isDesignMode || !OperatingSystem.IsWindows())
        {
            return;
        }

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        WindowsWindowBackdrop.Apply(hwnd);
        WindowsWindowBackdrop.SetDarkTitleBar(hwnd, useDarkTitleBar: !useLightTheme);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!isDesignMode && OperatingSystem.IsWindows())
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        commandCancellationTokenSource?.Cancel();
        streamCancellationTokenSource?.Cancel();

        try
        {
            Task[] runningTasks = new Task?[] { streamTask, commandServerTask }
                .OfType<Task>()
                .ToArray();
            if (runningTasks.Length > 0)
            {
                Task.WaitAll(runningTasks, 2000);
            }
        }
        catch
        {
            // best effort shutdown
        }

        trayIcon?.Dispose();
        if (ownsAppIcon)
        {
            appIcon.Dispose();
        }

        previewImage.Source = null;
        frameAccessSemaphore.Dispose();
        commandCancellationTokenSource?.Dispose();
        streamCancellationTokenSource?.Dispose();

        base.OnClosed(e);
    }

    private void InitializeVisualConfiguration()
    {
        Title = appTitle;
        FontFamily = ResolveUiFont();

        streamTitleText.FontFamily = FontFamily;
        capturedFramesTitleText.FontFamily = FontFamily;
        settingsTitleText.FontFamily = FontFamily;
        statusText.FontFamily = FontFamily;
        fpsComboBox.FontFamily = FontFamily;
        captureFolderHeaderText.FontFamily = FontFamily;
        captureFolderTextBox.FontFamily = FontFamily;
        startWithWindowsCheckBox.FontFamily = FontFamily;
        keepMinimizedCheckBox.FontFamily = FontFamily;
        browseCaptureFolderButton.FontFamily = FontFamily;
        captureNowButton.FontFamily = FontFamily;

        capturedFramesListBox.ItemsSource = CapturedFrames;

        StylePrimaryButton(captureNowButton, isLightTheme: true);
        StyleSecondaryButton(browseCaptureFolderButton, isLightTheme: true);

        browseCaptureFolderButton.Click += (_, _) => ChooseCaptureFolder();
        captureNowButton.Click += async (_, _) => await CaptureScreenshotFromUiAsync();
        startWithWindowsCheckBox.Checked += StartWithWindowsCheckBoxCheckedChanged;
        startWithWindowsCheckBox.Unchecked += StartWithWindowsCheckBoxCheckedChanged;
        keepMinimizedCheckBox.Checked += KeepMinimizedCheckBoxCheckedChanged;
        keepMinimizedCheckBox.Unchecked += KeepMinimizedCheckBoxCheckedChanged;
        fpsComboBox.SelectionChanged += FpsComboBoxSelectionChanged;
        UpdateTargetFpsFromSelection();
        UpdateAvailableFpsOptions(maxObservedFps);
    }

    private void InitializeRuntimeConfiguration()
    {
        appSettings = AppSettingsStore.Load();

        DrawingIcon? extracted = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);
        if (extracted is not null)
        {
            appIcon = extracted;
            ownsAppIcon = true;
        }

        ImageSource? iconSource = CreateImageSourceFromIcon(appIcon);
        if (iconSource is not null)
        {
            Icon = iconSource;
        }

        captureFolderTextBox.Text = appSettings.CaptureFolder;

        if (OperatingSystem.IsWindows())
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        ApplyTheme(WindowsThemeReader.IsAppLightTheme());

        frameDirectory = Path.Combine(Path.GetTempPath(), "IosScreenCaptureTool", "frames");
        Directory.CreateDirectory(frameDirectory);

        bool startupEnabled = StartupRegistration.IsEnabled();
        if (appSettings.StartWithWindowsMinimized != startupEnabled)
        {
            appSettings.StartWithWindowsMinimized = startupEnabled;
            AppSettingsStore.Save(appSettings);
        }

        suppressStartupCheckboxChanged = true;
        startWithWindowsCheckBox.IsChecked = appSettings.StartWithWindowsMinimized;
        keepMinimizedCheckBox.IsChecked = appSettings.KeepMinimizedAfterClosing;
        suppressStartupCheckboxChanged = false;

        trayIcon = new Forms.NotifyIcon
        {
            Text = appTitle,
            Icon = appIcon,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray));
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (isDesignMode || started)
        {
            return;
        }

        started = true;
        streamCancellationTokenSource = new CancellationTokenSource();
        commandCancellationTokenSource = new CancellationTokenSource();
        streamTask = Task.Run(() => StreamLoopAsync(streamCancellationTokenSource.Token));
        commandServerTask = Task.Run(() => CommandServerLoopAsync(commandCancellationTokenSource.Token));

        if (startMinimizedAtLaunch)
        {
            await Dispatcher.InvokeAsync(MinimizeToTray, DispatcherPriority.ApplicationIdle);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting && !isDesignMode && appSettings.KeepMinimizedAfterClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        commandCancellationTokenSource?.Cancel();
        streamCancellationTokenSource?.Cancel();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General &&
            e.Category != UserPreferenceCategory.Color &&
            e.Category != UserPreferenceCategory.VisualStyle)
        {
            return;
        }

        bool isLightTheme = WindowsThemeReader.IsAppLightTheme();
        Dispatcher.BeginInvoke(new Action(() => ApplyTheme(isLightTheme)));
    }

    private void ApplyTheme(bool isLightTheme)
    {
        useLightTheme = isLightTheme;

        WpfColor window = isLightTheme ? WpfColor.FromRgb(243, 243, 243) : WpfColor.FromRgb(31, 31, 31);
        WpfColor card = isLightTheme ? WpfColor.FromRgb(252, 252, 252) : WpfColor.FromRgb(45, 45, 45);
        WpfColor input = isLightTheme ? Colors.White : WpfColor.FromRgb(58, 58, 58);
        WpfColor textPrimary = isLightTheme ? WpfColor.FromRgb(28, 28, 28) : WpfColor.FromRgb(242, 242, 242);
        WpfColor textSecondary = isLightTheme ? WpfColor.FromRgb(86, 86, 86) : WpfColor.FromRgb(196, 196, 196);
        WpfColor border = isLightTheme ? WpfColor.FromArgb(31, 0, 0, 0) : WpfColor.FromArgb(77, 255, 255, 255);
        WpfColor hover = isLightTheme ? WpfColor.FromRgb(240, 240, 240) : WpfColor.FromRgb(60, 60, 60);
        WpfColor selected = isLightTheme ? WpfColor.FromRgb(230, 230, 230) : WpfColor.FromRgb(75, 75, 75);
        WpfColor scrollbarTrack = isLightTheme ? WpfColor.FromRgb(245, 245, 245) : WpfColor.FromRgb(40, 40, 40);
        WpfColor scrollbarThumb = isLightTheme ? WpfColor.FromRgb(200, 200, 200) : WpfColor.FromRgb(90, 90, 90);
        WpfColor scrollbarThumbHover = isLightTheme ? WpfColor.FromRgb(180, 180, 180) : WpfColor.FromRgb(110, 110, 110);
        WpfColor scrollbarThumbPressed = isLightTheme ? WpfColor.FromRgb(160, 160, 160) : WpfColor.FromRgb(130, 130, 130);

        Resources["ThemeCardBrush"] = CreateBrush(card);
        Resources["ThemeTextPrimaryBrush"] = CreateBrush(textPrimary);
        Resources["ThemeBorderBrush"] = CreateBrush(border);
        Resources["ThemeHoverBrush"] = CreateBrush(hover);
        Resources["ThemeSelectedBrush"] = CreateBrush(selected);
        Resources["ThemeScrollBarTrackBrush"] = CreateBrush(scrollbarTrack);
        Resources["ThemeScrollBarThumbBrush"] = CreateBrush(scrollbarThumb);
        Resources["ThemeScrollBarThumbHoverBrush"] = CreateBrush(scrollbarThumbHover);
        Resources["ThemeScrollBarThumbPressedBrush"] = CreateBrush(scrollbarThumbPressed);

        Background = CreateBrush(window);
        rootGrid.Background = CreateBrush(window);
        streamHostGrid.Background = CreateBrush(window);
        rightHostGrid.Background = CreateBrush(window);

        streamCardBorder.Background = System.Windows.Media.Brushes.Black;
        streamCardBorder.BorderBrush = CreateBrush(border);

        streamInfoCardBorder.Background = CreateBrush(card);
        streamInfoCardBorder.BorderBrush = CreateBrush(border);
        capturedFramesCardBorder.Background = CreateBrush(card);
        capturedFramesCardBorder.BorderBrush = CreateBrush(border);
        settingsCardBorder.Background = CreateBrush(card);
        settingsCardBorder.BorderBrush = CreateBrush(border);

        streamTitleText.Foreground = CreateBrush(textPrimary);
        capturedFramesTitleText.Foreground = CreateBrush(textPrimary);
        settingsTitleText.Foreground = CreateBrush(textPrimary);
        statusText.Foreground = CreateBrush(textPrimary);
        fpsComboBox.Foreground = CreateBrush(textSecondary);
        captureFolderHeaderText.Foreground = CreateBrush(textSecondary);
        startWithWindowsCheckBox.Foreground = CreateBrush(textPrimary);
        keepMinimizedCheckBox.Foreground = CreateBrush(textPrimary);

        fpsComboBox.Background = CreateBrush(input);
        fpsComboBox.BorderBrush = CreateBrush(border);

        captureFolderTextBox.Background = CreateBrush(input);
        captureFolderTextBox.Foreground = CreateBrush(textPrimary);
        captureFolderTextBox.BorderBrush = CreateBrush(border);
        capturedFramesListBox.Foreground = CreateBrush(textPrimary);

        StylePrimaryButton(captureNowButton, isLightTheme);
        StyleSecondaryButton(browseCaptureFolderButton, isLightTheme);

        if (IsLoaded && OperatingSystem.IsWindows())
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            WindowsWindowBackdrop.SetDarkTitleBar(hwnd, useDarkTitleBar: !isLightTheme);
        }
    }

    private static SolidColorBrush CreateBrush(WpfColor color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.FontFamily ResolveUiFont()
    {
        string[] candidates = ["Segoe UI Variable Text", "Segoe UI"];
        foreach (string candidate in candidates)
        {
            if (Fonts.SystemFontFamilies.Any(fontFamily =>
                string.Equals(fontFamily.Source, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return new System.Windows.Media.FontFamily(candidate);
            }
        }

        return new System.Windows.Media.FontFamily("Segoe UI");
    }

    private static void StylePrimaryButton(System.Windows.Controls.Button button, bool isLightTheme)
    {
        button.BorderThickness = new Thickness(0);
        button.Foreground = System.Windows.Media.Brushes.White;
        button.Background = isLightTheme
            ? CreateBrush(WpfColor.FromRgb(0, 95, 184))
            : CreateBrush(WpfColor.FromRgb(76, 157, 255));
    }

    private static void StyleSecondaryButton(System.Windows.Controls.Button button, bool isLightTheme)
    {
        button.BorderThickness = new Thickness(1);
        button.BorderBrush = isLightTheme
            ? CreateBrush(WpfColor.FromRgb(206, 206, 206))
            : CreateBrush(WpfColor.FromRgb(86, 86, 86));
        button.Background = isLightTheme
            ? CreateBrush(WpfColor.FromRgb(250, 250, 250))
            : CreateBrush(WpfColor.FromRgb(62, 62, 62));
        button.Foreground = isLightTheme
            ? CreateBrush(WpfColor.FromRgb(30, 30, 30))
            : CreateBrush(WpfColor.FromRgb(240, 240, 240));
    }

    private static ImageSource? CreateImageSourceFromIcon(DrawingIcon icon)
    {
        try
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            return null;
        }
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        Forms.ContextMenuStrip menu = new();

        Forms.ToolStripMenuItem showItem = new("Show");
        showItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray));

        Forms.ToolStripMenuItem captureItem = new("Grab Screenshot");
        captureItem.Click += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(async () => await CaptureScreenshotFromUiAsync()));
        };

        Forms.ToolStripMenuItem exitItem = new("Exit");
        exitItem.Click += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                exiting = true;
                Close();
                System.Windows.Application.Current?.Shutdown();
            }));
        };

        menu.Items.Add(showItem);
        menu.Items.Add(captureItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void MinimizeToTray()
    {
        if (!IsLoaded)
        {
            return;
        }

        Hide();
        ShowInTaskbar = false;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void ChooseCaptureFolder()
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = "Select folder for manual screenshots",
            UseDescriptionForTitle = true,
            SelectedPath = captureFolderTextBox.Text
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        appSettings.CaptureFolder = dialog.SelectedPath;
        captureFolderTextBox.Text = dialog.SelectedPath;
        AppSettingsStore.Save(appSettings);
    }

    private async Task CaptureScreenshotFromUiAsync()
    {
        string targetFolder = string.IsNullOrWhiteSpace(captureFolderTextBox.Text)
            ? AppSettingsStore.DefaultCaptureFolder
            : captureFolderTextBox.Text;

        Directory.CreateDirectory(targetFolder);
        string fileName = $"ios-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string outputPath = Path.Combine(targetFolder, fileName);

        PipeCommandResponse response = await SaveCurrentFrameAsync(outputPath, CancellationToken.None);
        if (response.Success)
        {
            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(outputPath);
                bitmap.DecodePixelWidth = 100;
                bitmap.EndInit();
                bitmap.Freeze();

                CapturedFrames.Insert(0, new CapturedFrame
                {
                    FilePath = outputPath,
                    Thumbnail = bitmap
                });
            }
            catch
            {
                // Ignore thumbnail creation errors
            }

            return;
        }

        System.Windows.MessageBox.Show(
            this,
            response.Message ?? "Failed to capture screenshot.",
            appTitle,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private void StartWithWindowsCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (suppressStartupCheckboxChanged)
        {
            return;
        }

        bool enabled = startWithWindowsCheckBox.IsChecked == true;
        try
        {
            StartupRegistration.SetEnabled(enabled);
            appSettings.StartWithWindowsMinimized = enabled;
            AppSettingsStore.Save(appSettings);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"Failed to update Windows startup setting.{Environment.NewLine}{exception.Message}",
                appTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            suppressStartupCheckboxChanged = true;
            startWithWindowsCheckBox.IsChecked = !enabled;
            suppressStartupCheckboxChanged = false;
        }
    }

    private void KeepMinimizedCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (suppressStartupCheckboxChanged)
        {
            return;
        }

        appSettings.KeepMinimizedAfterClosing = keepMinimizedCheckBox.IsChecked == true;
        AppSettingsStore.Save(appSettings);
    }

    private async Task CommandServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream pipeServer = CreatePipeServer();
                await pipeServer.WaitForConnectionAsync(cancellationToken);

                using StreamReader reader = new(pipeServer, leaveOpen: true);
                using StreamWriter writer = new(pipeServer, leaveOpen: true) { AutoFlush = true };

                string? requestLine = await reader.ReadLineAsync(cancellationToken);
                PipeCommandResponse response = await HandleCommandAsync(requestLine, cancellationToken);
                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // continue serving next command
            }
        }
    }

    private NamedPipeServerStream CreatePipeServer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        PipeSecurity security = new();
        SecurityIdentifier? currentUserSid = WindowsIdentity.GetCurrent().User;
        if (currentUserSid is null)
        {
            currentUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        }

        PipeAccessRule accessRule = new(
            currentUserSid,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow);

        security.AddAccessRule(accessRule);
        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private async Task<PipeCommandResponse> HandleCommandAsync(string? requestLine, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return new PipeCommandResponse(false, "Empty command.");
        }

        PipeCommandRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PipeCommandRequest>(requestLine);
        }
        catch (JsonException)
        {
            return new PipeCommandResponse(false, "Invalid command payload.");
        }

        if (request is null || !string.Equals(request.Command, "capture-frame", StringComparison.OrdinalIgnoreCase))
        {
            return new PipeCommandResponse(false, "Unsupported command.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return new PipeCommandResponse(false, "Output path is required.");
        }

        return await SaveCurrentFrameAsync(request.OutputPath, cancellationToken);
    }

    private async Task<PipeCommandResponse> SaveCurrentFrameAsync(string outputPath, CancellationToken cancellationToken)
    {
        string absoluteOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(absoluteOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await frameAccessSemaphore.WaitAsync(cancellationToken);
        try
        {
            byte[]? frameSnapshot = await Dispatcher.InvokeAsync(() =>
            {
                lock (frameLock)
                {
                    return latestFrameBytes?.ToArray();
                }
            }, DispatcherPriority.Send, cancellationToken);

            if (frameSnapshot is null)
            {
                return new PipeCommandResponse(false, "No frame available yet.");
            }

            await File.WriteAllBytesAsync(absoluteOutputPath, frameSnapshot, cancellationToken);
            return new PipeCommandResponse(true, absoluteOutputPath);
        }
        catch (OperationCanceledException)
        {
            return new PipeCommandResponse(false, "Capture canceled.");
        }
        catch (Exception exception)
        {
            return new PipeCommandResponse(false, $"Failed to save frame: {exception.Message}");
        }
        finally
        {
            frameAccessSemaphore.Release();
        }
    }

    private async Task StreamLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            SetStatus("Preparing dependencies...");
            BootstrapResult bootstrapResult = await PymobiledeviceBootstrapper.EnsureAsync(cancellationToken);
            if (!bootstrapResult.Success || string.IsNullOrWhiteSpace(bootstrapResult.PythonPath))
            {
                SetError(bootstrapResult.ErrorMessage ?? "Bootstrap failed.", null);
                return;
            }

            PymobiledeviceClient client = new(bootstrapResult.PythonPath);
            int index = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                DeviceListResult deviceListResult = await client.GetUsbDeviceUdidsAsync(cancellationToken);
                if (!deviceListResult.Success)
                {
                    SetStatus("Waiting for iOS device...");
                    SetFps(0);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                if (deviceListResult.Udids.Count == 0)
                {
                    SetStatus("No iOS device found. Waiting for reconnect...");
                    SetFps(0);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                if (deviceListResult.Udids.Count > 1)
                {
                    SetStatus("Multiple iOS devices detected. Connect only one device.");
                    SetFps(0);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                string udid = deviceListResult.Udids[0];

                SetStatus("Verifying Developer Mode...");
                DeveloperModeResult devModeResult = await client.EnsureDeveloperModeAsync(udid, cancellationToken);
                if (devModeResult.RebootRequired)
                {
                    SetStatus("Developer Mode enabled — restart your device to activate it, then reconnect.");
                    SetFps(0);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                SetStatus("Opening stream tunnel...");
                StreamSetupResult setup = await client.OpenStreamSessionAsync(udid, cancellationToken);
                if (!setup.Success || setup.Session is null)
                {
                    SetStatus("Unable to open stream tunnel. Retrying...");
                    SetFps(0);
                    await DelayBeforeReconnectAsync(cancellationToken);
                    continue;
                }

                await using StreamSession session = setup.Session;
                Stopwatch fpsWatch = Stopwatch.StartNew();
                int frameCount = 0;
                SetStatus("Streaming...");

                while (!cancellationToken.IsCancellationRequested)
                {
                    Stopwatch frameTimer = Stopwatch.StartNew();
                    string framePath = Path.Combine(frameDirectory, $"frame-{index % 3}.png");
                    FrameCaptureResult frameResult = await client.CaptureFrameAsync(session, framePath, cancellationToken);
                    if (!frameResult.Success)
                    {
                        SetStatus(BuildReconnectStatus(frameResult));
                        SetFps(0);
                        await DelayBeforeReconnectAsync(cancellationToken);
                        break;
                    }

                    byte[]? frameBytes = LoadFrameBytes(framePath);
                    if (frameBytes is not null)
                    {
                        SetFrame(frameBytes);
                    }

                    frameCount++;
                    index++;
                    if (fpsWatch.ElapsedMilliseconds >= 1000)
                    {
                        SetFps(frameCount);
                        frameCount = 0;
                        fpsWatch.Restart();
                    }

                    int fpsTarget = Math.Max(1, targetFps);
                    TimeSpan targetFrameTime = TimeSpan.FromMilliseconds(1000d / fpsTarget);
                    TimeSpan remaining = targetFrameTime - frameTimer.Elapsed;
                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception exception)
        {
            SetError("Unexpected stream failure.", exception.Message);
        }
    }

    private static async Task DelayBeforeReconnectAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private static string BuildReconnectStatus(FrameCaptureResult frameResult)
    {
        string details = frameResult.Details ?? string.Empty;
        string normalized = details.ToLowerInvariant();

        if (normalized.Contains("developer", StringComparison.Ordinal) ||
            normalized.Contains("dvt", StringComparison.Ordinal) ||
            normalized.Contains("invalidservice", StringComparison.Ordinal) ||
            normalized.Contains("not enabled", StringComparison.Ordinal))
        {
            return "Stream failed — Developer Mode may not be active on the device. Reconnecting...";
        }

        if (normalized.Contains("timeout", StringComparison.Ordinal) ||
            normalized.Contains("connect call failed", StringComparison.Ordinal) ||
            normalized.Contains("errno 10060", StringComparison.Ordinal) ||
            normalized.Contains("connection refused", StringComparison.Ordinal) ||
            normalized.Contains("device disconnected", StringComparison.Ordinal) ||
            normalized.Contains("broken pipe", StringComparison.Ordinal))
        {
            return "Device link lost. Reconnecting stream...";
        }

        return "Stream interrupted. Reconnecting...";
    }

    private static byte[]? LoadFrameBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    private void SetFrame(byte[] frameBytes)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action<byte[]>(SetFrame), frameBytes);
            return;
        }

        BitmapImage? nextImage = CreateBitmapImage(frameBytes);
        if (nextImage is null)
        {
            return;
        }

        lock (frameLock)
        {
            latestFrameBytes = frameBytes.ToArray();
        }

        previewImage.Source = nextImage;
    }

    private static BitmapImage? CreateBitmapImage(byte[] frameBytes)
    {
        try
        {
            using MemoryStream stream = new(frameBytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action<string>(SetStatus), text);
            return;
        }

        statusText.Text = text;
    }

    private void SetFps(int fps)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action<int>(SetFps), fps);
            return;
        }

        fpsComboBox.ToolTip = $"Current: {fps} fps";
        UpdateAvailableFpsOptions(fps);
    }

    private void FpsComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressFpsSelectionChanged)
        {
            return;
        }

        if (fpsComboBox.SelectedValue is not null &&
            int.TryParse(fpsComboBox.SelectedValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps) &&
            maxObservedFps > 0 &&
            fps > maxObservedFps)
        {
            SelectClosestSupportedFps();
            return;
        }

        UpdateTargetFpsFromSelection();
    }

    private void UpdateTargetFpsFromSelection()
    {
        if (fpsComboBox.SelectedValue is not null &&
            int.TryParse(fpsComboBox.SelectedValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
        {
            targetFps = Math.Clamp(fps, 1, 120);
        }
    }

    private void UpdateAvailableFpsOptions(int measuredFps)
    {
        if (measuredFps <= 0)
        {
            return;
        }

        if (measuredFps > maxObservedFps)
        {
            maxObservedFps = measuredFps;
        }

        int maxAllowed = Math.Max(1, maxObservedFps);
        ComboBoxItem? fallback = null;

        foreach (object item in fpsComboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                int.TryParse(comboItem.Tag?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
            {
                bool enabled = fps <= maxAllowed;
                comboItem.IsEnabled = enabled;
                comboItem.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                if (enabled)
                {
                    fallback = comboItem;
                }
            }
        }

        if (fpsComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.IsEnabled)
        {
            return;
        }

        if (fallback is not null)
        {
            suppressFpsSelectionChanged = true;
            fpsComboBox.SelectedItem = fallback;
            suppressFpsSelectionChanged = false;
            UpdateTargetFpsFromSelection();
        }
    }

    private void SelectClosestSupportedFps()
    {
        ComboBoxItem? fallback = null;

        foreach (object item in fpsComboBox.Items)
        {
            if (item is ComboBoxItem comboItem && comboItem.IsEnabled)
            {
                fallback = comboItem;
            }
        }

        if (fallback is not null)
        {
            suppressFpsSelectionChanged = true;
            fpsComboBox.SelectedItem = fallback;
            suppressFpsSelectionChanged = false;
            UpdateTargetFpsFromSelection();
        }
    }

    private void SetError(string message, string? details)
    {
        SetStatus(message);
        SetFps(0);

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action<string, string?>(SetError), message, details);
            return;
        }

        string fullMessage = string.IsNullOrWhiteSpace(details)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}{details}";

        System.Windows.MessageBox.Show(
            this,
            fullMessage,
            appTitle,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private void CapturedFrame_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is CapturedFrame frame)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = frame.FilePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void CopyImageLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is CapturedFrame frame)
        {
            try
            {
                System.Windows.Clipboard.SetText(frame.FilePath);
            }
            catch { }
        }
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is CapturedFrame frame)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{frame.FilePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}

public class CapturedFrame
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public ImageSource? Thumbnail { get; set; }
}
