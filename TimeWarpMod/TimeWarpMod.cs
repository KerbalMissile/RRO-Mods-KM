using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using RROML.Abstractions;

namespace TimeWarpMod
{
    public sealed class TimeWarpMod : IRromlMod
    {
        public string Id
        {
            get { return "TimeWarpMod"; }
        }

        public string Name
        {
            get { return "Time Warp Mod"; }
        }

        public string Version
        {
            get { return "0.2.0"; }
        }

        public void OnLoad(IModContext context)
        {
            var configPath = context.GetConfigPath("timewarp.json");
            var statePath = context.GetConfigPath("timewarp-state.json");
            var config = TimeWarpConfig.Load(configPath);
            context.Logger.Info("TimeWarpMod loaded. Hotkeys: Ctrl+F1=1x, Ctrl+F2=2x, Ctrl+F3=3x, Ctrl+F4=4x.");
            context.Logger.Info("TimeWarpMod dispatch path: native proxy time scaling.");

            var thread = new Thread(delegate() { RunLoop(context, config, statePath); });
            thread.IsBackground = true;
            thread.Name = "RROML TimeWarpMod";
            thread.Start();
        }

        private static void RunLoop(IModContext context, TimeWarpConfig config, string statePath)
        {
            var f1Down = false;
            var f2Down = false;
            var f3Down = false;
            var f4Down = false;
            var currentMultiplier = ReadCurrentMultiplier();
            WriteState(statePath, currentMultiplier, currentMultiplier, "Initialized", null);

            while (true)
            {
                try
                {
                    if (!config.Enabled)
                    {
                        Thread.Sleep(250);
                        continue;
                    }

                    var ctrlDown = !config.RequireCtrl || IsKeyDown(0x11);
                    ProcessHotkey(context, statePath, ref currentMultiplier, ctrlDown, 0x70, 1f, ref f1Down);
                    ProcessHotkey(context, statePath, ref currentMultiplier, ctrlDown, 0x71, 2f, ref f2Down);
                    ProcessHotkey(context, statePath, ref currentMultiplier, ctrlDown, 0x72, 3f, ref f3Down);
                    ProcessHotkey(context, statePath, ref currentMultiplier, ctrlDown, 0x73, 4f, ref f4Down);
                }
                catch (Exception exception)
                {
                    context.Logger.Error("TimeWarpMod loop failed.", exception);
                }

                Thread.Sleep(50);
            }
        }

        private static void ProcessHotkey(IModContext context, string statePath, ref float currentMultiplier, bool modifierActive, int virtualKey, float multiplier, ref bool keyWasDown)
        {
            var isDown = IsKeyDown(virtualKey);
            if (isDown && !keyWasDown && modifierActive)
            {
                ApplyMultiplier(context, statePath, ref currentMultiplier, multiplier);
            }

            keyWasDown = isDown;
        }

        private static void ApplyMultiplier(IModContext context, string statePath, ref float currentMultiplier, float multiplier)
        {
            var window = FindMainWindow(Process.GetCurrentProcess().Id);
            if (window == IntPtr.Zero)
            {
                context.Logger.Warn("TimeWarpMod could not find the game window.");
                WriteState(statePath, currentMultiplier, multiplier, "NoWindow", null);
                return;
            }

            if (NativeMethods.GetForegroundWindow() != window)
            {
                context.Logger.Warn("TimeWarpMod ignored hotkey because Railroads Online was not focused.");
                WriteState(statePath, currentMultiplier, multiplier, "GameNotFocused", null);
                return;
            }

            if (!NativeBridge.RROML_SetTimeScale(multiplier))
            {
                context.Logger.Error("TimeWarpMod failed to apply native time scale " + multiplier.ToString("0.0") + "x.");
                WriteState(statePath, currentMultiplier, multiplier, "NativeApplyFailed", null);
                return;
            }

            currentMultiplier = ReadCurrentMultiplier();
            context.Logger.Info("TimeWarpMod applied native time scale " + currentMultiplier.ToString("0.0") + "x.");
            WriteState(statePath, currentMultiplier, multiplier, "AppliedNativeScale", null);
        }

        private static float ReadCurrentMultiplier()
        {
            try
            {
                var scale = NativeBridge.RROML_GetTimeScale();
                if (scale <= 0d)
                {
                    return 1f;
                }

                return (float)scale;
            }
            catch
            {
                return 1f;
            }
        }

        private static void WriteState(string path, float currentMultiplier, float requestedMultiplier, string status, string command)
        {
            var serializer = new JavaScriptSerializer();
            var payload = new TimeWarpState
            {
                CurrentMultiplier = currentMultiplier,
                RequestedMultiplier = requestedMultiplier,
                Status = status,
                Command = command,
                UpdatedAtUtc = DateTime.UtcNow.ToString("o")
            };
            File.WriteAllText(path, serializer.Serialize(payload));
        }

        private static IntPtr FindMainWindow(int processId)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr handle, IntPtr lParam)
            {
                int windowProcessId;
                NativeMethods.GetWindowThreadProcessId(handle, out windowProcessId);
                if (windowProcessId != processId || !NativeMethods.IsWindowVisible(handle))
                {
                    return true;
                }

                if (NativeMethods.GetWindow(handle, 4) != IntPtr.Zero)
                {
                    return true;
                }

                found = handle;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }
    }

    internal sealed class TimeWarpConfig
    {
        public bool Enabled { get; set; }
        public bool RequireCtrl { get; set; }
        public string CommandName { get; set; }
        public ushort OpenConsoleVirtualKey { get; set; }
        public int OpenConsoleDelayMs { get; set; }

        public TimeWarpConfig()
        {
            Enabled = true;
            RequireCtrl = true;
            CommandName = "slomo";
            OpenConsoleVirtualKey = 0xC0;
            OpenConsoleDelayMs = 75;
        }

        public static TimeWarpConfig Load(string path)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                if (!File.Exists(path))
                {
                    var created = new TimeWarpConfig();
                    File.WriteAllText(path, serializer.Serialize(created));
                    return created;
                }

                var loaded = serializer.Deserialize<TimeWarpConfig>(File.ReadAllText(path));
                return loaded ?? new TimeWarpConfig();
            }
            catch
            {
                return new TimeWarpConfig();
            }
        }
    }

    internal sealed class TimeWarpState
    {
        public float CurrentMultiplier { get; set; }
        public float RequestedMultiplier { get; set; }
        public string Status { get; set; }
        public string Command { get; set; }
        public string UpdatedAtUtc { get; set; }
    }

    internal static class NativeBridge
    {
        [DllImport("XINPUT1_3.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        public static extern bool RROML_SetTimeScale(double scale);

        [DllImport("XINPUT1_3.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        public static extern double RROML_GetTimeScale();
    }

    internal static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint command);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);
    }
}
