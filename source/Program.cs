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
using System.Text;

namespace screenshot
{
    static class Program
    {
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        private static HiddenForm? hiddenForm;

        [STAThread]
        static void Main()
        {
            if (!IsRunAsAdmin())
            {
                try
                {
                    ProcessStartInfo procInfo = new ProcessStartInfo();
                    procInfo.UseShellExecute = true;
                    procInfo.FileName = Environment.ProcessPath;
                    procInfo.Verb = "runas";

                    Process.Start(procInfo);
                }
                catch
                {
                    // а
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            hiddenForm = new HiddenForm();
            Application.Run();
        }

        private static bool IsRunAsAdmin()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
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

        private static readonly Random random = new Random();
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

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
            StringBuilder result = new StringBuilder(8);
            for (int i = 0; i < 8; i++)
            {
                result.Append(Chars[random.Next(Chars.Length)]);
            }
            return result.ToString();
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

        private void SanitizeBitmapPixels(Bitmap bmp)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbValues = new byte[bytes];

            Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int counter = 0; counter < rgbValues.Length; counter += 4)
            {
                byte b = rgbValues[counter];
                byte g = rgbValues[counter + 1];
                byte r = rgbValues[counter + 2];

                rgbValues[counter + 3] = 255; 

                if (r <= 10 && g <= 10 && b <= 10)
                {
                    rgbValues[counter] = 0;     
                    rgbValues[counter + 1] = 0; 
                    rgbValues[counter + 2] = 0; 
                }
            }

            Marshal.Copy(rgbValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);
        }

        private void SaveAndCopy(Bitmap bmp)
        {
            SanitizeBitmapPixels(bmp);

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
                // чтоб не падать изза звук
            }

            bmp.Dispose();
            GC.Collect();
        }

        private void CaptureFullScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
            Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            SaveAndCopy(bmp);
        }

        private void CaptureArea()
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
            Bitmap screenShot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(screenShot))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            SanitizeBitmapPixels(screenShot);

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
                        g.DrawImage(screenShot, new Rectangle(0, 0, cropped.Width, cropped.Height), overlay.SelectedArea, GraphicsUnit.Pixel);
                    }
                    SaveAndCopy(cropped);
                }
            }
            screenShot.Dispose();
        }

        private void StartPicker()
        {
            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
            Bitmap screenShot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(screenShot))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            SanitizeBitmapPixels(screenShot);

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

    class OverlayForm : Form
    {
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP   = 0x0205;

        private readonly Bitmap background;
        private readonly bool isColorPicker;
        
        private readonly List<DrawAction> actions = new List<DrawAction>();
        private PenAction? currentPenAction;

        private Point startPoint;
        private bool isDrawing = false;
        private bool isDrawingWithPen = false;
        private bool isDrawingRectangle = false; 
        private bool isCtrlPressed = false;
        private bool isAltPressed = false;        
        
        private Rectangle currentAltRect;         
        public Rectangle SelectedArea { get; private set; }
        public string SelectedColorHex { get; private set; } = "#000000";
        private Color currentMouseColor = Color.Black;
        private Point mousePos;

        public OverlayForm(Bitmap bg, bool colorPicker)
        {
            this.background = bg;
            this.isColorPicker = colorPicker;
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen?.Bounds ?? SystemInformation.VirtualScreen;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Cross;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.KeyPreview = true; 
            
            this.TransparencyKey = Color.Empty;
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
            if (e.Control)
            {
                isCtrlPressed = true;
                this.Invalidate(); 
            }
            if (e.Alt)
            {
                isAltPressed = true;
                this.Invalidate(); 
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!e.Control)
            {
                isCtrlPressed = false;
                this.Invalidate(); 
            }
            if (!e.Alt)
            {
                isAltPressed = false;
                this.Invalidate(); 
            }
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
                if (isDrawing || SelectedArea.Width > 0 || isDrawingRectangle)
                {
                    isDrawing = false;
                    isDrawingRectangle = false;
                    SelectedArea = Rectangle.Empty;
                    currentAltRect = Rectangle.Empty;
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
                using (Pen pen = new Pen(Color.White, 1))
                {
                    e.Graphics.FillRectangle(brush, mousePos.X + 15, mousePos.Y + 15, 20, 20);
                    e.Graphics.DrawRectangle(pen, mousePos.X + 15, mousePos.Y + 15, 20, 20);
                }
            }
            else if (isDrawingRectangle && currentAltRect.Width > 0 && currentAltRect.Height > 0)
            {
                using (SolidBrush maroonBrush = new SolidBrush(Color.Maroon)) 
                {
                    e.Graphics.FillRectangle(maroonBrush, currentAltRect);
                }
            }
            else if (isDrawing || SelectedArea.Width > 0)
            {
                using (Brush dimBrush = new SolidBrush(Color.FromArgb(100, Color.Black)))
                {
                    e.Graphics.FillRectangle(dimBrush, 0, 0, this.Width, SelectedArea.Top);
                    e.Graphics.FillRectangle(dimBrush, 0, SelectedArea.Top, SelectedArea.Left, SelectedArea.Height);
                    e.Graphics.FillRectangle(dimBrush, SelectedArea.Right, SelectedArea.Top, this.Width - SelectedArea.Right, SelectedArea.Height);
                    e.Graphics.FillRectangle(dimBrush, 0, SelectedArea.Bottom, this.Width, this.Height - SelectedArea.Bottom);
                }

                using (Pen pen = new Pen(Color.Cyan, 1))
                {
                    e.Graphics.DrawRectangle(pen, SelectedArea);
                }
            }
            else
            {
                if (!isCtrlPressed && !isAltPressed)
                {
                    using (Brush dimBrush = new SolidBrush(Color.FromArgb(60, Color.Black)))
                    {
                        e.Graphics.FillRectangle(dimBrush, 0, 0, this.Width, this.Height);
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mousePos = e.Location;
            if (background == null) return;

            if (isColorPicker)
            {
                if (e.X >= 0 && e.X < background.Width && e.Y >= 0 && e.Y < background.Height)
                {
                    currentMouseColor = background.GetPixel(e.X, e.Y);
                    this.Invalidate();
                }
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
                    Color c = background.GetPixel(e.X, e.Y);
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
