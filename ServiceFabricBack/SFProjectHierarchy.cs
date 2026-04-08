using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace ServiceFabricBack
{
    /// <summary>
    /// IVsProject/IVsHierarchy implementation for Service Fabric Application projects.
    /// Parses the .sfproj MSBuild XML and presents files/folders in Solution Explorer.
    /// </summary>
    public class SFProjectHierarchy :
        IVsProject,
        IVsProject2,
        IVsUIHierarchy,
        IVsHierarchy,
        IPersistFileFormat,
        IVsGetCfgProvider,
        IVsCfgProvider2
    {
        private readonly ServiceFabricBackPackage package;
        private readonly string projectFile;
        private readonly string projectDir;
        private readonly string projectName;
        private readonly Microsoft.VisualStudio.OLE.Interop.IServiceProvider oleServiceProvider;
        private Guid projectInstanceGuid;

        private readonly Dictionary<uint, IVsHierarchyEvents> sinkMap = new Dictionary<uint, IVsHierarchyEvents>();
        private uint nextCookie = 1;

        private readonly List<HierarchyNode> nodes = new List<HierarchyNode>();

        // MSBuild item types that represent actual files
        private static readonly HashSet<string> FileItemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "None", "Content", "Compile", "EmbeddedResource",
            "ApplicationManifest", "ServiceManifest", "ApplicationPackageRoot"
        };

        private class HierarchyNode
        {
            public uint ItemId;
            public string RelativePath;
            public string FullPath;
            public string DisplayName;
            public bool IsFolder;
            public bool IsProjectReference;
            public uint ParentId;
        }

        public SFProjectHierarchy(ServiceFabricBackPackage package, string projectFile,
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider oleServiceProvider)
        {
            this.package = package;
            this.projectFile = projectFile;
            this.projectDir = Path.GetDirectoryName(projectFile);
            this.projectName = Path.GetFileNameWithoutExtension(projectFile);
            this.oleServiceProvider = oleServiceProvider;
            this.projectInstanceGuid = Guid.Empty;

            ParseProjectFile();
        }

        private void ParseProjectFile()
        {
            try
            {
                var doc = XDocument.Load(projectFile);
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

                // Try to read ProjectGuid from the file
                var guidElem = doc.Descendants(ns + "ProjectGuid").FirstOrDefault();
                if (guidElem != null && Guid.TryParse(guidElem.Value, out var parsed))
                    projectInstanceGuid = parsed;

                // Collect file items (None, Content, etc.)
                var fileItems = new List<string>();
                var projRefs = new List<string>();

                foreach (var elem in doc.Descendants(ns + "ItemGroup").SelectMany(ig => ig.Elements()))
                {
                    var include = elem.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;

                    var localName = elem.Name.LocalName;

                    if (FileItemTypes.Contains(localName))
                    {
                        // Only include items that look like file paths (contain \ or . or /)
                        if (include.Contains("\\") || include.Contains("/") || include.Contains("."))
                            fileItems.Add(include);
                    }
                    else if (string.Equals(localName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                    {
                        projRefs.Add(include);
                    }
                    // Skip ProjectConfiguration, PackageReference, etc.
                }

                // Build folder structure from file paths
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in fileItems)
                {
                    var dir = Path.GetDirectoryName(item);
                    while (!string.IsNullOrEmpty(dir) && !dir.StartsWith(".."))
                    {
                        folders.Add(dir);
                        dir = Path.GetDirectoryName(dir);
                    }
                }

                // Create folder nodes
                var folderMap = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                foreach (var folder in folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    uint id = nextCookie++;
                    var parentFolder = Path.GetDirectoryName(folder);
                    uint parentId = VSItemIdRoot;
                    if (!string.IsNullOrEmpty(parentFolder) && folderMap.ContainsKey(parentFolder))
                        parentId = folderMap[parentFolder];

                    nodes.Add(new HierarchyNode
                    {
                        ItemId = id,
                        RelativePath = folder,
                        FullPath = Path.Combine(projectDir, folder),
                        DisplayName = Path.GetFileName(folder),
                        IsFolder = true,
                        IsProjectReference = false,
                        ParentId = parentId
                    });
                    folderMap[folder] = id;
                }

                // Create file nodes
                foreach (var item in fileItems.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    // Skip items with paths going outside the project dir
                    if (item.StartsWith("..")) continue;

                    uint id = nextCookie++;
                    var parentFolder = Path.GetDirectoryName(item);
                    uint parentId = VSItemIdRoot;
                    if (!string.IsNullOrEmpty(parentFolder) && folderMap.ContainsKey(parentFolder))
                        parentId = folderMap[parentFolder];

                    nodes.Add(new HierarchyNode
                    {
                        ItemId = id,
                        RelativePath = item,
                        FullPath = Path.Combine(projectDir, item),
                        DisplayName = Path.GetFileName(item),
                        IsFolder = false,
                        IsProjectReference = false,
                        ParentId = parentId
                    });
                }

                // Create project reference nodes (flat under root)
                foreach (var pref in projRefs.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    uint id = nextCookie++;
                    nodes.Add(new HierarchyNode
                    {
                        ItemId = id,
                        RelativePath = pref,
                        FullPath = Path.GetFullPath(Path.Combine(projectDir, pref)),
                        DisplayName = Path.GetFileNameWithoutExtension(pref),
                        IsFolder = false,
                        IsProjectReference = true,
                        ParentId = VSItemIdRoot
                    });
                }
            }
            catch
            {
                // If parsing fails, show empty project
            }
        }

        // Helper constants
        private static readonly uint VSItemIdRoot = unchecked((uint)VSConstants.VSITEMID_ROOT);
        private static readonly uint VSItemIdNil = unchecked((uint)VSConstants.VSITEMID_NIL);

        private HierarchyNode FindNode(uint itemId)
        {
            return nodes.FirstOrDefault(n => n.ItemId == itemId);
        }

        private List<HierarchyNode> GetChildren(uint parentId)
        {
            return nodes.Where(n => n.ParentId == parentId).ToList();
        }

        private uint GetFirstChildId(uint parentId)
        {
            var children = GetChildren(parentId);
            return children.Count > 0 ? children[0].ItemId : VSItemIdNil;
        }

        private uint GetNextSiblingId(HierarchyNode node)
        {
            var siblings = GetChildren(node.ParentId);
            int idx = siblings.FindIndex(n => n.ItemId == node.ItemId);
            return (idx >= 0 && idx < siblings.Count - 1) ? siblings[idx + 1].ItemId : VSItemIdNil;
        }

        #region IVsHierarchy

        public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
            => VSConstants.S_OK;

        public int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
        {
            ppSP = oleServiceProvider;
            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;

        public int GetGuidProperty(uint itemid, int propid, out Guid pguid)
        {
            pguid = Guid.Empty;
            if (itemid == VSItemIdRoot)
            {
                if (propid == (int)__VSHPROPID.VSHPROPID_ProjectIDGuid)
                {
                    pguid = projectInstanceGuid != Guid.Empty
                        ? projectInstanceGuid
                        : GenerateGuidFromPath(projectFile);
                    return VSConstants.S_OK;
                }
                if (propid == (int)__VSHPROPID.VSHPROPID_TypeGuid)
                {
                    pguid = new Guid(Guids.SFProjectTypeGuidString);
                    return VSConstants.S_OK;
                }
            }
            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        public int SetGuidProperty(uint itemid, int propid, ref Guid rguid)
            => VSConstants.E_NOTIMPL;

        public int GetProperty(uint itemid, int propid, out object pvar)
        {
            pvar = null;

            // Handle root node
            if (itemid == VSItemIdRoot)
                return GetRootProperty(propid, out pvar);

            // Handle child nodes
            var node = FindNode(itemid);
            if (node != null)
                return GetNodeProperty(node, propid, out pvar);

            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        private int GetRootProperty(int propid, out object pvar)
        {
            pvar = null;
            switch (propid)
            {
                case (int)__VSHPROPID.VSHPROPID_Caption:
                case (int)__VSHPROPID.VSHPROPID_Name:
                    pvar = projectName;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_ProjectDir:
                    pvar = projectDir + "\\";
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_TypeName:
                    pvar = "Service Fabric Application";
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_SaveName:
                    pvar = Path.GetFileName(projectFile);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_ParentHierarchy:
                    pvar = null;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_ParentHierarchyItemid:
                    pvar = unchecked((int)VSItemIdRoot);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_Expandable:
                    pvar = nodes.Any(n => n.ParentId == VSItemIdRoot) ? 1 : 0;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_ExpandByDefault:
                    pvar = 1;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_FirstChild:
                    pvar = unchecked((int)GetFirstChildId(VSItemIdRoot));
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_NextSibling:
                    pvar = unchecked((int)VSItemIdNil);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_IconIndex:
                    pvar = SFIcons.Project;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex:
                    pvar = SFIcons.Project;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_IconImgList:
                    pvar = SFIcons.GetImageList().Handle;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle:
                case (int)__VSHPROPID.VSHPROPID_IconHandle:
                    pvar = IntPtr.Zero;
                    return VSConstants.S_OK;

                // VSHPROPID2 queries
                case (int)__VSHPROPID2.VSHPROPID_ChildrenEnumerated:
                    pvar = true;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID2.VSHPROPID_Container:
                    pvar = true;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList:
                case (int)__VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList:
                    pvar = "";
                    return VSConstants.S_OK;
            }

            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        private int GetNodeProperty(HierarchyNode node, int propid, out object pvar)
        {
            pvar = null;
            switch (propid)
            {
                case (int)__VSHPROPID.VSHPROPID_Caption:
                case (int)__VSHPROPID.VSHPROPID_Name:
                    pvar = node.DisplayName;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_Expandable:
                    if (node.IsFolder)
                        pvar = nodes.Any(n => n.ParentId == node.ItemId) ? 1 : 0;
                    else
                        pvar = 0;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_ExpandByDefault:
                    pvar = node.IsFolder ? 1 : 0;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_FirstChild:
                    pvar = node.IsFolder
                        ? unchecked((int)GetFirstChildId(node.ItemId))
                        : unchecked((int)VSItemIdNil);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_NextSibling:
                    pvar = unchecked((int)GetNextSiblingId(node));
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_Parent:
                    pvar = unchecked((int)node.ParentId);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_SaveName:
                    pvar = node.RelativePath;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_IconIndex:
                    pvar = GetNodeIconIndex(node);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex:
                    pvar = node.IsFolder ? SFIcons.FolderOpen : GetNodeIconIndex(node);
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_IconImgList:
                    pvar = SFIcons.GetImageList().Handle;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID.VSHPROPID_IconHandle:
                case (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle:
                    pvar = IntPtr.Zero;
                    return VSConstants.S_OK;

                case (int)__VSHPROPID2.VSHPROPID_ChildrenEnumerated:
                    pvar = true;
                    return VSConstants.S_OK;
            }

            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        private int GetNodeIconIndex(HierarchyNode node)
        {
            if (node.IsFolder) return SFIcons.FolderClosed;
            if (node.IsProjectReference) return SFIcons.Reference;
            return SFIcons.GetIconForFile(node.DisplayName);
        }

        public int SetProperty(uint itemid, int propid, object var)
            => VSConstants.E_NOTIMPL;

        public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested,
            out IntPtr ppHierarchyNested, out uint pitemidNested)
        {
            ppHierarchyNested = IntPtr.Zero;
            pitemidNested = 0;
            return VSConstants.E_FAIL;
        }

        public int GetCanonicalName(uint itemid, out string pbstrName)
        {
            if (itemid == VSItemIdRoot) { pbstrName = projectFile; return VSConstants.S_OK; }
            var node = FindNode(itemid);
            pbstrName = node?.FullPath;
            return node != null ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public int ParseCanonicalName(string pszName, out uint pitemid)
        {
            pitemid = VSItemIdNil;
            if (string.Equals(pszName, projectFile, StringComparison.OrdinalIgnoreCase))
            { pitemid = VSItemIdRoot; return VSConstants.S_OK; }
            var node = nodes.FirstOrDefault(n =>
                string.Equals(n.FullPath, pszName, StringComparison.OrdinalIgnoreCase));
            if (node != null) { pitemid = node.ItemId; return VSConstants.S_OK; }
            return VSConstants.E_FAIL;
        }

        public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie)
        {
            pdwCookie = nextCookie++;
            sinkMap[pdwCookie] = pEventSink;
            return VSConstants.S_OK;
        }

        public int UnadviseHierarchyEvents(uint dwCookie)
        {
            sinkMap.Remove(dwCookie);
            return VSConstants.S_OK;
        }

        public int Unused0() => VSConstants.E_NOTIMPL;
        public int Unused1() => VSConstants.E_NOTIMPL;
        public int Unused2() => VSConstants.E_NOTIMPL;
        public int Unused3() => VSConstants.E_NOTIMPL;
        public int Unused4() => VSConstants.E_NOTIMPL;

        public int QueryClose(out int pfCanClose)
        { pfCanClose = 1; return VSConstants.S_OK; }

        #endregion

        #region IVsUIHierarchy

        public int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds,
            OLECMD[] prgCmds, IntPtr pCmdText)
            => (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

        public int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID,
            uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
            => (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;

        #endregion

        #region IVsProject / IVsProject2

        public int IsDocumentInProject(string pszMkDocument, out int pfFound,
            VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid)
        {
            pfFound = 0;
            pitemid = 0;
            if (string.Equals(pszMkDocument, projectFile, StringComparison.OrdinalIgnoreCase))
            {
                pfFound = 1; pitemid = VSItemIdRoot;
                if (pdwPriority != null && pdwPriority.Length > 0)
                    pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Standard;
                return VSConstants.S_OK;
            }
            var node = nodes.FirstOrDefault(n =>
                string.Equals(n.FullPath, pszMkDocument, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                pfFound = 1; pitemid = node.ItemId;
                if (pdwPriority != null && pdwPriority.Length > 0)
                    pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Standard;
            }
            return VSConstants.S_OK;
        }

        public int GetMkDocument(uint itemid, out string pbstrMkDocument)
        {
            if (itemid == VSItemIdRoot) { pbstrMkDocument = projectFile; return VSConstants.S_OK; }
            var node = FindNode(itemid);
            pbstrMkDocument = node?.FullPath;
            return node != null ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public int OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting,
            out IVsWindowFrame ppWindowFrame)
        {
            ppWindowFrame = null;
            string filePath;
            if (itemid == VSItemIdRoot)
                filePath = projectFile;
            else
            {
                var node = FindNode(itemid);
                if (node == null || node.IsFolder) return VSConstants.E_FAIL;
                filePath = node.FullPath;
            }

            var sp = new ServiceProvider(oleServiceProvider);
            var openDoc = sp.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null) return VSConstants.E_FAIL;

            var logicalView = rguidLogicalView;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP;
            IVsUIHierarchy ppHierOpen;
            uint pitemidOpen;
            IVsWindowFrame ppFrame;
            openDoc.OpenDocumentViaProject(
                filePath, ref logicalView, out ppSP, out ppHierOpen, out pitemidOpen, out ppFrame);
            ppWindowFrame = ppFrame;
            return ppFrame != null ? VSConstants.S_OK : VSConstants.E_FAIL;
        }

        public int GetItemContext(uint itemid, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP)
        { ppSP = oleServiceProvider; return VSConstants.S_OK; }

        public int GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot,
            out string pbstrItemName)
        { pbstrItemName = pszSuggestedRoot + pszExt; return VSConstants.S_OK; }

        public int AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation,
            string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen,
            IntPtr hwndDlgOwner, VSADDRESULT[] pResult)
        {
            if (pResult != null && pResult.Length > 0) pResult[0] = VSADDRESULT.ADDRESULT_Cancel;
            return VSConstants.E_NOTIMPL;
        }

        public int RemoveItem(uint dwReserved, uint itemid, out int pfResult)
        { pfResult = 0; return VSConstants.E_NOTIMPL; }

        public int ReopenItem(uint itemid, ref Guid rguidEditorType, string pszPhysicalView,
            ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
            => OpenItem(itemid, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);

        #endregion

        #region IPersistFileFormat

        public int GetClassID(out Guid pClassID)
        { pClassID = new Guid(Guids.SFProjectTypeGuidString); return VSConstants.S_OK; }

        public int IsDirty(out int pfIsDirty) { pfIsDirty = 0; return VSConstants.S_OK; }
        public int InitNew(uint nFormatIndex) => VSConstants.S_OK;
        public int Load(string pszFilename, uint grfMode, int fReadOnly) => VSConstants.S_OK;
        public int Save(string pszFilename, int fRemember, uint nFormatIndex) => VSConstants.S_OK;
        public int SaveCompleted(string pszFilename) => VSConstants.S_OK;

        public int GetCurFile(out string ppszFilename, out uint pnFormatIndex)
        { ppszFilename = projectFile; pnFormatIndex = 0; return VSConstants.S_OK; }

        public int GetFormatList(out string ppszFormatList)
        { ppszFormatList = "Service Fabric Application (*.sfproj)\n*.sfproj\n"; return VSConstants.S_OK; }

        #endregion

        #region IVsGetCfgProvider / IVsCfgProvider2

        public int GetCfgProvider(out IVsCfgProvider ppCfgProvider)
        { ppCfgProvider = this; return VSConstants.S_OK; }

        public int GetCfgs(uint celt, IVsCfg[] a, uint[] pcActual, uint[] pfFlags)
        { if (pcActual != null && pcActual.Length > 0) pcActual[0] = 0; return VSConstants.S_OK; }

        public int GetCfgNames(uint celt, string[] rgbstr, uint[] pcActual)
        {
            if (celt > 0 && rgbstr != null) rgbstr[0] = "Debug";
            if (pcActual != null && pcActual.Length > 0) pcActual[0] = 1;
            return VSConstants.S_OK;
        }

        public int GetPlatformNames(uint celt, string[] rgbstr, uint[] pcActual)
        {
            if (celt > 0 && rgbstr != null) rgbstr[0] = "x64";
            if (pcActual != null && pcActual.Length > 0) pcActual[0] = 1;
            return VSConstants.S_OK;
        }

        public int GetCfgOfName(string pszCfgName, string pszPlatformName, out IVsCfg ppCfg)
        { ppCfg = null; return VSConstants.E_NOTIMPL; }

        public int AddCfgsOfCfgName(string n, string c, int f) => VSConstants.E_NOTIMPL;
        public int DeleteCfgsOfCfgName(string n) => VSConstants.E_NOTIMPL;
        public int RenameCfgsOfCfgName(string o, string n) => VSConstants.E_NOTIMPL;
        public int AddCfgsOfPlatformName(string n, string c) => VSConstants.E_NOTIMPL;
        public int DeleteCfgsOfPlatformName(string n) => VSConstants.E_NOTIMPL;

        public int GetSupportedPlatformNames(uint celt, string[] rgbstr, uint[] pcActual)
            => GetPlatformNames(celt, rgbstr, pcActual);

        public int AdviseCfgProviderEvents(IVsCfgProviderEvents pCPE, out uint pdwCookie)
        { pdwCookie = nextCookie++; return VSConstants.S_OK; }

        public int UnadviseCfgProviderEvents(uint dwCookie) => VSConstants.S_OK;

        public int GetCfgProviderProperty(int propid, out object var)
        { var = null; return VSConstants.E_NOTIMPL; }

        #endregion

        private static Guid GenerateGuidFromPath(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    path.ToLowerInvariant()));
                return new Guid(bytes);
            }
        }
    }
}
