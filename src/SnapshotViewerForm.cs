using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EstlcamEx
{
    public partial class SnapshotViewerForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly SnapshotManager snapshotManager;
        private readonly List<SnapshotInfo> allSnapshots;
        private List<SnapshotInfo> viewSnapshots;

        private readonly ImageList imageListLarge = new ImageList();
        private TileSizePreset currentTileSize = TileSizePreset.S;
        private TimeFilterPreset currentTimeFilter = TimeFilterPreset.All;
        private SortOrderPreset currentSortOrder = SortOrderPreset.NewestFirst;

        // UI controls
        private ComboBox comboSort;
        private ComboBox comboTileSize;
        private ComboBox comboTimeFilter;
        private SplitContainer splitMain;
        private ListView listSnapshots;
        private PictureBox previewBox;
        private Label labelMeta;
        private RoundedButton buttonRestore;
        private TrackBar trackTimeline;
        private Label labelTimelineInfo;
        private Label labelEmptyState;

        // Map ListView index <-> SnapshotInfo
        private readonly Dictionary<ListViewItem, SnapshotInfo> itemToSnapshot = new();

        public SnapshotViewerForm(SnapshotManager manager)
        {
            snapshotManager = manager ?? throw new ArgumentNullException(nameof(manager));
            allSnapshots = snapshotManager.GetSnapshots().ToList();
            viewSnapshots = new List<SnapshotInfo>(allSnapshots);

            InitializeComponent();
            InitializeLayout();
            PopulateInitialData();
        }

        // --- Layout / init ---

        private void InitializeComponent()
        {
            Text = $"EstlcamEx Snapshot Viewer – {System.IO.Path.GetFileName(snapshotManager.WorkingFilePath)}";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;   // full screen feel, but user can Alt+Tab
            MinimumSize = new Size(900, 600);

            // Header panel
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8),
            };

            var labelFile = new Label
            {
                AutoSize = true,
                Text = $"Snapshots for: {snapshotManager.WorkingFilePath}",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock = DockStyle.Left,
            };

            // Sort combo
            comboSort = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140
            };
            comboSort.Items.AddRange(new object[]
            {
                "Newest first",
                "Oldest first"
            });
            comboSort.SelectedIndex = 0;
            comboSort.SelectedIndexChanged += (_, __) =>
            {
                currentSortOrder = (SortOrderPreset)comboSort.SelectedIndex;
                RefreshSnapshotView();
            };

            // Tile size combo: XS | S | M | L
            comboTileSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 100
            };
            comboTileSize.Items.AddRange(new object[] { "XS", "S", "M", "L" });
            comboTileSize.SelectedIndex = 1; // default S
            comboTileSize.SelectedIndexChanged += (_, __) =>
            {
                currentTileSize = (TileSizePreset)comboTileSize.SelectedIndex;
                ApplyTileSizeToImageList();
                RefreshSnapshotView();
            };

            // Time filter combo
            comboTimeFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 130
            };
            comboTimeFilter.Items.AddRange(new object[]
            {
                "All",
                "Last 24 hours",
                "Last 7 days",
                "Last 30 days"
            });
            comboTimeFilter.SelectedIndex = 0;
            comboTimeFilter.SelectedIndexChanged += (_, __) =>
            {
                currentTimeFilter = (TimeFilterPreset)comboTimeFilter.SelectedIndex;
                RefreshSnapshotView();
            };

            // Right-aligned panel for filters
            var panelRightHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false
            };
            panelRightHeader.Controls.Add(new Label { Text = "Sort:", AutoSize = true, ForeColor = Color.White, Padding = new Padding(8, 8, 4, 0) });
            panelRightHeader.Controls.Add(comboSort);
            panelRightHeader.Controls.Add(new Label { Text = "Tile size:", AutoSize = true, ForeColor = Color.White, Padding = new Padding(8, 8, 4, 0) });
            panelRightHeader.Controls.Add(comboTileSize);
            panelRightHeader.Controls.Add(new Label { Text = "Time:", AutoSize = true, ForeColor = Color.White, Padding = new Padding(8, 8, 4, 0) });
            panelRightHeader.Controls.Add(comboTimeFilter);

            panelHeader.Controls.Add(panelRightHeader);
            panelHeader.Controls.Add(labelFile);

            // Split container
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8,              // thicker grab bar
                //Panel1MinSize = 200,
                //Panel2MinSize = 300
            };

            splitMain.DoubleClick += SplitMain_SplitterDoubleClick;


            // Left: ListView for tiled snapshots
            listSnapshots = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.LargeIcon,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
            };
            listSnapshots.LargeImageList = imageListLarge;
            listSnapshots.ItemSelectionChanged += ListSnapshots_ItemSelectionChanged;
            listSnapshots.ItemActivate += ListSnapshots_ItemActivate; // double-click or Enter

            labelEmptyState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "No snapshots yet for this file.\nMake changes in Estlcam and press (Ctrl + S) to create snapshots.",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            var panelLeft = new Panel { Dock = DockStyle.Fill };
            panelLeft.Controls.Add(listSnapshots);
            panelLeft.Controls.Add(labelEmptyState);

            splitMain.Panel1.Controls.Add(panelLeft);

            // Right: preview + meta + restore
            previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            labelMeta = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Padding = new Padding(8),
                Text = ""
            };

            var container = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
            };

            buttonRestore = new RoundedButton
            {
                CornerRadius = 12,
                Height = 48,
                Width = 480,
                MaximumSize = new Size(480, 48),
                Text = "Restore snapshot (open copy in Estlcam)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                BackColor = Color.ForestGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            buttonRestore.FlatAppearance.BorderSize = 0;
            buttonRestore.Anchor = AnchorStyles.None; // Center it
            buttonRestore.Click += ButtonRestore_Click;


            var labelRestoreHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                Padding = new Padding(8),
                Text = "A copy will be created as <originalName>_restored_<timestamp>.ext.\nYour original file will not be modified. Use \"Save As...\" in Estlcam to keep changes."
            };

            var panelRight = new Panel { Dock = DockStyle.Fill };
            panelRight.Controls.Add(previewBox);
            panelRight.Controls.Add(labelMeta);
            panelRight.Controls.Add(labelRestoreHint);
            container.Controls.Add(buttonRestore);
            this.Controls.Add(container);

            splitMain.Panel2.Controls.Add(panelRight);

            // Timeline bottom panel
            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(8)
            };

            trackTimeline = new TrackBar
            {
                Dock = DockStyle.Fill,
                TickStyle = TickStyle.BottomRight,
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 5
            };
            trackTimeline.Scroll += TrackTimeline_Scroll;

            labelTimelineInfo = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelBottom.Controls.Add(trackTimeline);
            panelBottom.Controls.Add(labelTimelineInfo);

            // Form controls
            Controls.Add(splitMain);
            Controls.Add(panelBottom);
            Controls.Add(panelHeader);

            BackColor = Color.FromArgb(24, 24, 24);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Compute a safe initial splitter distance: 20% left / 80% right,
            // but clamped between Panel1MinSize and Width - Panel2MinSize.
            int totalWidth = splitMain.Width;
            int minDistance = splitMain.Panel1MinSize;
            int maxDistance = totalWidth - splitMain.Panel2MinSize;

            // Only set if we have a valid range
            if (maxDistance > minDistance)
            {
                int desired = (int)(totalWidth * 0.20);
                if (desired < minDistance) desired = minDistance;
                if (desired > maxDistance) desired = maxDistance;

                splitMain.SplitterDistance = desired;
            }
            else
            {
                // Fallback: just split in the middle if we're in a tiny layout state
                // (user resizing later will correct it).
                splitMain.SplitterDistance = totalWidth / 2;
            }
        }


        public void BringToFrontAndFocus()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            // Make sure it's visible
            if (!Visible)
            {
                Show();
            }

            // These three are often enough
            BringToFront();
            Activate();
            Focus();

            // Nudge Windows to make this the foreground window
            try
            {
                SetForegroundWindow(this.Handle);
            }
            catch
            {
                // ignore; worst case the window is just brought to front without focus
            }
        }


        private void SplitMain_SplitterDoubleClick(object sender, EventArgs e)
        {
            // We want a single column of tiles in Panel1 based on current tile size.
            // Use the current ImageList size plus some padding for labels/margins.
            var tileSize = imageListLarge.ImageSize;

            int desiredTileWidth = tileSize.Width;

            // Extra space for text under thumbnails, listview margins, scrollbar etc.
            int extraPadding = 70;
            int targetPanel1Width = desiredTileWidth + extraPadding;

            // Respect min/max layout constraints
            int maxPanel1Width = ClientSize.Width - splitMain.Panel2MinSize - splitMain.SplitterWidth;
            int minPanel1Width = splitMain.Panel1MinSize;

            targetPanel1Width = Math.Max(minPanel1Width, Math.Min(targetPanel1Width, maxPanel1Width));

            splitMain.SplitterDistance = targetPanel1Width;
        }


        private void InitializeLayout()
        {
            // ImageList config; will be adjusted by tile size preset
            imageListLarge.ColorDepth = ColorDepth.Depth32Bit;
            ApplyTileSizeToImageList();
            this.Resize += (_, __) =>
            {
                ApplyTileSizeToImageList();
                RefreshSnapshotView();
            };
        }

        private void PopulateInitialData()
        {
            RefreshSnapshotView();
        }

        // --- View refreshing / filters / sorting ---

        private void RefreshSnapshotView()
        {
            viewSnapshots = ApplyTimeFilter(allSnapshots, currentTimeFilter);
            viewSnapshots = ApplySortOrder(viewSnapshots, currentSortOrder);

            listSnapshots.BeginUpdate();
            listSnapshots.Items.Clear();
            imageListLarge.Images.Clear();
            itemToSnapshot.Clear();

            if (viewSnapshots.Count == 0)
            {
                labelEmptyState.Visible = true;
                listSnapshots.Visible = false;
                trackTimeline.Enabled = false;
                labelTimelineInfo.Text = "No snapshots.";
            }
            else
            {
                labelEmptyState.Visible = false;
                listSnapshots.Visible = true;
                trackTimeline.Enabled = true;

                for (int i = 0; i < viewSnapshots.Count; i++)
                {
                    var snap = viewSnapshots[i];
                    string key = $"snap_{i}";

                    // Load thumbnail image from existing PNG
                    Image img = CreateThumbnailForSnapshot(snap);
                    imageListLarge.Images.Add(key, img);

                    string labelText =
                        $"{snap.Timestamp:yyyy-MM-dd HH:mm:ss} ({snap.RelativeText})";

                    var item = new ListViewItem(labelText)
                    {
                        ImageKey = key
                    };

                    listSnapshots.Items.Add(item);
                    itemToSnapshot[item] = snap;
                }

                trackTimeline.Minimum = 0;
                trackTimeline.Maximum = Math.Max(0, viewSnapshots.Count - 1);
                trackTimeline.Value = viewSnapshots.Count - 1; // latest by default

                if (viewSnapshots.Count > 1)
                {
                    trackTimeline.TickFrequency = Math.Max(1, viewSnapshots.Count / 10);
                }
                else
                {
                    trackTimeline.TickFrequency = 1;
                }

                labelTimelineInfo.Text =
                    $"Snapshots: {viewSnapshots.Count} | Range: {viewSnapshots.First().Timestamp:g} – {viewSnapshots.Last().Timestamp:g}";

                // Select last (latest)
                if (listSnapshots.Items.Count > 0)
                {
                    listSnapshots.Items[listSnapshots.Items.Count - 1].Selected = true;
                    listSnapshots.EnsureVisible(listSnapshots.Items.Count - 1);
                }
            }

            listSnapshots.EndUpdate();
        }

        private static List<SnapshotInfo> ApplyTimeFilter(List<SnapshotInfo> source, TimeFilterPreset filter)
        {
            if (filter == TimeFilterPreset.All || source.Count == 0) return new List<SnapshotInfo>(source);

            DateTime cutoff = DateTime.MinValue;
            var now = DateTime.Now;

            switch (filter)
            {
                case TimeFilterPreset.Last24Hours:
                    cutoff = now.AddDays(-1);
                    break;
                case TimeFilterPreset.Last7Days:
                    cutoff = now.AddDays(-7);
                    break;
                case TimeFilterPreset.Last30Days:
                    cutoff = now.AddDays(-30);
                    break;
            }

            return source.Where(s => s.Timestamp >= cutoff).ToList();
        }

        private static List<SnapshotInfo> ApplySortOrder(List<SnapshotInfo> source, SortOrderPreset sort)
        {
            return sort == SortOrderPreset.NewestFirst
                ? source.OrderBy(s => s.Timestamp).ToList()   // we’ll reverse visually later (or just treat “latest” as last)
                : source.OrderBy(s => s.Timestamp).ToList();
        }

        private void ApplyTileSizeToImageList()
        {
            // WinForms ImageList requires 1..256 for both width and height
            const int MaxSide = 256;
            const int MinSide = 16;

            // Use a baseline from window width, but keep it sane
            int baseWidth = ClientSize.Width > 0
                ? ClientSize.Width / 6
                : 200;

            baseWidth = Math.Clamp(baseWidth, 80, MaxSide); // keep baseline sane

            // XS | S | M | L — S is default
            // Height ~ 4:3 aspect
            Size size = currentTileSize switch
            {
                TileSizePreset.XS => new Size((int)(baseWidth * 0.50), (int)(baseWidth * 0.50 * 0.75)),
                TileSizePreset.S => new Size((int)(baseWidth * 0.85), (int)(baseWidth * 0.85 * 0.75)),
                TileSizePreset.M => new Size((int)(baseWidth * 1.5), (int)(baseWidth * 1.5 * 0.75)),
                TileSizePreset.L => new Size((int)(baseWidth * 3.0), (int)(baseWidth * 3.0 * 0.75)),
                _ => new Size((int)(baseWidth * 0.85), (int)(baseWidth * 0.85 * 0.75)),
            };

            // Clamp to ImageList limits
            int w = Math.Clamp(size.Width, MinSide, MaxSide);
            int h = Math.Clamp(size.Height, MinSide, MaxSide);

            imageListLarge.ImageSize = new Size(w, h);
        }



        // --- Events ---

        private void ListSnapshots_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected) return;
            if (!itemToSnapshot.TryGetValue(e.Item, out var snap)) return;

            UpdatePreviewForSnapshot(snap);

            // Sync timeline
            int idx = viewSnapshots.IndexOf(snap);
            if (idx >= 0 && idx >= trackTimeline.Minimum && idx <= trackTimeline.Maximum)
            {
                trackTimeline.Value = idx;
            }
        }

        private void ListSnapshots_ItemActivate(object sender, EventArgs e)
        {
            // Double-click or Enter
            if (listSnapshots.SelectedItems.Count == 0) return;
            var item = listSnapshots.SelectedItems[0];
            if (!itemToSnapshot.TryGetValue(item, out var snap)) return;

            RestoreAndOpen(snap);
        }

        private void TrackTimeline_Scroll(object sender, EventArgs e)
        {
            if (viewSnapshots.Count == 0) return;
            int idx = trackTimeline.Value;
            if (idx < 0 || idx >= viewSnapshots.Count) return;

            var snap = viewSnapshots[idx];

            // Update list selection
            var item = listSnapshots.Items[idx];
            listSnapshots.SelectedItems.Clear();
            item.Selected = true;
            item.Focused = true;
            listSnapshots.EnsureVisible(idx);

            UpdatePreviewForSnapshot(snap);
        }

        private void ButtonRestore_Click(object sender, EventArgs e)
        {
            if (listSnapshots.SelectedItems.Count == 0) return;
            var item = listSnapshots.SelectedItems[0];
            if (!itemToSnapshot.TryGetValue(item, out var snap)) return;

            RestoreAndOpen(snap);
        }

        // --- Preview / restore helpers ---

        private void UpdatePreviewForSnapshot(SnapshotInfo snap)
        {
            // Load preview image
            if (!string.IsNullOrEmpty(snap.PreviewImagePath) && System.IO.File.Exists(snap.PreviewImagePath))
            {
                try
                {
                    using var temp = Image.FromFile(snap.PreviewImagePath);
                    previewBox.Image?.Dispose();
                    previewBox.Image = new Bitmap(temp);
                }
                catch
                {
                    previewBox.Image?.Dispose();
                    previewBox.Image = null;
                }
            }
            else
            {
                previewBox.Image?.Dispose();
                previewBox.Image = null;
            }

            labelMeta.Text =
                $"Snapshot: {snap.Timestamp:yyyy-MM-dd HH:mm:ss} ({snap.RelativeText})\n" +
                $"File: {System.IO.Path.GetFileName(snap.SnapshotPath)}";
        }

        private void RestoreAndOpen(SnapshotInfo snap)
        {
            try
            {
                // Let the user know something is happening (no file link here)
                Toast.Show("Restoring snapshot, opening Estlcam… This may take a few seconds.");

                string restoredPath = snapshotManager.RestoreSnapshotAsCopy(snap);

                // Open restored copy in a new Estlcam instance (or associated app)
                EstlcamInterop.OpenFileInNewInstance(restoredPath);

                // Optional: second toast with link to the restored file
                Toast.ShowSnapshot(
                    "EstlcamEx: Restored snapshot opened",
                    restoredPath,
                    snap.PreviewImagePath
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to restore snapshot:\n{ex.Message}",
                    "Restore error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



        private Image CreateThumbnailForSnapshot(SnapshotInfo snap)
        {
            var size = imageListLarge.ImageSize;

            // Fallback: blank tile if PNG missing
            if (string.IsNullOrEmpty(snap.PreviewImagePath) || !System.IO.File.Exists(snap.PreviewImagePath))
            {
                var blank = new Bitmap(size.Width, size.Height);
                using (var g = Graphics.FromImage(blank))
                {
                    g.Clear(Color.DimGray);
                }
                return blank;
            }

            try
            {
                using var original = Image.FromFile(snap.PreviewImagePath);

                float targetW = size.Width;
                float targetH = size.Height;

                float scale = Math.Min(targetW / original.Width, targetH / original.Height);
                int w = (int)(original.Width * scale);
                int h = (int)(original.Height * scale);

                var bmp = new Bitmap(size.Width, size.Height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    int offsetX = (size.Width - w) / 2;
                    int offsetY = (size.Height - h) / 2;

                    g.DrawImage(original, offsetX, offsetY, w, h);
                }

                return bmp;
            }
            catch
            {
                var blank = new Bitmap(size.Width, size.Height);
                using (var g = Graphics.FromImage(blank))
                {
                    g.Clear(Color.DimGray);
                }
                return blank;
            }
        }

    }

    public enum TileSizePreset
    {
        XS = 0,
        S = 1,
        M = 2,
        L = 3
    }

    public enum TimeFilterPreset
    {
        All = 0,
        Last24Hours = 1,
        Last7Days = 2,
        Last30Days = 3
    }

    public enum SortOrderPreset
    {
        NewestFirst = 0,
        OldestFirst = 1
    }
}
