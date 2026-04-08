<div align="center">

```
                                     ___
                                   /     \
                                  | () () |
                                   \ ⌣  /
                              _____|_____|_____
                             /                  \
                            /   ╔══════════════╗  \
                           |    ║  .sfproj     ║   |
                           |    ║   SUPPORT    ║   |
                    ╭──╮   |    ╚══════════════╝   |   ╭──╮
                    │SF│  /\                       /\   │SF│
                    ╰──╯ /  \_____________________/  \  ╰──╯
                        /  /  |   |       |   |  \   \
                       /  /   |   |       |   |   \   \
                      (__/    |___|       |___|    \__)
                              =====       =====
```

# 🦕 ServiceFabricBack

### *Bringing `.sfproj` back from extinction in Visual Studio 2026*

[![VS 2026](https://img.shields.io/badge/Visual%20Studio-2026-purple?style=for-the-badge&logo=visualstudio)](https://visualstudio.microsoft.com/)
[![Service Fabric](https://img.shields.io/badge/Service%20Fabric-.sfproj-blue?style=for-the-badge)](https://learn.microsoft.com/azure/service-fabric/)
[![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)](LICENSE.txt)

### 🛡️ Status

```text
      __
  /_)    / _\  rawr - your sfproj files are safe now
 / /    / /
/ /_   / /_
\__/   \__/
```

**Built with 💜 for everyone still running Service Fabric in production**

*Because not everything needs to be migrated to Kubernetes 😄*

</div>

---

## 🦖 What is this?

Microsoft removed built-in support for **Service Fabric Application projects (`.sfproj`)** in Visual Studio 2026. If you have existing Service Fabric solutions, they show up as **"(incompatible)"** and refuse to load.

This extension **brings `.sfproj` support back** — like a paleontologist recovering a dinosaur from the fossil record.

### What it does

- ✅ **Loads `.sfproj` projects** in Solution Explorer (no more "incompatible")
- ✅ **Displays the full project tree** — ApplicationPackageRoot, ApplicationParameters, PublishProfiles, Scripts, etc.
- ✅ **Opens files** — double-click any file to edit with the appropriate VS editor
- ✅ **Custom modern icons** — a clean microservices mesh icon for the project node, plus type-specific file icons
- ✅ **Project references** — shows referenced service projects
- ✅ **Includes a project template** for creating new SF Application projects
- ✅ **MSBuild targets** — basic build/package support for the SF application packaging workflow

### What it does NOT do (yet)

- ❌ SF SDK deployment integration (use PowerShell scripts or CI/CD pipelines)
- ❌ Service Fabric cluster management UI
- ❌ Add/remove service dialogs

---

## 📦 Installation

### Option 1: Build from source (recommended)

> **Prerequisites:** Visual Studio 2026 with the *Visual Studio extension development* workload installed.

```powershell
# 1. Clone the repo
git clone https://github.com/your-username/ServiceFabricBack.git
cd ServiceFabricBack

# 2. Build the VSIX
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
    ServiceFabricBack\ServiceFabricBack.csproj /t:Build /p:Configuration=Debug

# 3. Install the extension
# The VSIX is at: ServiceFabricBack\bin\Debug\ServiceFabricBack.vsix
# Double-click it, or run:
Start-Process "ServiceFabricBack\bin\Debug\ServiceFabricBack.vsix"
```

4. **Restart Visual Studio** after installation
5. Open your Service Fabric solution — `.sfproj` projects should load normally

### Option 2: Install pre-built VSIX

1. Grab the latest `ServiceFabricBack.vsix` from the [Releases](../../releases) page
2. Double-click the `.vsix` file
3. Follow the installer prompts
4. Restart Visual Studio

### Verifying installation

Go to **Extensions → Manage Extensions** and look for:

> **Service Fabric Project Support** — *Igor Nesterov*

### Optional diagnostics tracing

Tracing is now **disabled by default**.

Enable it only when troubleshooting:

```powershell
# Enable tracing for this shell session
$env:SERVICEFABRICBACK_TRACE = "1"

# Optional: custom log file path
$env:SERVICEFABRICBACK_TRACE_PATH = "$env:TEMP\ServiceFabricBack.Hierarchy.log"

# Launch Visual Studio from the same shell
Start-Process devenv.exe
```

Read recent entries:

```powershell
Get-Content "$env:TEMP\ServiceFabricBack.Hierarchy.log" -Tail 120
```

Disable tracing:

```powershell
Remove-Item Env:SERVICEFABRICBACK_TRACE -ErrorAction SilentlyContinue
Remove-Item Env:SERVICEFABRICBACK_TRACE_PATH -ErrorAction SilentlyContinue
```

---

## 🏗️ How it works

Since Microsoft never created a modern CPS-based project system for `.sfproj` (see [microsoft/service-fabric#885](https://github.com/microsoft/service-fabric/issues/885)), this extension implements the VS project system interfaces directly:

```
┌─────────────────────┐
│  SFProjectFactory   │  ← Registered for GUID {A07B5EB6-...}
│  (IVsProjectFactory)│     which is the well-known SF project type
└──────────┬──────────┘
           │ creates
           ▼
┌─────────────────────┐
│ SFProjectHierarchy  │  ← Parses .sfproj XML, builds file tree
│ (IVsHierarchy,      │     for Solution Explorer display
│  IVsProject,        │
│  IVsUIHierarchy)    │
└─────────────────────┘
```

- **`SFProjectFactory`** — Implements `IVsProjectFactory`, registered under the SF project type GUID `{A07B5EB6-E848-4116-A8D0-A826331D98C6}`. When VS encounters this GUID in a `.sln` file, it delegates to our factory.
- **`SFProjectHierarchy`** — Parses the `.sfproj` MSBuild XML to extract file items (`None`, `Content`, `ProjectReference`) and builds a proper hierarchy for Solution Explorer. Handles file opening via `IVsUIShellOpenDocument.OpenStandardEditor`.
- **`SFIcons`** — Generates all icons programmatically using GDI+ — a microservices mesh for the project, type-specific file icons, and folder icons.

---

## 📁 Project structure

```
ServiceFabricBack/
├── Guids.cs                  # Package and project type GUIDs
├── ServiceFabricBackPackage.cs    # VS Package — registers the factory
├── SFProjectFactory.cs       # IVsProjectFactory for .sfproj files
├── SFProjectHierarchy.cs     # IVsHierarchy — the project tree
├── SFIcons.cs                # Programmatic icon generation
├── BuildTargets/
│   ├── ServiceFabric.props   # MSBuild properties for sfproj
│   └── ServiceFabric.targets # MSBuild build/package targets
├── ProjectTemplates/
│   └── ServiceFabricApplication/  # New project template
│       ├── Application.sfproj
│       ├── ApplicationPackageRoot/
│       ├── ApplicationParameters/
│       ├── PublishProfiles/
│       └── Scripts/
└── source.extension.vsixmanifest
```

---

## 🤝 Contributing

PRs welcome! Some areas that could use help:

- **Build integration** — wire up the MSBuild targets to actually invoke SF packaging
- **Deployment** — add a "Publish" context menu command that runs the deploy script
- **Better icons** — swap GDI+ icons for proper SVG-based `ImageMoniker` support
- **Property pages** — add project property pages for SF-specific settings

---

## 📜 License

[MIT](LICENSE.txt) — do whatever you want with it.

---

<div align="center">

*Made because sometimes you have to bring things back from the past* 🦕

</div>