using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ServiceFabricBack
{
    /// <summary>
    /// VS Package that registers the Service Fabric Application project type (.sfproj)
    /// back into Visual Studio 2026 after it was removed from the built-in tooling.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Service Fabric Application Support", "Adds back support for Service Fabric Application projects (.sfproj) in Visual Studio 2026.", "1.0.6")]
    [Guid(Guids.PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideProjectFactory(
        typeof(SFProjectFactory),
        "Service Fabric Application",
        "Service Fabric Application Project Files (*.sfproj);*.sfproj",
        "sfproj", "sfproj",
        @"ProjectTemplates\ServiceFabricApplication",
        LanguageVsTemplate = "CSharp",
        NewProjectRequireNewFolderVsTemplate = false)]
    public sealed class ServiceFabricBackPackage : AsyncPackage
    {
        #region Package Members

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Register the project factory so VS recognizes .sfproj files
            this.RegisterProjectFactory(new SFProjectFactory());
        }

        #endregion
    }
}
