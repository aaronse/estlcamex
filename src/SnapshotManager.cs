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
        private readonly string workingFile;
        private readonly string workingExt;
        private readonly string snapshotDir;
        private readonly List<SnapshotInfo> snapshots = new();
        private readonly HashSet<string> snapshotPaths = new(StringComparer.OrdinalIgnoreCase);

        private int index = -1;
        
        private FileSystemWatcher watcher;
        private DateTime lastSnapshotTime = DateTime.MinValue;

        public SnapshotManager()
        {
            workingFile = @"E:\git\azab2c-private\projects\rad-xy-tape\export-jig.E12"; // TODO: make dynamic later
            workingExt = Path.GetExtension(workingFile);

            snapshotDir = Path.Combine(
                Path.GetDirectoryName(workingFile) ?? "",
                ".snapshots",
                Path.GetFileNameWithoutExtension(workingFile) ?? "default");

            Directory.CreateDirectory(snapshotDir);

            LoadExistingSnapshotsFromDisk();

            watcher = new FileSystemWatcher(
                Path.GetDirectoryName(workingFile) ?? "",
                Path.GetFileName(workingFile) ?? "*.*");

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += (_, __) => OnFileSaved();
            watcher.EnableRaisingEvents = true;
        }


        public string WorkingFilePath => workingFile;


        private void LoadExistingSnapshotsFromDisk()
        {
            snapshots.Clear();
            snapshotPaths.Clear();

            if (!Directory.Exists(snapshotDir))
            {
                DebugLog($"LoadExistingSnapshotsFromDisk, snapshotDir does not exist: {snapshotDir}");
                return;
            }

            try
            {
                // Only files matching the same extension as workingFile
                var files = Directory.GetFiles(snapshotDir, "*" + workingExt);

                foreach (var file in files)
                {
                    var info = CreateSnapshotInfoFromPath(file);
                    snapshots.Add(info);
                    snapshotPaths.Add(file);
                }

                // Sort ascending by timestamp
                snapshots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                index = snapshots.Count - 1;

                DebugLog($"LoadExistingSnapshotsFromDisk, loaded {snapshots.Count} snapshots from {snapshotDir}");
            }
            catch (Exception ex)
            {
                DebugLog($"LoadExistingSnapshotsFromDisk, error: {ex}");
            }
        }


        private SnapshotInfo CreateSnapshotInfoFromPath(string snapshotPath)
        {
            string baseName = Path.GetFileNameWithoutExtension(snapshotPath) ?? "";
            DateTime ts = ParseTimestampFromBaseName(baseName)
                          ?? File.GetCreationTime(snapshotPath);

            return new SnapshotInfo
            {
                Timestamp = ts,
                SnapshotPath = snapshotPath,
                PreviewImagePath = Path.Combine(
                    Path.GetDirectoryName(snapshotPath) ?? "",
                    baseName + ".png"),
                RelativeText = FormatRelativeTime(ts)
            };
        }


        private DateTime? ParseTimestampFromBaseName(string baseName)
        {
            // We expect something like: 20251205_091230 or 20251205_091230_1
            // So we take the first 15 chars "yyyyMMdd_HHmmss"
            if (string.IsNullOrWhiteSpace(baseName) || baseName.Length < 15)
                return null;

            string stamp = baseName.Substring(0, 15); // "yyyyMMdd_HHmmss"

            if (DateTime.TryParseExact(
                    stamp,
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsed))
            {
                return parsed;
            }

            return null;
        }


        public IReadOnlyList<SnapshotInfo> GetSnapshots()
        {
            // Ensure we have the latest snapshots from disk
            LoadExistingSnapshotsFromDisk();
            return snapshots.AsReadOnly();
        }


        private void OnFileSaved()
        {
            var now = DateTime.Now;
            if ((now - lastSnapshotTime).TotalSeconds < 1)
            {
                DebugLog("OnFileSaved, skipping, too soon since last snapshot");
                return;
            }

            lastSnapshotTime = now;

            string stamp = now.ToString("yyyyMMdd_HHmmss");
            string baseName = stamp;

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

                    DebugLog($"OnFileSaved, exiting successfully after {attempt} attempt(s): {dest}");

                    // Only track if we don't already know about this path
                    if (!snapshotPaths.Contains(dest))
                    {
                        var info = new SnapshotInfo
                        {
                            Timestamp = now,
                            SnapshotPath = dest,
                            PreviewImagePath = pngPath,
                            RelativeText = FormatRelativeTime(now)
                        };

                        snapshots.Add(info);
                        snapshotPaths.Add(dest);

                        // Keep snapshots sorted by time
                        snapshots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                        index = snapshots.Count - 1;
                    }

                    // Screenshot PNG (best-effort)
                    try
                    {
                        ScreenshotHelper.CaptureEstlcamWindow(pngPath);
                        DebugLog($"OnFileSaved, screenshot saved: {pngPath}");
                    }
                    catch (Exception exShot)
                    {
                        DebugLog($"OnFileSaved, screenshot error: {exShot.Message}");
                    }

                    // Toast with clickable filename, thumbnail preview
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
            Restore(snapshots[index].SnapshotPath);
        }

        public void Redo()
        {
            if (index >= snapshots.Count - 1) return;
            index++;
            Restore(snapshots[index].SnapshotPath);
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

        public string RestoreSnapshotAsCopy(SnapshotInfo snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            string originalDir = Path.GetDirectoryName(workingFile);
            string originalBase = Path.GetFileNameWithoutExtension(workingFile);
            string ext = Path.GetExtension(workingFile);

            // Build base restored name with timestamp
            string stamp = snapshot.Timestamp.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{originalBase}_restored_{stamp}";
            string candidate = Path.Combine(originalDir, baseName + ext);

            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(originalDir, $"{baseName}_{suffix}{ext}");
                suffix++;
            }

            File.Copy(snapshot.SnapshotPath, candidate, overwrite: false);

            // Optionally mark restored file as read-only too (hard immutability)
            // var attr = File.GetAttributes(candidate);
            // File.SetAttributes(candidate, attr | FileAttributes.ReadOnly);

            return candidate;
        }


        private void DebugLog(string msg)
        {
            File.AppendAllText("EstlcamEx.log", $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }

        private static string FormatRelativeTime(DateTime t)
        {
            var delta = DateTime.Now - t;

            if (delta.TotalSeconds < 60) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} mins ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hours ago";
            if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} days ago";
            if (delta.TotalDays < 30) return $"{(int)(delta.TotalDays / 7)} weeks ago";
            return t.ToShortDateString();
        }

    }
}
