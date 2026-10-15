using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoneyTown.Editor
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        [InitializeOnLoadMethod]
        static void OnImport()
        {
            EditorApplication.delayCall += () => {
                if (!Application.isBatchMode && PlayerSettings.productName != "MoneyTown") Prepare();
            };
        }
        [MenuItem("MoneyTown/Prepare Project")]
        public static void Prepare()
        {
            // 초기 프로젝트에만 씬을 생성합니다. 기존 씬 편집 내용은 덮어쓰지 않습니다.
            if (!File.Exists(ScenePath)) {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.043f, .078f, .125f); camera.orthographic = true;
                new GameObject("MoneyTown").AddComponent<GameScreen>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "MoneyTown"; PlayerSettings.productName = "MoneyTown";
            PlayerSettings.bundleVersion = "0.19.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.moneytown.unity");
            PlayerSettings.Android.bundleVersionCode = 27;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // 2D uGUI는 GLES3로 충분하며 에뮬레이터의 ARM64 Vulkan 변환 오류를 피합니다.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultScreenWidth = 480; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            // uGUI StandaloneInputModule은 기존 Input Manager를 사용합니다.
            var settings = new SerializedObject(UnityEditor.Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("MoneyTown project prepared. Open Assets/Scenes/Main.unity and press Play.");
        }
        [MenuItem("MoneyTown/Run Core Checks")]
        public static void Check() { InformationCatalog.Load(Resources.Load<TextAsset>("information-catalog").text,Resources.Load<TextAsset>("information-catalog-v1").text,Resources.Load<TextAsset>("information-catalog-v2").text); Debug.Log($"MoneyTown C# core checks: {CoreChecks.Run()} passed."); }
        [MenuItem("MoneyTown/Build Android APK")]
        public static void BuildAndroid()
        {
            Prepare(); Check();
#if UNITY_ANDROID
            string sdk = Environment.GetEnvironmentVariable("MONEYTOWN_ANDROID_SDK");
            string ndk = Environment.GetEnvironmentVariable("MONEYTOWN_ANDROID_NDK");
            string jdk = Environment.GetEnvironmentVariable("MONEYTOWN_ANDROID_JDK");
            if (!string.IsNullOrEmpty(sdk)) UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdk;
            if (!string.IsNullOrEmpty(ndk)) UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = ndk;
            if (!string.IsNullOrEmpty(jdk)) UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = jdk;
#endif
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity Hub에서 Android Build Support와 SDK, NDK, OpenJDK를 설치하세요.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, "MoneyTown-Unity.apk");
        }
        [MenuItem("MoneyTown/Build Windows Preview")]
        public static void BuildWindows()
        {
            Prepare(); Check(); Build(BuildTarget.StandaloneWindows64, "Windows/MoneyTown.exe");
            File.WriteAllText(Path.GetFullPath("../dist/unity/Windows/build-version.txt"), PlayerSettings.bundleVersion);
        }
        [MenuItem("MoneyTown/Build Isolated UI Verification")]
        public static void BuildVerification()
        {
            Prepare(); Check();
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            try {
                // Unity's own screen preferences must also use a separate registry key.
                PlayerSettings.companyName = PlayerSettings.productName = "MoneyTownVerification";
                Build(BuildTarget.StandaloneWindows64, "Verification/MoneyTown.exe", BuildOptions.Development);
            } finally {
                PlayerSettings.companyName = company; PlayerSettings.productName = product;
                AssetDatabase.SaveAssets();
            }
        }
        static void Build(BuildTarget target, string output, BuildOptions? options = null)
        {
            string path = Path.GetFullPath(Path.Combine("..", "dist", "unity", output));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = path, target = target,
                options = options ?? (target == BuildTarget.StandaloneWindows64 ? BuildOptions.None : BuildOptions.Development)
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Unity build failed: " + report.summary.result);
            Debug.Log("MoneyTown build: " + path);
        }
    }
}
