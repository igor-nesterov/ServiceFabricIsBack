<#
.SYNOPSIS
Deploys a Service Fabric application to a cluster.

.DESCRIPTION
This script deploys a Service Fabric application package to a local or remote cluster.

.PARAMETER ApplicationPackagePath
Path to the application package folder.

.PARAMETER PublishProfileFile
Path to the publish profile XML file.

.PARAMETER UseExistingClusterConnection
If true, uses the current cluster connection. Otherwise connects using the publish profile.

.PARAMETER OverwriteBehavior
Overwrite behavior: Never, Always, or SameAppTypeAndVersion.

.PARAMETER SkipPackageValidation
If true, skips the package validation step.

.PARAMETER CopyPackageTimeoutSec
Timeout in seconds for the package copy operation.

.EXAMPLE
.\Deploy-FabricApplication.ps1 -ApplicationPackagePath '..\pkg\Debug'
#>

Param
(
    [String]
    $ApplicationPackagePath,

    [String]
    $PublishProfileFile,

    [Switch]
    $UseExistingClusterConnection,

    [String]
    [ValidateSet('Never','Always','SameAppTypeAndVersion')]
    $OverwriteBehavior = 'SameAppTypeAndVersion',

    [Switch]
    $SkipPackageValidation,

    [int]
    $CopyPackageTimeoutSec = 600
)

$LocalFolder = (Split-Path $MyInvocation.MyCommand.Path)

if (-not $PublishProfileFile)
{
    $PublishProfileFile = "$LocalFolder\..\PublishProfiles\Local.1Node.pubxml"
}

if (-not $ApplicationPackagePath)
{
    $ApplicationPackagePath = "$LocalFolder\..\pkg\Debug"
}

Write-Host "Deploying Service Fabric application..."
Write-Host "  Application Package: $ApplicationPackagePath"
Write-Host "  Publish Profile: $PublishProfileFile"

# Load publish profile
[xml]$publishProfile = Get-Content $PublishProfileFile
$clusterEndpoint = $publishProfile.PublishProfile.ClusterConnectionParameters.ConnectionEndpoint

if ($clusterEndpoint)
{
    Write-Host "  Cluster Endpoint: $clusterEndpoint"
    
    if (-not $UseExistingClusterConnection)
    {
        Connect-ServiceFabricCluster -ConnectionEndpoint $clusterEndpoint
    }
}

# Validate package
if (-not $SkipPackageValidation)
{
    Write-Host "Validating application package..."
    Test-ServiceFabricApplicationPackage -ApplicationPackagePath $ApplicationPackagePath
}

# Copy package to image store
Write-Host "Copying application package to image store..."
Copy-ServiceFabricApplicationPackage -ApplicationPackagePath $ApplicationPackagePath `
    -TimeoutSec $CopyPackageTimeoutSec

# Get application type info from manifest
$appManifestPath = Join-Path $ApplicationPackagePath "ApplicationManifest.xml"
[xml]$appManifest = Get-Content $appManifestPath 
$appTypeName = $appManifest.ApplicationManifest.ApplicationTypeName
$appTypeVersion = $appManifest.ApplicationManifest.ApplicationTypeVersion

# Register application type
Write-Host "Registering application type '$appTypeName' version '$appTypeVersion'..."
Register-ServiceFabricApplicationType -ApplicationPathInImageStore $appTypeName

# Create or upgrade application instance
$appName = "fabric:/$appTypeName"
$existingApp = Get-ServiceFabricApplication -ApplicationName $appName -ErrorAction SilentlyContinue

if ($existingApp)
{
    Write-Host "Upgrading existing application '$appName'..."
    Start-ServiceFabricApplicationUpgrade -ApplicationName $appName `
        -ApplicationTypeVersion $appTypeVersion `
        -Monitored `
        -FailureAction Rollback
}
else
{
    Write-Host "Creating new application '$appName'..."
    New-ServiceFabricApplication -ApplicationName $appName `
        -ApplicationTypeName $appTypeName `
        -ApplicationTypeVersion $appTypeVersion
}

Write-Host "Deployment complete."
