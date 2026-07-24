using Assistant.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;


namespace Assistant
{
    internal class ScreenCapManager
    {
        private static readonly TimerCallback m_DoCaptureCall = new(CaptureNow);
        private static readonly object m_CaptureLock = new();
        private static readonly object m_ListLock = new();

        internal static readonly string[] ScreenshotExtensions =
        {
            "jpeg", "jpg", "png", "bmp", "gif", "tiff", "tif"
        };

        private static readonly HashSet<string> m_ScreenshotExtensionSet =
            new(ScreenshotExtensions, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> m_ReservedFileNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

        public static void Initialize()
        {
        }

        internal static void DeathCapture(double delay)
        {
            Timer.DelayedCallback(TimeSpan.FromSeconds(delay), m_DoCaptureCall).Start();
        }

        public static Image CaptureWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !User32.IsWindow(handle))
                throw new InvalidOperationException("未找到有效的 UO 窗口。");

            if (!User32.IsWindowVisible(handle) || IsWindowCloaked(handle))
                throw new InvalidOperationException("UO 窗口当前不可见，无法截图。");

            if (User32.IsIconic(handle))
                throw new InvalidOperationException("UO 窗口已最小化，无法截图。");

            Rectangle bounds = GetWindowBounds(handle);
            Bitmap windowImage = CaptureWindowContent(handle, bounds.Size);
            return windowImage ?? CaptureBounds(bounds);
        }

        internal static void CaptureNow()
        {
            CaptureNowPath();
        }
        internal static string CaptureNowPath()
        {
            string filename = null;
            string pathToDisplay = null;
            Exception failure = null;

            lock (m_CaptureLock)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string playerName = MakeSafeFileName(World.Player?.Name);
                    string path = EnsureCapturePath(out bool pathChanged);
                    if (pathChanged)
                        pathToDisplay = path;

                    string type = NormalizeFormat(RazorEnhanced.Settings.General.ReadString("ImageFormat"));
                    filename = CreateUniqueFileName(path, playerName, now, type);

                    IntPtr handle = Client.Instance?.GetWindowHandle() ?? IntPtr.Zero;
                    bool fullScreen = RazorEnhanced.Settings.General.ReadBool("CapFullScreen");

                    using (Image image = fullScreen ? CaptureFullScreen(handle) : CaptureWindow(handle))
                    {
                        if (RazorEnhanced.Settings.General.ReadBool("CapTimeStamp"))
                            DrawTimestamp(image, playerName, now);

                        try
                        {
                            SaveImageAtomic(image, filename, GetFormat(type));
                        }
                        catch (Exception firstSaveError)
                        {
                            string fallbackPath = Assistant.Engine.RootPath;
                            if (string.Equals(
                                Path.GetFullPath(path),
                                Path.GetFullPath(fallbackPath),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                throw;
                            }

                            Utility.Logger.Warn(
                                firstSaveError,
                                "无法写入截图目录 {0}，将重试 RA 目录。",
                                path);

                            Directory.CreateDirectory(fallbackPath);
                            RazorEnhanced.Settings.General.WriteString("CapPath", fallbackPath);
                            pathToDisplay = fallbackPath;
                            filename = CreateUniqueFileName(fallbackPath, playerName, now, type);
                            SaveImageAtomic(image, filename, GetFormat(type));
                        }
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }

            UpdateScreenshotUi(pathToDisplay, failure == null);

            if (failure != null)
            {
                Utility.Logger.Error(failure, "截图失败，目标文件: {0}", filename ?? "(未创建)");
                try
                {
                    RazorEnhanced.Misc.SendMessage($"截图失败: {failure.Message}", 33, false);
                }
                catch
                {
                }
                return string.Empty;
            }

            return filename;
        }

        private static ImageFormat GetFormat(string fmt)
        {
            switch (fmt)
            {
                case "png":
                    return ImageFormat.Png;
                case "bmp":
                    return ImageFormat.Bmp;
                case "gif":
                    return ImageFormat.Gif;
                case "tif":
                    return ImageFormat.Tiff;
                default:
                    return ImageFormat.Jpeg;
            }
        }

        internal static void DisplayTo(ListBox lb)
        {
            string path = EnsureCapturePath(out bool pathChanged);
            if (pathChanged)
                UpdateScreenshotUi(path, false);

            if (lb.InvokeRequired)
            {
                lb.BeginInvoke(new MethodInvoker(() => PopulateList(lb, path)));
            }
            else
            {
                PopulateList(lb, path);
            }
        }

        private static void PopulateList(ListBox list, string path)
        {
            lock (m_ListLock)
            {
                List<FileInfo> files = new();
                try
                {
                    foreach (string file in GetScreenshotFiles(path))
                        files.Add(new FileInfo(file));
                }
                catch (Exception ex)
                {
                    Utility.Logger.Warn(ex, "无法读取截图目录 {0}。", path);
                }

                files.Sort((left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));

                list.BeginUpdate();
                try
                {
                    list.Items.Clear();
                    for (int i = 0; i < files.Count && i < 500; i++)
                        list.Items.Add(files[i].Name);
                }
                finally
                {
                    list.EndUpdate();
                }
            }
        }

        internal static List<string> GetScreenshotFiles(string path)
        {
            List<string> screenshots = new();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return screenshots;

            foreach (string file in Directory.GetFiles(path))
            {
                string extension = Path.GetExtension(file);
                if (extension.Length > 1 && m_ScreenshotExtensionSet.Contains(extension.Substring(1)))
                    screenshots.Add(file);
            }

            return screenshots;
        }

        private static Image CaptureFullScreen(IntPtr handle)
        {
            Rectangle bounds = handle != IntPtr.Zero && User32.IsWindow(handle)
                ? Screen.FromHandle(handle).Bounds
                : Screen.PrimaryScreen.Bounds;

            return CaptureBounds(bounds);
        }

        private static Bitmap CaptureWindowContent(IntPtr handle, Size size)
        {
            if (size.Width <= 0 || size.Height <= 0 || User32.IsHungAppWindow(handle))
                return null;

            Bitmap bitmap = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
            try
            {
                bool success;
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    IntPtr deviceContext = graphics.GetHdc();
                    try
                    {
                        success = User32.PrintWindow(
                            handle,
                            deviceContext,
                            User32.PW_RENDERFULLCONTENT);
                    }
                    finally
                    {
                        graphics.ReleaseHdc(deviceContext);
                    }
                }

                if (!success || IsLikelyBlank(bitmap))
                {
                    bitmap.Dispose();
                    return null;
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                return null;
            }
        }

        private static bool IsLikelyBlank(Bitmap bitmap)
        {
            int xStep = Math.Max(1, bitmap.Width / 24);
            int yStep = Math.Max(1, bitmap.Height / 24);
            int startY = Math.Min(bitmap.Height - 1, Math.Max(0, bitmap.Height / 12));
            HashSet<int> colors = new();
            int visiblePixels = 0;

            for (int y = startY; y < bitmap.Height; y += yStep)
            {
                for (int x = 0; x < bitmap.Width; x += xStep)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.A > 8)
                        visiblePixels++;

                    colors.Add(color.ToArgb());
                    if (visiblePixels > 8 && colors.Count > 3)
                        return false;
                }
            }

            return visiblePixels == 0 || colors.Count <= 3;
        }

        private static Bitmap CaptureBounds(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("截图区域尺寸无效。");

            Rectangle visibleBounds = Rectangle.Intersect(bounds, SystemInformation.VirtualScreen);
            if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
                throw new InvalidOperationException("截图区域不在可见屏幕内。");

            Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    Point destination = new(
                        visibleBounds.Left - bounds.Left,
                        visibleBounds.Top - bounds.Top);
                    graphics.CopyFromScreen(
                        visibleBounds.Location,
                        destination,
                        visibleBounds.Size,
                        CopyPixelOperation.SourceCopy);
                }

                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        private static Rectangle GetWindowBounds(IntPtr handle)
        {
            User32.RECT rect;
            if (!User32.GetWindowRect(handle, out rect))
                throw new InvalidOperationException("无法读取 UO 窗口位置。");

            Rectangle bounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("UO 窗口尺寸无效。");

            return bounds;
        }

        private static bool IsWindowCloaked(IntPtr handle)
        {
            try
            {
                return DwmApi.DwmGetWindowAttribute(
                    handle,
                    DwmApi.DWMWA_CLOAKED,
                    out int cloaked,
                    sizeof(int)) == 0 && cloaked != 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static string EnsureCapturePath(out bool pathChanged)
        {
            pathChanged = false;
            string configuredPath = RazorEnhanced.Settings.General.ReadString("CapPath");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                try
                {
                    Directory.CreateDirectory(configuredPath);
                    return configuredPath;
                }
                catch (Exception ex)
                {
                    Utility.Logger.Warn(ex, "无法使用截图目录 {0}，将回退到 RA 目录。", configuredPath);
                }
            }

            string fallbackPath = Assistant.Engine.RootPath;
            Directory.CreateDirectory(fallbackPath);
            RazorEnhanced.Settings.General.WriteString("CapPath", fallbackPath);
            pathChanged = true;
            return fallbackPath;
        }

        private static string NormalizeFormat(string format)
        {
            string normalized = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            switch (normalized)
            {
                case "jpeg":
                case "jpg":
                    return "jpg";
                case "png":
                case "bmp":
                case "gif":
                    return normalized;
                case "tiff":
                case "tif":
                    return "tif";
                default:
                    RazorEnhanced.Settings.General.WriteString("ImageFormat", "jpg");
                    return "jpg";
            }
        }

        private static string MakeSafeFileName(string name)
        {
            string safeName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalidChar, '_');

            safeName = safeName.TrimEnd(' ', '.');
            string deviceName = safeName.Split('.')[0];
            if (m_ReservedFileNames.Contains(deviceName))
                safeName = $"_{safeName}";

            const int maxPlayerNameLength = 80;
            if (safeName.Length > maxPlayerNameLength)
                safeName = safeName.Substring(0, maxPlayerNameLength).TrimEnd(' ', '.');

            return string.IsNullOrWhiteSpace(safeName) ? "Unknown" : safeName;
        }

        private static string CreateUniqueFileName(
            string path,
            string playerName,
            DateTime timestamp,
            string type)
        {
            string stem = $"{playerName}_{timestamp:yyyy-MM-dd_HH.mm.ss}";
            string filename = Path.Combine(path, $"{stem}.{type}");
            int suffix = 1;

            while (File.Exists(filename))
            {
                filename = Path.Combine(path, $"{stem}-{suffix}.{type}");
                suffix++;
            }

            return filename;
        }

        private static void DrawTimestamp(Image image, string playerName, DateTime timestamp)
        {
            string shardName = string.IsNullOrWhiteSpace(World.ShardName) ? "Unknown" : World.ShardName;
            string text = $"{playerName} ({shardName}) - {timestamp:yyyy-MM-dd HH:mm:ss}";

            using (Graphics graphics = Graphics.FromImage(image))
            using (SolidBrush background = new(Color.FromArgb(180, Color.Black)))
            using (SolidBrush foreground = new(Color.White))
            {
                Font font = SystemFonts.MessageBoxFont;
                SizeF textSize = graphics.MeasureString(text, font);
                float x = 8;
                float y = Math.Max(8, image.Height - textSize.Height - 16);
                RectangleF backgroundRect = new(
                    x - 4,
                    y - 3,
                    Math.Min(textSize.Width + 8, image.Width - x),
                    textSize.Height + 6);

                graphics.FillRectangle(background, backgroundRect);
                graphics.DrawString(text, font, foreground, x, y);
            }
        }

        private static void SaveImageAtomic(Image image, string filename, ImageFormat format)
        {
            string temporaryFile = $"{filename}.{Guid.NewGuid():N}.tmp";
            try
            {
                image.Save(temporaryFile, format);
                if (!File.Exists(temporaryFile) || new FileInfo(temporaryFile).Length == 0)
                    throw new IOException("截图文件为空。");

                File.Move(temporaryFile, filename);
            }
            finally
            {
                if (File.Exists(temporaryFile))
                {
                    try
                    {
                        File.Delete(temporaryFile);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void UpdateScreenshotUi(string path, bool reloadList)
        {
            try
            {
                MainForm mainWindow = Engine.MainWindow;
                if (mainWindow == null ||
                    mainWindow.IsDisposed ||
                    mainWindow.Disposing ||
                    !mainWindow.IsHandleCreated)
                {
                    return;
                }

                mainWindow.SafeAction(window =>
                {
                    if (window.IsDisposed || window.Disposing)
                        return;

                    if (path != null)
                        window.ScreenPath.Text = path;

                    if (reloadList)
                        window.ReloadScreenShotsList();
                });
            }
            catch (InvalidOperationException)
            {
                // The UI can disappear while a delayed death capture is finishing.
            }
        }

        private static class DwmApi
        {
            internal const int DWMWA_CLOAKED = 14;

            [DllImport("dwmapi.dll")]
            internal static extern int DwmGetWindowAttribute(
                IntPtr hwnd,
                int attribute,
                out int value,
                int valueSize);
        }

        /// <summary>
        /// Helper class containing User32 API functions
        /// </summary>
        private class User32
        {
            internal const uint PW_RENDERFULLCONTENT = 0x00000002;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsHungAppWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);
        }
    }
}
