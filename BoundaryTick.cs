using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BoundaryTick
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayAppContext());
        }
    }

    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly StickyEdgeController _controller;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _enabledItem;
        private readonly List<ToolStripMenuItem> _delayItems;

        public TrayAppContext()
        {
            _settings = AppSettings.Load();
            _controller = new StickyEdgeController(_settings.DelayMs);
            _controller.Enabled = _settings.Enabled;
            _controller.Start();

            _enabledItem = new ToolStripMenuItem("\uCF1C\uC9D0")
            {
                Checked = _settings.Enabled,
                CheckOnClick = true
            };
            _enabledItem.CheckedChanged += OnEnabledChanged;
            UpdateEnabledLabel();

            _delayItems = new List<ToolStripMenuItem>();
            var delayMenu = new ToolStripMenuItem("\uAC78\uB9BC \uAC15\uB3C4");
            AddDelayItem(delayMenu, "\uC9E7\uAC8C (80ms)", 80);
            AddDelayItem(delayMenu, "\uAE30\uBCF8 (140ms)", 140);
            AddDelayItem(delayMenu, "\uAE38\uAC8C (220ms)", 220);
            AddDelayItem(delayMenu, "\uAC15\uD558\uAC8C (320ms)", 320);
            UpdateDelayChecks();

            var refreshItem = new ToolStripMenuItem("\uBAA8\uB2C8\uD130 \uB2E4\uC2DC \uC77D\uAE30");
            refreshItem.Click += delegate { _controller.RefreshScreens(); };

            var exitItem = new ToolStripMenuItem("\uC885\uB8CC");
            exitItem.Click += delegate { ExitThread(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add(_enabledItem);
            menu.Items.Add(delayMenu);
            menu.Items.Add(refreshItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Text = "BoundaryTick",
                ContextMenuStrip = menu,
                Visible = true
            };
            _notifyIcon.DoubleClick += delegate
            {
                _enabledItem.Checked = !_enabledItem.Checked;
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _controller.Dispose();
            }
            base.Dispose(disposing);
        }

        private void AddDelayItem(ToolStripMenuItem parent, string label, int delayMs)
        {
            var item = new ToolStripMenuItem(label)
            {
                Tag = delayMs,
                CheckOnClick = false
            };
            item.Click += delegate
            {
                _settings.DelayMs = delayMs;
                _settings.Save();
                _controller.DelayMs = delayMs;
                UpdateDelayChecks();
            };
            _delayItems.Add(item);
            parent.DropDownItems.Add(item);
        }

        private void UpdateDelayChecks()
        {
            for (int i = 0; i < _delayItems.Count; i++)
            {
                var delayMs = (int)_delayItems[i].Tag;
                _delayItems[i].Checked = delayMs == _settings.DelayMs;
            }
        }

        private void OnEnabledChanged(object sender, EventArgs e)
        {
            _settings.Enabled = _enabledItem.Checked;
            _settings.Save();
            _controller.Enabled = _settings.Enabled;
            UpdateEnabledLabel();
        }

        private void UpdateEnabledLabel()
        {
            _enabledItem.Text = _enabledItem.Checked ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0";
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    return icon;
                }
            }
            catch
            {
            }

            return SystemIcons.Application;
        }
    }

    internal sealed class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoundaryTick.ini");

        public bool Enabled = true;
        public int DelayMs = 140;

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(SettingsPath))
            {
                settings.Save();
                return settings;
            }

            foreach (var rawLine in File.ReadAllLines(SettingsPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();
                if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    bool parsed;
                    if (bool.TryParse(value, out parsed))
                    {
                        settings.Enabled = parsed;
                    }
                }
                else if (key.Equals("DelayMs", StringComparison.OrdinalIgnoreCase))
                {
                    int parsed;
                    if (int.TryParse(value, out parsed))
                    {
                        settings.DelayMs = Clamp(parsed, 20, 1000);
                    }
                }
            }

            return settings;
        }

        public void Save()
        {
            using (var writer = new StreamWriter(SettingsPath, false))
            {
                writer.WriteLine("# BoundaryTick settings");
                writer.WriteLine("Enabled=" + Enabled);
                writer.WriteLine("DelayMs=" + DelayMs);
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }

    internal sealed class StickyEdgeController : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int LLMHF_INJECTED = 0x00000001;

        private readonly object _sync = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private NativeMethods.LowLevelMouseProc _hookProc;
        private IntPtr _hookId = IntPtr.Zero;
        private List<MonitorArea> _monitors = new List<MonitorArea>();
        private MonitorArea _currentMonitor;
        private EdgeLock _activeLock;
        private bool _disposed;
        private int _delayMs;

        public StickyEdgeController(int delayMs)
        {
            _delayMs = Clamp(delayMs, 20, 1000);
            RefreshScreens();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        public bool Enabled { get; set; }

        public int DelayMs
        {
            get { return _delayMs; }
            set { _delayMs = Clamp(value, 20, 1000); }
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero)
            {
                return;
            }

            _hookProc = HookCallback;
            _hookId = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, _hookProc, NativeMethods.GetCurrentModuleHandle(), 0);
            if (_hookId == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                MessageBox.Show("Failed to install the mouse hook. Error code: " + error, "BoundaryTick", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void RefreshScreens()
        {
            lock (_sync)
            {
                var monitors = new List<MonitorArea>();
                var screens = Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    monitors.Add(new MonitorArea(i, screens[i].DeviceName, screens[i].Bounds));
                }

                _monitors = monitors;
                _currentMonitor = FindMonitor(Cursor.Position);
                _activeLock = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            RefreshScreens();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == new IntPtr(WM_MOUSEMOVE))
            {
                try
                {
                    var info = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                    if ((info.flags & LLMHF_INJECTED) == 0)
                    {
                        var point = new Point(info.pt.x, info.pt.y);
                        if (ShouldHoldAtBoundary(point))
                        {
                            return new IntPtr(1);
                        }
                    }
                }
                catch
                {
                    _activeLock = null;
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool ShouldHoldAtBoundary(Point nextPoint)
        {
            lock (_sync)
            {
                if (!Enabled || _monitors.Count < 2)
                {
                    _activeLock = null;
                    UpdateCurrentMonitor(nextPoint);
                    return false;
                }

                var targetMonitor = FindMonitor(nextPoint);
                if (_currentMonitor == null)
                {
                    _currentMonitor = targetMonitor;
                    return false;
                }

                if (_activeLock != null)
                {
                    if (!_activeLock.IsStillPushing(nextPoint))
                    {
                        _activeLock = null;
                        UpdateCurrentMonitor(nextPoint);
                        return false;
                    }

                    var elapsed = _clock.ElapsedMilliseconds - _activeLock.StartMs;
                    if (elapsed < _delayMs)
                    {
                        NativeMethods.SetCursorPos(_activeLock.ClampPoint.X, _activeLock.ClampPoint.Y);
                        return true;
                    }

                    _activeLock = null;
                    if (targetMonitor != null)
                    {
                        _currentMonitor = targetMonitor;
                    }
                    return false;
                }

                if (targetMonitor != null && targetMonitor.Index != _currentMonitor.Index)
                {
                    EdgeHit edgeHit;
                    if (TryCreateEdgeHit(_currentMonitor, targetMonitor, nextPoint, out edgeHit))
                    {
                        _activeLock = new EdgeLock(edgeHit, _clock.ElapsedMilliseconds);
                        NativeMethods.SetCursorPos(edgeHit.ClampPoint.X, edgeHit.ClampPoint.Y);
                        return true;
                    }

                    _currentMonitor = targetMonitor;
                    return false;
                }

                UpdateCurrentMonitor(nextPoint);
                return false;
            }
        }

        private void UpdateCurrentMonitor(Point point)
        {
            var monitor = FindMonitor(point);
            if (monitor != null)
            {
                _currentMonitor = monitor;
            }
        }

        private MonitorArea FindMonitor(Point point)
        {
            for (int i = 0; i < _monitors.Count; i++)
            {
                if (_monitors[i].Bounds.Contains(point))
                {
                    return _monitors[i];
                }
            }
            return null;
        }

        private static bool TryCreateEdgeHit(MonitorArea from, MonitorArea to, Point nextPoint, out EdgeHit edgeHit)
        {
            var fromBounds = from.Bounds;
            var toBounds = to.Bounds;

            if (fromBounds.Right == toBounds.Left)
            {
                int top;
                int bottom;
                if (TryGetVerticalOverlap(fromBounds, toBounds, out top, out bottom) && nextPoint.Y >= top && nextPoint.Y < bottom)
                {
                    edgeHit = new EdgeHit(from, to, EdgeDirection.Right, new Point(fromBounds.Right - 1, Clamp(nextPoint.Y, top, bottom - 1)), top, bottom);
                    return true;
                }
            }
            else if (fromBounds.Left == toBounds.Right)
            {
                int top;
                int bottom;
                if (TryGetVerticalOverlap(fromBounds, toBounds, out top, out bottom) && nextPoint.Y >= top && nextPoint.Y < bottom)
                {
                    edgeHit = new EdgeHit(from, to, EdgeDirection.Left, new Point(fromBounds.Left, Clamp(nextPoint.Y, top, bottom - 1)), top, bottom);
                    return true;
                }
            }
            else if (fromBounds.Bottom == toBounds.Top)
            {
                int left;
                int right;
                if (TryGetHorizontalOverlap(fromBounds, toBounds, out left, out right) && nextPoint.X >= left && nextPoint.X < right)
                {
                    edgeHit = new EdgeHit(from, to, EdgeDirection.Down, new Point(Clamp(nextPoint.X, left, right - 1), fromBounds.Bottom - 1), left, right);
                    return true;
                }
            }
            else if (fromBounds.Top == toBounds.Bottom)
            {
                int left;
                int right;
                if (TryGetHorizontalOverlap(fromBounds, toBounds, out left, out right) && nextPoint.X >= left && nextPoint.X < right)
                {
                    edgeHit = new EdgeHit(from, to, EdgeDirection.Up, new Point(Clamp(nextPoint.X, left, right - 1), fromBounds.Top), left, right);
                    return true;
                }
            }

            edgeHit = null;
            return false;
        }

        private static bool TryGetVerticalOverlap(Rectangle a, Rectangle b, out int top, out int bottom)
        {
            top = Math.Max(a.Top, b.Top);
            bottom = Math.Min(a.Bottom, b.Bottom);
            return bottom > top;
        }

        private static bool TryGetHorizontalOverlap(Rectangle a, Rectangle b, out int left, out int right)
        {
            left = Math.Max(a.Left, b.Left);
            right = Math.Min(a.Right, b.Right);
            return right > left;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }

    internal sealed class MonitorArea
    {
        public readonly int Index;
        public readonly string Name;
        public readonly Rectangle Bounds;

        public MonitorArea(int index, string name, Rectangle bounds)
        {
            Index = index;
            Name = name;
            Bounds = bounds;
        }
    }

    internal sealed class EdgeHit
    {
        public readonly MonitorArea From;
        public readonly MonitorArea To;
        public readonly EdgeDirection Direction;
        public readonly Point ClampPoint;
        public readonly int SharedStart;
        public readonly int SharedEnd;

        public EdgeHit(MonitorArea from, MonitorArea to, EdgeDirection direction, Point clampPoint, int sharedStart, int sharedEnd)
        {
            From = from;
            To = to;
            Direction = direction;
            ClampPoint = clampPoint;
            SharedStart = sharedStart;
            SharedEnd = sharedEnd;
        }
    }

    internal sealed class EdgeLock
    {
        private readonly EdgeHit _edgeHit;
        public readonly long StartMs;

        public EdgeLock(EdgeHit edgeHit, long startMs)
        {
            _edgeHit = edgeHit;
            StartMs = startMs;
        }

        public Point ClampPoint
        {
            get { return _edgeHit.ClampPoint; }
        }

        public bool IsStillPushing(Point point)
        {
            switch (_edgeHit.Direction)
            {
                case EdgeDirection.Right:
                    return point.X >= _edgeHit.From.Bounds.Right && point.Y >= _edgeHit.SharedStart && point.Y < _edgeHit.SharedEnd;
                case EdgeDirection.Left:
                    return point.X < _edgeHit.From.Bounds.Left && point.Y >= _edgeHit.SharedStart && point.Y < _edgeHit.SharedEnd;
                case EdgeDirection.Down:
                    return point.Y >= _edgeHit.From.Bounds.Bottom && point.X >= _edgeHit.SharedStart && point.X < _edgeHit.SharedEnd;
                case EdgeDirection.Up:
                    return point.Y < _edgeHit.From.Bounds.Top && point.X >= _edgeHit.SharedStart && point.X < _edgeHit.SharedEnd;
                default:
                    return false;
            }
        }
    }

    internal enum EdgeDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    internal static class NativeMethods
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        public static IntPtr GetCurrentModuleHandle()
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                using (var module = process.MainModule)
                {
                    if (module != null)
                    {
                        return GetModuleHandle(module.ModuleName);
                    }
                }
            }
            catch
            {
            }

            return IntPtr.Zero;
        }

        public static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }
    }
}
