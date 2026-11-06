using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

/// Quest 3 app (Android, OpenXR): Builds/Quest/BlockTower.apk, installed with adb; multiplayer through the VR server (Server/).
/// XR is set up for Android only, so play mode in the editor stays flat-screen.
/// Menu: BlockTower > Build Quest
public static class VRBuild
{
    const string OutPath = "Builds/Quest/BlockTower.apk";

    [MenuItem("BlockTower/Set Up Quest XR")]
    public static void Setup()
    {
        var android = BuildTargetGroup.Android;
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
        {
            System.IO.Directory.CreateDirectory("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(android))
            perTarget.CreateDefaultSettingsForBuildTarget(android);
        var general = perTarget.SettingsForBuildTarget(android);
        general.InitManagerOnStart = true;
        if (!general.AssignedSettings)
            perTarget.CreateDefaultManagerSettingsForBuildTarget(android);
        XRPackageMetadataStore.AssignLoader(general.AssignedSettings, typeof(OpenXRLoader).FullName, android);

        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(android);
        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(android);
        openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;  // multiview: one pass for both eyes
        string[] wanted =
        {
            "MetaQuestFeature",                    // Quest runtime, Android manifest
            "OculusTouchControllerProfile",        // controllers
            "MetaQuestTouchPlusControllerProfile", // Quest 3 controllers
            "HandTracking",                        // XR Hands joints
            "MetaHandTrackingAim",                 // menu gesture
            "MetaOpenXRHandMeshData",              // the player's own hand shape
            "SessionFocus",                        // the system menu up, the headset off
        };
        foreach (var f in openxr.GetFeatures())
            if (wanted.Contains(f.GetType().Name))
                f.enabled = true;
        EditorUtility.SetDirty(openxr);

        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.seoyeonsoft.blocktower");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
        QualitySettings.antiAliasing = 4;
        // Android refuses "Both": the game only reads the Input System (Controls, the XR rig)
        var player = new SerializedObject(Resources.FindObjectsOfTypeAll<PlayerSettings>()[0]);
        player.FindProperty("activeInputHandler").intValue = 1;
        player.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("VRBuild: Quest XR set up: " + string.Join(", ", openxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)));
    }

    [MenuItem("BlockTower/Build Quest")]
    public static void Build()
    {
        Setup();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { SceneBuilder.ScenePath },
            locationPathName = OutPath,
            target = BuildTarget.Android,
        });
        Debug.Log($"VRBuild: {report.summary.result}, {report.summary.totalSize / 1e6:F1} MB, {report.summary.totalTime}");
    }
}
