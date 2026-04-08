using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

namespace ServiceFabricBack
{
    /// <summary>
    /// Factory for creating Service Fabric Application project instances.
    /// Registered under the well-known SF project type GUID so VS can load .sfproj files.
    /// 
    /// Implements IVsProjectFactory directly (not FlavoredProjectFactoryBase) because
    /// sfproj files cannot be loaded by any built-in project system (C#, VB, etc.).
    /// Creates a lightweight SFProjectHierarchy that displays the project in Solution Explorer.
    /// </summary>
    [Guid(Guids.SFProjectTypeGuidString)]
    public class SFProjectFactory : IVsProjectFactory
    {
        private Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProviderSite;

        public SFProjectFactory()
        {
        }

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            this.serviceProviderSite = psp;
            return VSConstants.S_OK;
        }

        public int CanCreateProject(string pszFilename, uint grfCreateFlags, out int pfCanCreate)
        {
            pfCanCreate = pszFilename != null &&
                pszFilename.EndsWith(".sfproj", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return VSConstants.S_OK;
        }

        public int CreateProject(string pszFilename, string pszLocation, string pszName,
            uint grfCreateFlags, ref Guid iidProject, out IntPtr ppvProject, out int pfCanceled)
        {
            ppvProject = IntPtr.Zero;
            pfCanceled = 0;

            var hierarchy = new SFProjectHierarchy(pszFilename, serviceProviderSite);
            ppvProject = Marshal.GetIUnknownForObject(hierarchy);

            return VSConstants.S_OK;
        }

        public int Close()
        {
            return VSConstants.S_OK;
        }
    }
}
