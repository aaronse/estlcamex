using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
// if you use ScreenshotHelper: using System.Drawing; using System.Drawing.Imaging; live in ScreenshotHelper.cs, not here


namespace EstlcamEx
{
    public class SnapshotManager
    {
        private readonly string workingFile = @"E:\git\azab2c-private\projects\rad-xy-tape\export-jig.E12";
        private readonly string snapshotDir;
        private readonly List<string> versions = new();
        private int index = -1;

        private FileSystemWatcher watcher;
        private DateTime lastSnapshotTime = DateTime.MinValue;

        public SnapshotManager()
        {
            snapshotDir = Path.Combine(
                Path.GetDirectoryName(workingFile),
                ".snapshots",
                Path.GetFileNameWithoutExtension(workingFile));

            Directory.CreateDirectory(snapshotDir);

            watcher = new FileSystemWatcher(
                Path.GetDirectoryName(workingFile),
                Path.GetFileName(workingFile));

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += (_, e) => OnFileSaved();
            watcher.EnableRaisingEvents = true;
        }


        private void OnFileSaved()
        {
            // Debounce by time
            var now = DateTime.Now;
            if ((now - lastSnapshotTime).TotalSeconds < 1)
            {
                DebugLog("OnFileSaved, skipping, too soon since last snapshot");
                return;
            }

            lastSnapshotTime = now;

            string stamp = now.ToString("yyyyMMdd_HHmmss");
            string baseName = stamp;

            string workingExt = Path.GetExtension(workingFile); // preserve original ext
            string dest = Path.Combine(snapshotDir, $"{baseName}{workingExt}");
            string pngPath = Path.Combine(snapshotDir, $"{baseName}.png");

            DebugLog($"OnFileSaved, saving snapshot: {dest}");

            const int maxAttempts = 10;
            const int delayMs = 200;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (attempt == 1)
                    {
                        Thread.Sleep(delayMs);
                    }

                    File.Copy(workingFile, dest, overwrite: true);

                    versions.Add(dest);
                    index = versions.Count - 1;

                    DebugLog($"OnFileSaved, exiting successfully after {attempt} attempt(s): {dest}");

                    // Screenshot PNG (best-effort, does not fail snapshot)
                    try
                    {
                        ScreenshotHelper.CaptureEstlcamWindow(pngPath);
                        DebugLog($"OnFileSaved, screenshot saved: {pngPath}");
                    }
                    catch (Exception exShot)
                    {
                        DebugLog($"OnFileSaved, screenshot error: {exShot.Message}");
                    }

                    // Toast with clickable filename + thumbnail preview
                    try
                    {
                        Toast.ShowSnapshot("EstlcamEx: Snapshot saved", dest, pngPath);
                        DebugLog("OnFileSaved, toast shown");
                    }
                    catch (Exception exToast)
                    {
                        DebugLog($"OnFileSaved, toast error: {exToast.Message}");
                    }

                    return;
                }
                catch (IOException ex)
                {
                    DebugLog($"OnFileSaved, attempt {attempt} failed: {ex.Message}");

                    if (attempt == maxAttempts)
                    {
                        DebugLog($"OnFileSaved, giving up after {maxAttempts} attempts, no snapshot created for: {dest}");
                        return;
                    }

                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    DebugLog($"OnFileSaved, unexpected error: {ex}");
                    return;
                }
            }
        }



        public void Undo()
        {
            if (index <= 0) return;
            index--;
            Restore(versions[index]);
        }

        public void Redo()
        {
            if (index >= versions.Count - 1) return;
            index++;
            Restore(versions[index]);
        }

        private void Restore(string snapshot)
        {
            File.Copy(snapshot, workingFile, overwrite: true);
            EstlcamInterop.ReopenFile(workingFile);
            DebugLog($"Restored {snapshot}");

            // Optional toast on restore:
            Toast.Show("EstlcamEx: Restored snapshot");
        }

        public void OpenFolder()
        {
            System.Diagnostics.Process.Start("explorer.exe", snapshotDir);
        }

        private void DebugLog(string msg)
        {
            File.AppendAllText("EstlcamEx.log", $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
    }
}
