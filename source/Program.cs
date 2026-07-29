using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;

namespace screenshot
{
    static class Program
    {
        [DllImport("user32.dll")] 
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        private static HiddenForm? hiddenForm;

        [STAThread]
        static void Main(string[] args)
        {
            if (!IsRunAsAdmin())
            {
                try
                {
                    ProcessStartInfo procInfo = new ProcessStartInfo();
                    procInfo.UseShellExecute = true;
                    procInfo.FileName = Environment.ProcessPath;
                    procInfo.Verb = "runas";
                    
                    if (args.Length > 0)
                    {
                        procInfo.Arguments = string.Join(" ", args);
                    }
                    
                    Process.Start(procInfo);
                }
                catch { }
                return;
            }

            if (args.Length > 0 && args[0] == "--uninstall")
            {
                KillOldInstances();
                RunSelfUninstall();
                Environment.Exit(0);
                return;
            }
			
            KillOldInstances();
            RegisterInInstalledApps();
			
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            hiddenForm = new HiddenForm();
            Application.Run();
        }
        private static void RegisterInInstalledApps()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                string appDir = Path.GetDirectoryName(exePath) ?? "";
                long sizeInKb = new FileInfo(exePath).Length / 1024;
                
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\limon"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "limon");
                        key.SetValue("Publisher", "b1no");
                        key.SetValue("DisplayIcon", exePath);
                        key.SetValue("DisplayVersion", "4.4");
                        key.SetValue("InstallLocation", appDir);
                        key.SetValue("EstimatedSize", (int)sizeInKb, RegistryValueKind.DWord);
                        key.SetValue("URLInfoAbout", "https://github.com/Storinob/limon", RegistryValueKind.String);
                        
                        string uninstallCommand = $"\"{exePath}\" --uninstall";
                        key.SetValue("UninstallString", uninstallCommand, RegistryValueKind.String);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch
            {
				
			}
        }
        private static void RunSelfUninstall()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                string processName = Path.GetFileName(exePath);
                string batchPath = Path.Combine(Path.GetTempPath(), "limon-uninstaller.bat");

                string batContent = 
                    "@echo off\r\n" +
                    ":loop\r\n" +
                    $"taskkill /f /im \"{processName}\" >nul 2>&1\r\n" +
                    "timeout /t 2 >nul\r\n" +
                    "del /f /q \"%~1\" >nul 2>&1\r\n" +
                    "if exist \"%~1\" (\r\n" +
                    "    goto loop\r\n" +
                    ")\r\n" +
                    "reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\limon\" /f >nul 2>&1\r\n" +
                    "del \"%~f0\"\r\n";

                File.WriteAllText(batchPath, batContent, System.Text.Encoding.Default);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = batchPath,
                    Arguments = $"\"{exePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
            }
            catch { }
        }
        private static bool IsRunAsAdmin()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }
        private static void KillOldInstances()
        {
            Process currentProcess = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(currentProcess.ProcessName);
            
            foreach (Process p in processes)
            {
                if (p.Id != currentProcess.Id)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                    catch { }
                }
            }
        }
    }

    class HiddenForm : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_PRINTSCREEN = 0x002C;

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const uint SRCCOPY = 0x40CC0020;

        public HiddenForm()
        {
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Width = 0;
            this.Height = 0;

            Program.RegisterHotKey(this.Handle, 1, MOD_NONE, VK_PRINTSCREEN);
            Program.RegisterHotKey(this.Handle, 2, MOD_SHIFT, VK_PRINTSCREEN);
            Program.RegisterHotKey(this.Handle, 3, MOD_CONTROL, VK_PRINTSCREEN);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) CaptureArea();
                else if (id == 2) CaptureFullScreen();
                else if (id == 3) StartPicker();
            }
            base.WndProc(ref m);
        }

        private string GenerateRandomName()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private string GetSavePath()
        {
            string picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string monthFolder = DateTime.Now.ToString("MMMM_yyyy", CultureInfo.InvariantCulture).ToLower();
            string targetDir = Path.Combine(picturesDir, "screenshots", monthFolder);
            
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            
            string randomName = GenerateRandomName();
            return Path.Combine(targetDir, $"{randomName}.png");
        }

        private Bitmap CaptureScreenWithBitBlt(Rectangle bounds)
		{
			IntPtr hdcScreen = GetDC(IntPtr.Zero);
			IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
			IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, bounds.Width, bounds.Height);
			IntPtr hOld = SelectObject(hdcMem, hBitmap);

			BitBlt(hdcMem, 0, 0, bounds.Width, bounds.Height, hdcScreen, bounds.X, bounds.Y, SRCCOPY);
			Bitmap rawBmp = Image.FromHbitmap(hBitmap);

			SelectObject(hdcMem, hOld);
			DeleteDC(hdcMem);
			ReleaseDC(IntPtr.Zero, hdcScreen);
			DeleteObject(hBitmap);

			Bitmap finalBmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(finalBmp))
			{
				g.Clear(Color.Transparent);
				
				foreach (Screen screen in Screen.AllScreens)
				{
					Rectangle intersect = Rectangle.Intersect(screen.Bounds, bounds);
					if (!intersect.IsEmpty)
					{
						Rectangle rect = new Rectangle(intersect.X - bounds.X, intersect.Y - bounds.Y, intersect.Width, intersect.Height);
						g.DrawImage(rawBmp, rect, rect, GraphicsUnit.Pixel);
					}
				}
			}
			
			rawBmp.Dispose();
			return finalBmp;
		}

        private void SaveAndCopy(Bitmap bmp)
        {
            string savePath = GetSavePath();
            bmp.Save(savePath, ImageFormat.Png);
            
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                byte[] buffer = ms.ToArray();
                
                DataObject dataObject = new DataObject();
                dataObject.SetData("PNG", false, new MemoryStream(buffer));
                dataObject.SetData(DataFormats.Bitmap, true, bmp);
                Clipboard.SetDataObject(dataObject, true);
            }

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream? soundStream = assembly.GetManifestResourceStream("screenshot.done.wav"))
                {
                    if (soundStream != null)
                    {
                        using (System.Media.SoundPlayer player = new System.Media.SoundPlayer(soundStream))
                        {
                            player.Play(); 
                        }
                    }
                }
            }
            catch
            {
                // звук
            }

            bmp.Dispose();
        }

        private void CaptureFullScreen()
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            Bitmap bmp = CaptureScreenWithBitBlt(bounds);
            SaveAndCopy(bmp);
        }

        private void CaptureArea()
		{
			Rectangle bounds = SystemInformation.VirtualScreen;
			Bitmap screenShot = CaptureScreenWithBitBlt(bounds);
			
			using (var overlay = new OverlayForm(screenShot, false))
			{
				if (overlay.ShowDialog() == DialogResult.OK && overlay.SelectedArea.Width > 5 && overlay.SelectedArea.Height > 5)
				{
					Bitmap cropped = new Bitmap(overlay.SelectedArea.Width, overlay.SelectedArea.Height, PixelFormat.Format32bppArgb);
					using (Graphics g = Graphics.FromImage(cropped))
					{
						g.InterpolationMode = InterpolationMode.NearestNeighbor;
						g.PixelOffsetMode = PixelOffsetMode.HighQuality;
						g.CompositingQuality = CompositingQuality.HighQuality;
						g.CompositingMode = CompositingMode.SourceCopy;

						g.Clear(Color.Transparent); 
						g.DrawImage(screenShot, new Rectangle(0, 0, cropped.Width, cropped.Height), overlay.SelectedArea, GraphicsUnit.Pixel);
					}
					SaveAndCopy(cropped);
				}
			}
			screenShot.Dispose();
		}

        private void StartPicker()
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            Bitmap screenShot = CaptureScreenWithBitBlt(bounds);
            
            using (var overlay = new OverlayForm(screenShot, true))
            {
                if (overlay.ShowDialog() == DialogResult.OK)
                {
                    Clipboard.SetText(overlay.SelectedColorHex);
                }
            }
            screenShot.Dispose();
        }
    }

    abstract class DrawAction
    {
        public abstract void Draw(Graphics g);
    }

    class PenAction : DrawAction
    {
        public List<Point> Points { get; } = new List<Point>();
        public Color Color { get; set; } = Color.Red;
        public float Width { get; set; } = 3f;

        public override void Draw(Graphics g)
        {
            if (Points.Count < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Color, Width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLines(pen, Points.ToArray());
            }
        }
    }

    class RectAction : DrawAction
    {
        public Rectangle Rect { get; set; }
        public Color Color { get; set; } = Color.Maroon;

        public override void Draw(Graphics g)
        {
            if (Rect.Width <= 0 || Rect.Height <= 0) return;
            using (SolidBrush brush = new SolidBrush(Color))
            {
                g.FillRectangle(brush, Rect);
            }
        }
    }

    class HollowRectAction : DrawAction
    {
        public Rectangle Rect { get; set; }
        public Color Color { get; set; } = Color.Red;
        public float Width { get; set; } = 3f;

        public override void Draw(Graphics g)
        {
            if (Rect.Width <= 0 || Rect.Height <= 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Color, Width) { LineJoin = LineJoin.Round })
            {
                g.DrawRectangle(pen, Rect);
            }
        }
    }

    class OverlayForm : Form
    {
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP   = 0x0205;
        
        private readonly Bitmap background;
        private readonly bool isColorPicker;
        
        private readonly byte[] backgroundPixels;
        private readonly int backgroundStride;

        private readonly List<DrawAction> actions = new List<DrawAction>();
        private PenAction? currentPenAction;
        private Point startPoint;
        
        private bool isDrawing = false;
        private bool isDrawingWithPen = false;
        private bool isDrawingRectangle = false; 
        private bool isDrawingHollowRectangle = false; 
        
        private bool isCtrlPressed = false;
        private bool isAltPressed = false;        
        private bool isShiftPressed = false; 
        
        private Rectangle currentAltRect;         
        private Rectangle currentShiftRect; 

        public Rectangle SelectedArea { get; private set; }
        public string SelectedColorHex { get; private set; } = "#000000";
        private Color currentMouseColor = Color.Black;
        private Point mousePos;

        private readonly SolidBrush dimOverlayBrush = new SolidBrush(Color.FromArgb(60, Color.Black));
        private readonly SolidBrush dimSelectedBrush = new SolidBrush(Color.FromArgb(100, Color.Black));
        private readonly Pen cyanPen = new Pen(Color.Cyan, 1);
        private readonly SolidBrush maroonBrush = new SolidBrush(Color.Maroon);
        private readonly Pen whitePen = new Pen(Color.White, 1);
        private readonly Pen redPen = new Pen(Color.Red, 3f) { LineJoin = LineJoin.Round };

        public OverlayForm(Bitmap bg, bool colorPicker)
        {
            this.background = bg;
            this.isColorPicker = colorPicker;
			
			this.BackColor = Color.Black;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = SystemInformation.VirtualScreen;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Cross;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.KeyPreview = true; 
            this.TransparencyKey = Color.Empty;

            Rectangle rect = new Rectangle(0, 0, bg.Width, bg.Height);
            BitmapData bmpData = bg.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            backgroundStride = bmpData.Stride;
            backgroundPixels = new byte[Math.Abs(bmpData.Stride) * bg.Height];
            Marshal.Copy(bmpData.Scan0, backgroundPixels, 0, backgroundPixels.Length);
            bg.UnlockBits(bmpData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dimOverlayBrush.Dispose();
                dimSelectedBrush.Dispose();
                cyanPen.Dispose();
                maroonBrush.Dispose();
                whitePen.Dispose();
                redPen.Dispose();
            }
            base.Dispose(disposing);
        }

        private Color GetPixelColor(int x, int y)
        {
            if (x < 0 || x >= background.Width || y < 0 || y >= background.Height)
                return Color.Black;

            int index = y * backgroundStride + x * 4;
            byte b = backgroundPixels[index];
            byte g = backgroundPixels[index + 1];
            byte r = backgroundPixels[index + 2];
            
            return Color.FromArgb(r, g, b);
        }

        private void UndoLastAction()
        {
            if (actions.Count > 0)
            {
                actions.RemoveAt(actions.Count - 1);
                this.Invalidate();
            }
        }

        private void BakeActionsToBackground()
        {
            if (actions.Count == 0 || background == null) return;
            using (Graphics g = Graphics.FromImage(background))
            {
                foreach (var action in actions)
                {
                    action.Draw(g);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control) { isCtrlPressed = true; this.Invalidate(); }
            if (e.Alt) { isAltPressed = true; this.Invalidate(); }
            if (e.Shift) { isShiftPressed = true; this.Invalidate(); } 
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!e.Control) { isCtrlPressed = false; this.Invalidate(); }
            if (!e.Alt) { isAltPressed = false; this.Invalidate(); }
            if (!e.Shift) { isShiftPressed = false; this.Invalidate(); } 
            base.OnKeyUp(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_RBUTTONDOWN)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_RBUTTONUP)
            {
                if (isDrawing || SelectedArea.Width > 0 || isDrawingRectangle || isDrawingHollowRectangle)
                {
                    isDrawing = false;
                    isDrawingRectangle = false;
                    isDrawingHollowRectangle = false; 
                    SelectedArea = Rectangle.Empty;
                    currentAltRect = Rectangle.Empty;
                    currentShiftRect = Rectangle.Empty; 
                    this.Invalidate();
                }
                else
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (background == null) return;
            
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(background, 0, 0);
            
            foreach (var action in actions)
            {
                action.Draw(e.Graphics);
            }

            if (isColorPicker)
            {
                using (SolidBrush brush = new SolidBrush(currentMouseColor))
                {
                    e.Graphics.FillRectangle(brush, mousePos.X + 15, mousePos.Y + 15, 20, 20);
                    e.Graphics.DrawRectangle(whitePen, mousePos.X + 15, mousePos.Y + 15, 20, 20);
                }
            }
            else if (isDrawingRectangle && currentAltRect.Width > 0 && currentAltRect.Height > 0)
            {
                e.Graphics.FillRectangle(maroonBrush, currentAltRect);
            }
            else if (isDrawingHollowRectangle && currentShiftRect.Width > 0 && currentShiftRect.Height > 0)
            {
                e.Graphics.DrawRectangle(redPen, currentShiftRect);
            }
            else if (isDrawing || SelectedArea.Width > 0)
            {
                e.Graphics.FillRectangle(dimSelectedBrush, 0, 0, this.Width, SelectedArea.Top);
                e.Graphics.FillRectangle(dimSelectedBrush, 0, SelectedArea.Top, SelectedArea.Left, SelectedArea.Height);
                e.Graphics.FillRectangle(dimSelectedBrush, SelectedArea.Right, SelectedArea.Top, this.Width - SelectedArea.Right, SelectedArea.Height);
                e.Graphics.FillRectangle(dimSelectedBrush, 0, SelectedArea.Bottom, this.Width, this.Height - SelectedArea.Bottom);

                e.Graphics.DrawRectangle(cyanPen, SelectedArea);
            }
            else
            {
                if (!isCtrlPressed && !isAltPressed && !isShiftPressed)
                {
                    e.Graphics.FillRectangle(dimOverlayBrush, 0, 0, this.Width, this.Height);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mousePos = e.Location;
            if (background == null) return;

            if (isColorPicker)
            {
                currentMouseColor = GetPixelColor(e.X, e.Y);
                this.Invalidate();
            }
            else if (isDrawingWithPen && currentPenAction != null)
            {
                currentPenAction.Points.Add(e.Location);
                this.Invalidate();
            }
            else if (isDrawingRectangle)
            {
                int x = Math.Min(startPoint.X, e.X);
                int y = Math.Min(startPoint.Y, e.Y);
                int width = Math.Abs(startPoint.X - e.X);
                int height = Math.Abs(startPoint.Y - e.Y);
                currentAltRect = new Rectangle(x, y, width, height);
                this.Invalidate();
            }
            else if (isDrawingHollowRectangle)
            {
                int x = Math.Min(startPoint.X, e.X);
                int y = Math.Min(startPoint.Y, e.Y);
                int width = Math.Abs(startPoint.X - e.X);
                int height = Math.Abs(startPoint.Y - e.Y);
                currentShiftRect = new Rectangle(x, y, width, height);
                this.Invalidate();
            }
            else if (isDrawing)
            {
                int x = Math.Min(startPoint.X, e.X);
                int y = Math.Min(startPoint.Y, e.Y);
                int width = Math.Abs(startPoint.X - e.X);
                int height = Math.Abs(startPoint.Y - e.Y);
                SelectedArea = new Rectangle(x, y, width, height);
                this.Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && background != null)
            {
                if (isColorPicker)
                {
                    Color c = GetPixelColor(e.X, e.Y);
                    SelectedColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (isCtrlPressed)
                {
                    isDrawingWithPen = true;
                    currentPenAction = new PenAction();
                    currentPenAction.Points.Add(e.Location);
                    actions.Add(currentPenAction);
                }
                else if (isAltPressed)
                {
                    isDrawingRectangle = true;
                    startPoint = e.Location;
                    currentAltRect = Rectangle.Empty;
                }
                else if (isShiftPressed)
                {
                    isDrawingHollowRectangle = true;
                    startPoint = e.Location;
                    currentShiftRect = Rectangle.Empty;
                }
                else
                {
                    isDrawing = true;
                    startPoint = e.Location;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (isDrawingWithPen)
                {
                    isDrawingWithPen = false;
                    if (currentPenAction != null && currentPenAction.Points.Count == 1)
                    {
                        currentPenAction.Points.Add(currentPenAction.Points[0]);
                    }
                    currentPenAction = null;
                }
                else if (isDrawingRectangle)
                {
                    isDrawingRectangle = false;
                    if (currentAltRect.Width > 0 && currentAltRect.Height > 0)
                    {
                        actions.Add(new RectAction { Rect = currentAltRect, Color = Color.Maroon });
                    }
                    currentAltRect = Rectangle.Empty;
                    this.Invalidate();
                }
                else if (isDrawingHollowRectangle)
                {
                    isDrawingHollowRectangle = false;
                    if (currentShiftRect.Width > 0 && currentShiftRect.Height > 0)
                    {
                        actions.Add(new HollowRectAction { Rect = currentShiftRect, Color = Color.Red, Width = 3f });
                    }
                    currentShiftRect = Rectangle.Empty;
                    this.Invalidate();
                }
                else if (!isColorPicker && isDrawing)
                {
                    isDrawing = false;
                    if (SelectedArea.Width > 5 && SelectedArea.Height > 5)
                    {
                        BakeActionsToBackground();
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            if (keyData == Keys.Back || keyData == (Keys.Control | Keys.Z))
            {
                UndoLastAction();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}