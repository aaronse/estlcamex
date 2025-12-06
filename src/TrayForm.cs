using System;
using System.Windows.Forms;

namespace EstlcamEx
{
    public partial class TrayForm : Form
    {
        private NotifyIcon trayIcon;
        private SnapshotManager snapshot;
        private KeyboardHook keyboard;
        private SnapshotViewerForm _snapshotViewer;

        public TrayForm()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            trayIcon = new NotifyIcon
            {
                // Project → Properties → Resources → Add Existing File...
                Icon =  Properties.Resources.estlcamex,
                //Icon = new Icon("Assets/estlcamex.ico"),
                Visible = true,
                Text = "EstlcamEx – Snapshot Undo for Estlcam"
            };

            trayIcon.ContextMenuStrip = BuildMenu();

            snapshot = new SnapshotManager();
            keyboard = new KeyboardHook();

            keyboard.CtrlZPressed += OnUndo;
            keyboard.CtrlYPressed += OnRedo;
            keyboard.CtrlRPressed += OnReviewSnapshots;
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            //menu.Items.Add("Open Snapshots Folder", null, (_, __) => snapshot.OpenFolder());
            menu.Items.Add(new ToolStripMenuItem("Open Snapshot Viewer", null, (_, __) =>
            {
                if (_snapshotViewer == null || _snapshotViewer.IsDisposed)
                {
                    _snapshotViewer = new SnapshotViewerForm(snapshot);
                }

                _snapshotViewer.BringToFrontAndFocus();
            }));
            menu.Items.Add("Exit", null, (_, __) => Application.Exit());


            return menu;
        }

        private void OnUndo()
        {
            if (!EstlcamInterop.IsEstlcamForeground()) return;
            snapshot.Undo();
        }

        private void OnRedo()
        {
            if (!EstlcamInterop.IsEstlcamForeground()) return;
            snapshot.Redo();
        }

        private void OnReviewSnapshots()
        {
            if (!EstlcamInterop.IsEstlcamForeground()) return;

            // Must switch to UI thread to create/show forms
            this.BeginInvoke(new Action(() =>
            {
                if (_snapshotViewer == null || _snapshotViewer.IsDisposed)
                {
                    _snapshotViewer = new SnapshotViewerForm(snapshot);
                }

                _snapshotViewer.BringToFrontAndFocus();
            }));
        }
    }
}
