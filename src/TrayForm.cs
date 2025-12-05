using System;
using System.Windows.Forms;

namespace EstlcamEx
{
    public partial class TrayForm : Form
    {
        private NotifyIcon trayIcon;
        private SnapshotManager snapshot;
        private KeyboardHook keyboard;

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
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Snapshots Folder", null, (_, __) => snapshot.OpenFolder());
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
    }
}
