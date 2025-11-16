param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$CSProjFilePath,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$PuckPath
)

# Hardcoded list of package name substrings to look for
# Hardcoded list of Reference Include attributes where HintPath starts with G:\SteamLibrary
$packages = @(
    "0Harmony",
    "Assembly-CSharp-firstpass",
    "AYellowpaper.SerializedCollections",
    "com.rlabrecque.steamworks.net",
    "DebugGUI",
    "DOTween",
    "DOTween.Modules",
    "Linework",
    "Microsoft.Bcl.AsyncInterfaces",
    "Mono.Cecil.Pdb",
    "Mono.Cecil.Rocks",
    "Mono.Security",
    "MonoMod.RuntimeDetour",
    "MonoMod.Utils",
    "Newtonsoft.Json",
    "Open.Nat",
    "Puck",
    "SingularityGroup.HotReload.Runtime",
    "SingularityGroup.HotReload.Runtime.Public",
    "SocketIO.Core",
    "SocketIO.Serializer.Core",
    "SocketIO.Serializer.SystemTextJson",
    "SocketIOClient",
    "System.ComponentModel.Composition",
    "System.EnterpriseServices",
    "System.ServiceModel.Internals",
    "Unity.Burst",
    "Unity.Burst.Unsafe",
    "Unity.Collections",
    "Unity.Collections.LowLevel.ILSupport",
    "Unity.InputSystem",
    "Unity.InputSystem.ForUI",
    "Unity.Mathematics",
    "Unity.MemoryProfiler",
    "Unity.Multiplayer.Center.Common",
    "Unity.Multiplayer.Tools.Adapters",
    "Unity.Multiplayer.Tools.Adapters.Ngo1",
    "Unity.Multiplayer.Tools.Adapters.Ngo1WithUtp2",
    "Unity.Multiplayer.Tools.Adapters.Utp2",
    "Unity.Multiplayer.Tools.Common",
    "Unity.Multiplayer.Tools.Initialization",
    "Unity.Multiplayer.Tools.MetricEvents",
    "Unity.Multiplayer.Tools.MetricTestData",
    "Unity.Multiplayer.Tools.MetricTypes",
    "Unity.Multiplayer.Tools.NetStats",
    "Unity.Multiplayer.Tools.NetStatsMonitor.Component",
    "Unity.Multiplayer.Tools.NetStatsMonitor.Configuration",
    "Unity.Multiplayer.Tools.NetStatsMonitor.Implementation",
    "Unity.Multiplayer.Tools.NetStatsReporting",
    "Unity.Multiplayer.Tools.NetVis.Configuration",
    "Unity.Multiplayer.Tools.NetworkProfiler.Runtime",
    "Unity.Multiplayer.Tools.NetworkSimulator.Runtime",
    "Unity.Multiplayer.Tools.NetworkSolutionInterface",
    "Unity.Netcode.Runtime",
    "Unity.Networking.Transport",
    "Unity.PlayerPrefsEditor.Samples.SampleScene",
    "Unity.Profiling.Core",
    "Unity.Rendering.LightTransport.Runtime",
    "Unity.RenderPipeline.Universal.ShaderLibrary",
    "Unity.RenderPipelines.Core.Runtime",
    "Unity.RenderPipelines.Core.Runtime.Shared",
    "Unity.RenderPipelines.Core.ShaderLibrary",
    "Unity.RenderPipelines.GPUDriven.Runtime",
    "Unity.RenderPipelines.ShaderGraph.ShaderGraphLibrary",
    "Unity.RenderPipelines.Universal.2D.Runtime",
    "Unity.RenderPipelines.Universal.Config.Runtime",
    "Unity.RenderPipelines.Universal.Runtime",
    "Unity.RenderPipelines.Universal.Shaders",
    "Unity.Splines",
    "Unity.TextMeshPro",
    "UnityEngine",
    "UnityEngine.AccessibilityModule",
    "UnityEngine.AIModule",
    "UnityEngine.AMDModule",
    "UnityEngine.AndroidJNIModule",
    "UnityEngine.AnimationModule",
    "UnityEngine.ARModule",
    "UnityEngine.AssetBundleModule",
    "UnityEngine.AudioModule",
    "UnityEngine.ClothModule",
    "UnityEngine.ClusterInputModule",
    "UnityEngine.ClusterRendererModule",
    "UnityEngine.ContentLoadModule",
    "UnityEngine.CoreModule",
    "UnityEngine.CrashReportingModule",
    "UnityEngine.DirectorModule",
    "UnityEngine.DSPGraphModule",
    "UnityEngine.GameCenterModule",
    "UnityEngine.GIModule",
    "UnityEngine.GraphicsStateCollectionSerializerModule",
    "UnityEngine.GridModule",
    "UnityEngine.HierarchyCoreModule",
    "UnityEngine.HotReloadModule",
    "UnityEngine.ImageConversionModule",
    "UnityEngine.IMGUIModule",
    "UnityEngine.InputForUIModule",
    "UnityEngine.InputLegacyModule",
    "UnityEngine.InputModule",
    "UnityEngine.JSONSerializeModule",
    "UnityEngine.LocalizationModule",
    "UnityEngine.MarshallingModule",
    "UnityEngine.MultiplayerModule",
    "UnityEngine.NVIDIAModule",
    "UnityEngine.ParticleSystemModule",
    "UnityEngine.PerformanceReportingModule",
    "UnityEngine.Physics2DModule",
    "UnityEngine.PhysicsModule",
    "UnityEngine.PropertiesModule",
    "UnityEngine.RuntimeInitializeOnLoadManagerInitializerModule",
    "UnityEngine.ScreenCaptureModule",
    "UnityEngine.ShaderVariantAnalyticsModule",
    "UnityEngine.SharedInternalsModule",
    "UnityEngine.SpriteMaskModule",
    "UnityEngine.SpriteShapeModule",
    "UnityEngine.StreamingModule",
    "UnityEngine.SubstanceModule",
    "UnityEngine.SubsystemsModule",
    "UnityEngine.TerrainModule",
    "UnityEngine.TerrainPhysicsModule",
    "UnityEngine.TextCoreFontEngineModule",
    "UnityEngine.TextCoreTextEngineModule",
    "UnityEngine.TextRenderingModule",
    "UnityEngine.TilemapModule",
    "UnityEngine.TLSModule",
    "UnityEngine.UI",
    "UnityEngine.UIElementsModule",
    "UnityEngine.UIModule",
    "UnityEngine.UmbraModule",
    "UnityEngine.UnityAnalyticsCommonModule",
    "UnityEngine.UnityAnalyticsModule",
    "UnityEngine.UnityConnectModule",
    "UnityEngine.UnityCurlModule",
    "UnityEngine.UnityTestProtocolModule",
    "UnityEngine.UnityWebRequestAssetBundleModule",
    "UnityEngine.UnityWebRequestAudioModule",
    "UnityEngine.UnityWebRequestModule",
    "UnityEngine.UnityWebRequestTextureModule",
    "UnityEngine.UnityWebRequestWWWModule",
    "UnityEngine.VehiclesModule",
    "UnityEngine.VFXModule",
    "UnityEngine.VideoModule",
    "UnityEngine.VirtualTexturingModule",
    "UnityEngine.VRModule",
    "UnityEngine.WindModule",
    "UnityEngine.XRModule",
    "uPnP"
)

if (-not (Test-Path -Path $CSProjFilePath)) {
    Write-Error "File not found: $CSProjFilePath"
    exit 1
}

# Load XML
[xml]$doc = Get-Content -Path $CSProjFilePath -Raw

# Ensure backup
$bakPath = "$CSProjFilePath.bak"
Copy-Item -Path $CSProjFilePath -Destination $bakPath -Force

# Get all ItemGroup nodes (works with or without MSBuild default namespace)
$itemGroups = $doc.GetElementsByTagName('ItemGroup')

foreach ($pkg in $packages) {
    $matches = @()
    foreach ($ig in $itemGroups) {
        foreach ($child in $ig.ChildNodes) {
            if ($child.LocalName -eq 'Reference') {
                $includeAttr = $null
                if ($child.Attributes -and $child.Attributes['Include']) {
                    $includeAttr = $child.Attributes['Include'].Value
                }
                $hintNode = $child.SelectSingleNode("*[local-name() = 'HintPath']")
                $hintVal = if ($hintNode) { $hintNode.InnerText } else { '' }

                # Only match if the Include attribute exactly matches a package name
                $matchedPkg = $null
                foreach ($pkg in $packages) {
                    if ($includeAttr -eq $pkg) {
                        $matchedPkg = $pkg
                        break
                    }
                }

                if ($matchedPkg) {
                    $newHint = Join-Path -Path $PuckPath -ChildPath "Puck_Data\Managed\$matchedPkg.dll"
                    if (-not $hintNode) {
                        $hintNode = $doc.CreateElement('HintPath')
                        $child.AppendChild($hintNode) | Out-Null
                    }
                    $hintNode.InnerText = $newHint
                    # Optionally update Include attribute to match the package name exactly
                    if ($child.Attributes -and $child.Attributes['Include']) {
                        $child.Attributes['Include'].Value = $matchedPkg
                    }
                }
            }
        }
    }
}

# Save changes back to file
$doc.Save($CSProjFilePath)
Write-Output "Updated $CSProjFilePath (backup saved to $bakPath)"