# **EstlCamEx — Estlcam Snapshot & Tray Helper**

EstlCamEx is a small .NET WinForms tray application that extends **Estlcam** with quality-of-life features the community has frequently requested.

The goal is to **provide helpful automation without modifying Estlcam itself**, respecting the author’s time, interests, and constraints.

---

## ⭐ **Features**

### **📸 Automatic Snapshot Backups**

* Detects changes to Estlcam project files.
* Captures timestamped snapshots to prevent accidental overwrites.
* Restores snapshots safely without clobbering the original file.

### **🖼 Screenshot Helper**

* Creates PNG snapshots of the Estlcam window for quick visual references.

### **🔔 Toast Notifications**

* Displays a small, lightweight notification for:

  * Snapshot saved
  * Snapshot restored
  * Errors or warnings
* Clicking the toast opens the snapshot folder (highlighting the new file).

### **🛠 System Tray Utility**

* Runs quietly in the background.
* Tray menu includes:

  * Open snapshot folder
  * Toggle auto-backup
  * Exit
  * (More features in future releases…)

---

## 🚀 Roadmap / Ideas

Future ideas that could bring more value—while *not* stepping on Estlcam’s toes:

...

---

## 🧩 **Project Structure**

```
/src
  EstlcamEx.csproj
  Program.cs
  TrayForm.*         # Tray icon + menu + UI
  ToastForm.*        # Custom toast popup
  SnapshotManager.cs # File snapshot logic
  ScreenshotHelper.cs
  Assets/
  Resources/
/.github
  workflows/
    dotnet-desktop.yml  # GitHub Actions build + release pipeline
README.md
```

---

## 💻 **Building Locally (Developer Guide)**

Requirements:

* .NET 8 SDK
* Windows (WinForms)

From the repo root:

```powershell
dotnet restore src/EstlcamEx.csproj
dotnet build src/EstlcamEx.csproj --configuration Release
dotnet publish src/EstlcamEx.csproj -c Release -o publish
```

The published executable will appear in:

```
/publish/
```

---

## 🔄 GitHub Actions: Automated Releases

This repository includes a workflow:

```
.github/workflows/dotnet-desktop.yml
```

When you push a **tag**, e.g.:

```bash
git tag v0.1.0
git push origin v0.1.0
```

GitHub Actions will:

1. Build the project on `windows-latest`
2. Run `dotnet publish`
3. Zip the output
4. Create a GitHub Release
5. Attach the ZIP artifact

This gives contributors and users a clean download for every tagged release.

---

## 🙏 Community & Intent

Estlcam is a one-developer project with an extremely wide user base.
EstlCamEx is built out of respect for that reality:

* **Non-intrusive**
* **Opt-in**
* **Does not modify Estlcam**
* **Adds convenience layers the community requests**

Our mission is to extend the ecosystem—not rewrite it.

---

## 🤝 Contributing

Pull requests are welcome.

Ideas most likely to be accepted:

* Small, useful automations
* Safety improvements (prevent file loss!)
* UI clarity enhancements
* Documentation / UX polish
* Features that help beginners feel more confident

Large-scale CAM functionality is intentionally **out of scope**.

---

## 📄 License

MIT License — permissive, commercial-friendly, community-friendly.
