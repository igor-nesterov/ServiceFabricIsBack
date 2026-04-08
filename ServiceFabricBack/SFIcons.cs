using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ServiceFabricBack
{
    /// <summary>
    /// Generates modern-style icons for the Service Fabric project hierarchy.
    /// All icons are created programmatically — no external image files needed.
    /// </summary>
    internal static class SFIcons
    {
        // Icon indices
        public const int Project = 0;
        public const int FolderClosed = 1;
        public const int FolderOpen = 2;
        public const int XmlFile = 3;
        public const int ScriptFile = 4;
        public const int ConfigFile = 5;
        public const int Reference = 6;
        public const int GenericFile = 7;
        public const int ManifestFile = 8;

        // Service Fabric brand colors (modern palette)
        private static readonly Color SFPurple = Color.FromArgb(126, 87, 194);
        private static readonly Color SFPurpleLight = Color.FromArgb(179, 157, 219);
        private static readonly Color AzureBlue = Color.FromArgb(0, 120, 212);
        private static readonly Color AzureBlueDark = Color.FromArgb(0, 90, 158);
        private static readonly Color FolderYellow = Color.FromArgb(255, 196, 0);
        private static readonly Color FolderYellowDark = Color.FromArgb(220, 165, 0);
        private static readonly Color FileGray = Color.FromArgb(200, 200, 200);
        private static readonly Color FileGrayDark = Color.FromArgb(160, 160, 160);
        private static readonly Color XmlBlue = Color.FromArgb(66, 133, 244);
        private static readonly Color ScriptTeal = Color.FromArgb(1, 135, 134);
        private static readonly Color ConfigOrange = Color.FromArgb(227, 135, 18);
        private static readonly Color ManifestGreen = Color.FromArgb(76, 175, 80);
        private static readonly Color RefPink = Color.FromArgb(194, 87, 140);

        private static ImageList imageList;

        public static ImageList GetImageList()
        {
            if (imageList == null)
            {
                imageList = new ImageList();
                imageList.ImageSize = new Size(16, 16);
                imageList.ColorDepth = ColorDepth.Depth32Bit;

                imageList.Images.Add(CreateProjectIcon());       // 0
                imageList.Images.Add(CreateFolderIcon(false));   // 1
                imageList.Images.Add(CreateFolderIcon(true));    // 2
                imageList.Images.Add(CreateXmlFileIcon());       // 3
                imageList.Images.Add(CreateScriptFileIcon());    // 4
                imageList.Images.Add(CreateConfigFileIcon());    // 5
                imageList.Images.Add(CreateReferenceIcon());     // 6
                imageList.Images.Add(CreateGenericFileIcon());   // 7
                imageList.Images.Add(CreateManifestFileIcon());  // 8
            }
            return imageList;
        }

        public static int GetIconForFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return GenericFile;

            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            var name = System.IO.Path.GetFileName(fileName).ToLowerInvariant();

            if (name == "applicationmanifest.xml" || name == "servicemanifest.xml")
                return ManifestFile;
            if (ext == ".xml" || ext == ".pubxml")
                return XmlFile;
            if (ext == ".ps1" || ext == ".psm1" || ext == ".psd1" || ext == ".sh" || ext == ".cmd" || ext == ".bat")
                return ScriptFile;
            if (ext == ".config" || ext == ".json" || ext == ".ini" || name == "packages.config")
                return ConfigFile;

            return GenericFile;
        }

        /// <summary>
        /// Service Fabric project icon: a modern mesh/constellation of connected nodes
        /// representing microservices architecture.
        /// </summary>
        private static Bitmap CreateProjectIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var pen = new Pen(SFPurpleLight, 1.3f))
                {
                    // Mesh lines connecting the nodes
                    g.DrawLine(pen, 8, 1, 2, 6);
                    g.DrawLine(pen, 8, 1, 14, 6);
                    g.DrawLine(pen, 2, 6, 5, 13);
                    g.DrawLine(pen, 14, 6, 11, 13);
                    g.DrawLine(pen, 5, 13, 11, 13);
                    g.DrawLine(pen, 2, 6, 14, 6);
                    g.DrawLine(pen, 8, 1, 5, 13);
                    g.DrawLine(pen, 8, 1, 11, 13);
                }

                // Nodes (microservice endpoints)
                FillCircle(g, AzureBlue, 8, 1, 3);      // top
                FillCircle(g, SFPurple, 2, 6, 3);        // left
                FillCircle(g, SFPurple, 14, 6, 3);       // right
                FillCircle(g, AzureBlue, 5, 13, 3);      // bottom-left
                FillCircle(g, AzureBlue, 11, 13, 3);     // bottom-right

                // Bright centers
                FillCircle(g, Color.White, 8, 1, 1);
                FillCircle(g, Color.White, 2, 6, 1);
                FillCircle(g, Color.White, 14, 6, 1);
                FillCircle(g, Color.White, 5, 13, 1);
                FillCircle(g, Color.White, 11, 13, 1);
            }
            return bmp;
        }

        /// <summary>
        /// Modern flat folder icon with SF-tinted color.
        /// </summary>
        private static Bitmap CreateFolderIcon(bool open)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                if (open)
                {
                    // Open folder — tab at top, angled front
                    var tab = new Point[] { new Point(1, 4), new Point(1, 2), new Point(6, 2), new Point(7, 4) };
                    g.FillPolygon(new SolidBrush(FolderYellowDark), tab);

                    var back = new Point[] { new Point(1, 4), new Point(14, 4), new Point(14, 13), new Point(1, 13) };
                    g.FillPolygon(new SolidBrush(FolderYellowDark), back);

                    var front = new Point[] { new Point(3, 6), new Point(15, 6), new Point(13, 13), new Point(1, 13) };
                    g.FillPolygon(new SolidBrush(FolderYellow), front);
                }
                else
                {
                    // Closed folder — simple rectangle with tab
                    var tab = new Point[] { new Point(1, 4), new Point(1, 2), new Point(6, 2), new Point(7, 4) };
                    g.FillPolygon(new SolidBrush(FolderYellowDark), tab);

                    g.FillRectangle(new SolidBrush(FolderYellow), 1, 4, 13, 9);
                    g.DrawRectangle(new Pen(FolderYellowDark, 1f), 1, 4, 13, 9);
                }
            }
            return bmp;
        }

        /// <summary>
        /// File icon base — white page with folded corner and colored accent.
        /// </summary>
        private static Bitmap CreateFileBase(Graphics g, Color accentColor)
        {
            // Page shape with folded corner
            var page = new Point[]
            {
                new Point(3, 1), new Point(11, 1), new Point(14, 4),
                new Point(14, 14), new Point(3, 14)
            };
            g.FillPolygon(Brushes.White, page);
            g.DrawPolygon(new Pen(FileGrayDark, 1f), page);

            // Corner fold
            var fold = new Point[] { new Point(11, 1), new Point(11, 4), new Point(14, 4) };
            g.FillPolygon(new SolidBrush(FileGray), fold);
            g.DrawPolygon(new Pen(FileGrayDark, 0.8f), fold);

            // Accent bar at left edge
            g.FillRectangle(new SolidBrush(accentColor), 3, 1, 2, 13);

            return null; // modifies g in place
        }

        private static Bitmap CreateXmlFileIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                CreateFileBase(g, XmlBlue);

                // XML brackets
                using (var font = new Font("Consolas", 6f, FontStyle.Bold))
                using (var brush = new SolidBrush(XmlBlue))
                {
                    g.DrawString("<>", font, brush, 6, 6);
                }
            }
            return bmp;
        }

        private static Bitmap CreateScriptFileIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                CreateFileBase(g, ScriptTeal);

                // PS prompt symbol
                using (var font = new Font("Consolas", 6.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(ScriptTeal))
                {
                    g.DrawString(">_", font, brush, 6, 6);
                }
            }
            return bmp;
        }

        private static Bitmap CreateConfigFileIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                CreateFileBase(g, ConfigOrange);

                // Gear-like symbol (small circle with dots)
                using (var pen = new Pen(ConfigOrange, 1.2f))
                {
                    g.DrawEllipse(pen, 8, 7, 4, 4);
                    g.FillEllipse(new SolidBrush(ConfigOrange), 9, 8, 2, 2);
                }
            }
            return bmp;
        }

        private static Bitmap CreateManifestFileIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                CreateFileBase(g, ManifestGreen);

                // Checkmark for manifest
                using (var pen = new Pen(ManifestGreen, 1.8f))
                {
                    g.DrawLine(pen, 8, 11, 10, 13);
                    g.DrawLine(pen, 10, 13, 14, 7);
                }
            }
            return bmp;
        }

        private static Bitmap CreateGenericFileIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                CreateFileBase(g, FileGrayDark);

                // Simple content lines
                using (var pen = new Pen(FileGrayDark, 0.8f))
                {
                    g.DrawLine(pen, 7, 7, 12, 7);
                    g.DrawLine(pen, 7, 9, 11, 9);
                    g.DrawLine(pen, 7, 11, 12, 11);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Reference icon — a small arrow/link symbol.
        /// </summary>
        private static Bitmap CreateReferenceIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Cylinder shape (like a component reference)
                using (var brush = new SolidBrush(RefPink))
                using (var lightBrush = new SolidBrush(Color.FromArgb(224, 167, 200)))
                using (var pen = new Pen(Color.FromArgb(150, 60, 110), 1f))
                {
                    // Body
                    g.FillRectangle(brush, 4, 4, 8, 8);
                    // Top ellipse
                    g.FillEllipse(lightBrush, 4, 2, 8, 4);
                    g.DrawEllipse(pen, 4, 2, 8, 4);
                    // Bottom ellipse
                    g.DrawArc(pen, 4, 8, 8, 4, 0, 180);
                    // Sides
                    g.DrawLine(pen, 4, 4, 4, 10);
                    g.DrawLine(pen, 12, 4, 12, 10);
                }
            }
            return bmp;
        }

        private static void FillCircle(Graphics g, Color color, int cx, int cy, int radius)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);
            }
        }
    }
}
