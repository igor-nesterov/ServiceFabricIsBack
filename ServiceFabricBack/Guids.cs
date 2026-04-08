namespace ServiceFabricBack
{
    internal static class Guids
    {
        /// <summary>
        /// The GUID string for the ServiceFabricBack package.
        /// </summary>
        public const string PackageGuidString = "5fd20df8-00cf-4331-8aba-1848d9ce4a4b";

        /// <summary>
        /// The project type GUID for Service Fabric Application projects (.sfproj).
        /// This is the well-known GUID used by the original Service Fabric tooling.
        /// The factory is registered under this same GUID so VS maps .sfproj → our factory.
        /// </summary>
        public const string SFProjectTypeGuidString = "A07B5EB6-E848-4116-A8D0-A826331D98C6";

        /// <summary>
        /// C# project factory GUID — used as the base/inner project type for aggregation.
        /// sfproj files import Microsoft.Common.targets which is compatible with the C# project system.
        /// </summary>
        public const string CSharpProjectFactoryGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
    }
}
