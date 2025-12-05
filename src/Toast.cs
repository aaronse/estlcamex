using System;
using System.Windows.Forms;

namespace EstlcamEx
{
    public static class Toast
    {
        // Generic message-only toast (old behavior)
        public static void Show(string message)
        {
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.BeginInvoke(new Action(() =>
                {
                    var toast = new ToastForm(message, filePath: string.Empty, previewImagePath: null);
                    toast.Show();
                }));
            }
        }

        // Snapshot toast with file link + preview image
        public static void ShowSnapshot(string message, string filePath, string previewImagePath = null)
        {
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.BeginInvoke(new Action(() =>
                {
                    var toast = new ToastForm(message, filePath, previewImagePath);
                    toast.Show();
                }));
            }
        }
    }
}
