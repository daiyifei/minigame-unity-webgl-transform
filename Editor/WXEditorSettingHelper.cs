using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using static WeChatWASM.WXConvertCore;
using System;
using System.Reflection;

namespace WeChatWASM
{

    [InitializeOnLoad]
    public class WXSettingsHelperInterface
    {
        public static WXSettingsHelper helper = new WXSettingsHelper();
    }

    public class WXSettingsHelper
    {
        public static string projectRootPath;

        public WXSettingsHelper()
        {
            Type weixinMiniGamePackageHelpersType = Type.GetType("UnityEditor.WeixinPackageHelpers,UnityEditor");
            if (weixinMiniGamePackageHelpersType != null)
            {
                EventInfo onSettingsGUIEvent = weixinMiniGamePackageHelpersType.GetEvent("OnPackageSettingsGUI");
                EventInfo onPackageFocusEvent = weixinMiniGamePackageHelpersType.GetEvent("OnPackageFocus");
                EventInfo onPackageLostFocusEvent = weixinMiniGamePackageHelpersType.GetEvent("OnPackageLostFocus");
                EventInfo onBuildButtonGUIEvent = weixinMiniGamePackageHelpersType.GetEvent("OnPackageBuildButtonGUI");

                if (onPackageFocusEvent != null)
                {
                    onPackageFocusEvent.AddEventHandler(null, new Action(OnFocus));
                }

                if (onPackageLostFocusEvent != null)
                {
                    onPackageLostFocusEvent.AddEventHandler(null, new Action(OnLostFocus));
                }
                if (onSettingsGUIEvent != null)
                {
                    onSettingsGUIEvent.AddEventHandler(null, new Action<EditorWindow>(OnSettingsGUI));
                }
                if (onBuildButtonGUIEvent != null)
                {
                    onBuildButtonGUIEvent.AddEventHandler(null, new Action<EditorWindow>(OnBuildButtonGUI));
                }

            }

            //loadData();
            foldInstantGame = WXConvertCore.IsInstantGameAutoStreaming();

            projectRootPath = System.IO.Path.GetFullPath(Application.dataPath + "/../");

            _dstCache = "";
        }

        private static WXEditorScriptObject config;
        private static bool m_EnablePerfTool = false;

        private static string _dstCache;

        public void OnFocus()
        {
            loadData();
        }

        public void OnLostFocus()
        {
            saveData();
        }

        public void OnDisable()
        {
            EditorUtility.SetDirty(config);
        }

        public Texture tex;
        public void OnSettingsGUI(EditorWindow window)
        {
            PluginUpdateManager.CheckUpdateOnce();
            scrollRoot = EditorGUILayout.BeginScrollView(scrollRoot);

            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = Color.yellow;
            linkStyle.hover.textColor = Color.yellow;
            linkStyle.stretchWidth = false;
            linkStyle.alignment = TextAnchor.UpperLeft;
            linkStyle.wordWrap = true;

            foldBaseInfo = EditorGUILayout.Foldout(foldBaseInfo, "Basic Information");
            if (foldBaseInfo)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));

                this.formInput("appid", "Game AppID");
                this.formInput("cdn", "Game Resource CDN");
                this.formInput("projectName", "Mini Game Project Name");
                this.formIntPopup("orientation", "Game Orientation", new[] { "Portrait", "Landscape", "LandscapeLeft", "LandscapeRight" }, new[] { 0, 1, 2, 3 });
                this.formInput("memorySize", "UnityHeap Reserved Memory(?)", "Unit MB, pre-allocated memory value, casual games 256/medium games 496/heavy games 768, need to estimate the maximum UnityHeap value to prevent memory spikes from automatic expansion. Please check GIT documentation 'Optimizing Unity WebGL Memory' for estimation method");

                GUILayout.BeginHorizontal();
                string targetDst = "dst";
                if (!formInputData.ContainsKey(targetDst))
                {
                    formInputData[targetDst] = "";
                }
                
                GUILayout.Label(new GUIContent("Export Path(?)", "Supports relative path input relative to project root directory, e.g.: wxbuild"), GUILayout.Width(220));
                formInputData[targetDst] = GUILayout.TextField(formInputData[targetDst], GUILayout.ExpandWidth(true));
                if (GUILayout.Button(new GUIContent("Open"), GUILayout.Width(40)))
                {
                    if (!formInputData[targetDst].Trim().Equals(string.Empty))
                    {
                        EditorUtility.RevealInFinder(GetAbsolutePath(formInputData[targetDst]));
                    }
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("Select"), GUILayout.Width(50)))
                {
                    var dstPath = EditorUtility.SaveFolderPanel("Select your game export directory", string.Empty, string.Empty);
                    if (dstPath != string.Empty)
                    {
                        formInputData[targetDst] = dstPath;
                        this.saveData();
                    }
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();


                EditorGUILayout.EndVertical();
            }

            foldLoadingConfig = EditorGUILayout.Foldout(foldLoadingConfig, "Loading Configuration");
            if (foldLoadingConfig)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));

                GUILayout.BeginHorizontal();
                string targetBg = "bgImageSrc";
                
                GUILayout.Label("Startup Background/Video Cover", GUILayout.Width(220));
                tex = (Texture)EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.ExpandWidth(true));
                var currentBgSrc = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(currentBgSrc) && currentBgSrc != this.formInputData[targetBg])
                {
                    this.formInputData[targetBg] = currentBgSrc;
                    this.saveData();
                }
                GUILayout.EndHorizontal();

                this.formInput("videoUrl", "Loading Stage Video URL");
                this.formIntPopup("assetLoadType", "Initial Package Loading Method", new[] { "CDN", "Mini Game Package" }, new[] { 0, 1 });
                this.formCheckbox("compressDataPackage", "Compress Initial Package(?)", "Compress initial package resources with Brotli to reduce resource size. Note: First startup time may increase by 200ms, only recommended when using mini game subpackages to save package size");
                this.formInput("bundleExcludeExtensions", "File Types Not Auto-cached(?)", "(Separated by ;) When URL contains 'cdn+StreamingAssets', it will auto-cache, but not all files in StreamingAssets need caching, this option configures file extensions not to auto-cache. Default value: json");
                this.formInput("bundleHashLength", "Bundle Name Hash Length(?)", "Customize hash part length in Bundle filename, default 32, used for cache control.");
                this.formInput("preloadFiles", "Preload File List(?)", "Use ; as separator, supports fuzzy matching");

                EditorGUILayout.EndVertical();
            }

            foldSDKOptions = EditorGUILayout.Foldout(foldSDKOptions, "SDK Feature Options");
            if (foldSDKOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));

                this.formCheckbox("useFriendRelation", "Use Friend Relationship");
                this.formCheckbox("useMiniGameChat", "Use Social Components");
                this.formCheckbox("preloadWXFont", "Preload WeChat Font(?)", "Preload WeChat system font at game.js execution start, can use WX.GetWXFont to get WeChat font during runtime");
                this.formCheckbox("disableMultiTouch", "Disable Multi-touch");

                EditorGUILayout.EndVertical();
            }

            foldDebugOptions = EditorGUILayout.Foldout(foldDebugOptions, "Debug Build Options");
            if (foldDebugOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));


                this.formCheckbox("developBuild", "Development Build", "", false, null, OnDevelopmentBuildToggleChanged);
                this.formCheckbox("autoProfile", "Auto connect Profiler");
                this.formCheckbox("scriptOnly", "Scripts Only Build");
                this.formCheckbox("il2CppOptimizeSize", "Il2Cpp Optimize Size(?)", "Corresponds to Il2CppCodeGeneration option, when checked uses OptimizeSize (recommended default), generates about 15% smaller code, when unchecked uses OptimizeSpeed. Games with frequent access to generic collections recommend OptimizeSpeed, when using third-party components like HybridCLR can only use OptimizeSpeed. (This option is invalid in Dotnet Runtime mode)", !UseIL2CPP);
                this.formCheckbox("profilingFuncs", "Profiling Funcs");
                this.formCheckbox("profilingMemory", "Profiling Memory");
                this.formCheckbox("webgl2", "WebGL2.0(beta)");
                this.formCheckbox("iOSPerformancePlus", "iOSPerformancePlus(?)", "Whether to use iOS high performance+ rendering solution, helps improve rendering compatibility and reduce WebContent process memory");
                this.formCheckbox("deleteStreamingAssets", "Clear Streaming Assets");
                this.formCheckbox("cleanBuild", "Clean WebGL Build");
                // this.formCheckbox("cleanCloudDev", "Clean Cloud Dev");
                this.formCheckbox("fbslim", "Initial Package Optimization(?)", "Automatically clean up resources that are packaged by UnityEditor by default but never used by the game project during export, slim down initial package size. (Unity engine no longer needs to enable this capability)", UnityUtil.GetEngineVersion() > 0, (res) =>
                {
                    var fbWin = EditorWindow.GetWindow(typeof(WXFbSettingWindow), false, "Initial Package Optimization Configuration Panel", true);
                    fbWin.minSize = new Vector2(680, 350);
                    fbWin.Show();
                });
                this.formCheckbox("autoAdaptScreen", "Auto Adapt Screen Size(?)", "Automatically adjust canvas size when rotating screen on mobile or resizing window on PC");
                this.formCheckbox("showMonitorSuggestModal", "Show Optimization Suggestion Modal");
                this.formCheckbox("enableProfileStats", "Show Performance Panel");
                this.formCheckbox("enableRenderAnalysis", "Show Render Logs(dev only)");
                this.formCheckbox("brotliMT", "Brotli Multi-thread Compression(?)", "Enable multi-thread compression can improve packaging speed but will reduce compression ratio. Do not use multi-thread packaging for online if not using wasm code subpackaging");
#if UNITY_6000_0_OR_NEWER
                this.formCheckbox("enableWasm2023", "WebAssembly 2023(?)", "WebAssembly 2023 includes support for WebAssembly.Table and BigInt. (Android (Android 10 or later recommended), iOS (iOS 15 or later recommended))");
#endif

                if (m_EnablePerfTool)
                {
                    this.formCheckbox("enablePerfAnalysis", "Integrate Performance Analysis Tool", "Integrate performance analysis tool into Development Build package", false, null, OnPerfAnalysisFeatureToggleChanged);
                }

                EditorGUILayout.EndVertical();
            }


#if UNITY_INSTANTGAME
            foldInstantGame = EditorGUILayout.Foldout(foldInstantGame, "Instant Game - AutoStreaming");
            if (foldInstantGame)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                this.formInput("bundlePathIdentifier", "Bundle Path Identifier");
                this.formInput("dataFileSubPrefix", "Data File Sub Prefix");

                EditorGUI.BeginDisabledGroup(true);
                this.formCheckbox("autoUploadFirstBundle", "Build and Auto-upload First Bundle(?)", "Only effective when AutoStreaming is enabled", true);
                EditorGUI.EndDisabledGroup();

                GUILayout.BeginHorizontal();
                
                GUILayout.Label(new GUIContent("Clear AS Configuration(?)", "If you want to close AutoStreaming and use default publishing scheme, you need to clear AS configuration project."), GUILayout.Width(140));
                EditorGUI.BeginDisabledGroup(WXConvertCore.IsInstantGameAutoStreaming());
                if(GUILayout.Button(new GUIContent("Restore"),GUILayout.Width(60))){
                    string identifier = config.ProjectConf.bundlePathIdentifier;
                    string[] identifiers = identifier.Split(";");
                    string idStr = "";
                    foreach (string id in identifiers)
                    {
                        if (id != "AS" && id != "CUS/CustomAB")
                        {
                            idStr += id + ";";
                        }
                    }
                    config.ProjectConf.bundlePathIdentifier = idStr.Trim(';');
                    if (config.ProjectConf.dataFileSubPrefix == "CUS")
                    {
                        config.ProjectConf.dataFileSubPrefix = "";
                    }
                    this.loadData();
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.Empty);
                if (GUILayout.Button(new GUIContent("Learn Instant Game AutoStreaming", ""), linkStyle))
                {
                    Application.OpenURL("https://github.com/wechat-miniprogram/minigame-unity-webgl-transform/blob/main/Design/InstantGameGuide.md");
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
#endif
            foldFontOptions = EditorGUILayout.Foldout(foldFontOptions, "Font Configuration");
            if (foldFontOptions)
            {
                EditorGUILayout.BeginVertical("frameBox", GUILayout.ExpandWidth(true));
                this.formCheckbox("CJK_Unified_Ideographs", "Basic Chinese Characters(?)", "Unicode [0x4e00, 0x9fff]");
                this.formCheckbox("C0_Controls_and_Basic_Latin", "Basic Latin (English uppercase/lowercase, numbers, punctuation)(?)", "Unicode [0x0, 0x7f]");
                this.formCheckbox("CJK_Symbols_and_Punctuation", "Chinese Punctuation(?)", "Unicode [0x3000, 0x303f]");
                this.formCheckbox("General_Punctuation", "General Punctuation(?)", "Unicode [0x2000, 0x206f]");
                this.formCheckbox("Enclosed_CJK_Letters_and_Months", "CJK Letters and Months(?)", "Unicode [0x3200, 0x32ff]");
                this.formCheckbox("Vertical_Forms", "Chinese Vertical Punctuation(?)", "Unicode [0xfe10, 0xfe1f]");
                this.formCheckbox("CJK_Compatibility_Forms", "CJK Compatibility Symbols(?)", "Unicode [0xfe30, 0xfe4f]");
                this.formCheckbox("Miscellaneous_Symbols", "Miscellaneous Symbols(?)", "Unicode [0x2600, 0x26ff]");
                this.formCheckbox("CJK_Compatibility", "CJK Special Symbols(?)", "Unicode [0x3300, 0x33ff]");
                this.formCheckbox("Halfwidth_and_Fullwidth_Forms", "Full-width ASCII, Chinese/English Punctuation, Half-width Kana/Hiragana/Korean(?)", "Unicode [0xff00, 0xffef]");
                this.formCheckbox("Dingbats", "Decorative Symbols(?)", "Unicode [0x2700, 0x27bf]");
                this.formCheckbox("Letterlike_Symbols", "Letter-like Symbols(?)", "Unicode [0x2100, 0x214f]");
                this.formCheckbox("Enclosed_Alphanumerics", "Enclosed Alphanumerics(?)", "Unicode [0x2460, 0x24ff]");
                this.formCheckbox("Number_Forms", "Number Forms(?)", "Unicode [0x2150, 0x218f]");
                this.formCheckbox("Currency_Symbols", "Currency Symbols(?)", "Unicode [0x20a0, 0x20cf]");
                this.formCheckbox("Arrows", "Arrows(?)", "Unicode [0x2190, 0x21ff]");
                this.formCheckbox("Geometric_Shapes", "Geometric Shapes(?)", "Unicode [0x25a0, 0x25ff]");
                this.formCheckbox("Mathematical_Operators", "Mathematical Operators(?)", "Unicode [0x2200, 0x22ff]");
                this.formInput("CustomUnicode", "Custom Unicode(?)", "Force add all input characters to font preload list");
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        public void OnBuildButtonGUI(EditorWindow window)
        {
            GUIStyle linkStyle = new GUIStyle(GUI.skin.label);
            linkStyle.normal.textColor = Color.yellow;
            linkStyle.hover.textColor = Color.yellow;
            linkStyle.stretchWidth = false;
            linkStyle.alignment = TextAnchor.UpperLeft;
            linkStyle.wordWrap = true;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("More Configuration"), GUILayout.Width(120), GUILayout.Height(25)))
            {
                var minigameConfig = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset");
                Selection.activeObject = minigameConfig;
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("WebGL to Mini Game(Rarely Used)"), GUILayout.Width(210), GUILayout.Height(25)))
            {
                this.saveData();
                if (WXConvertCore.DoExport(false) == WXConvertCore.WXExportError.SUCCEED)
                {
                    window.ShowNotification(new GUIContent("Conversion Complete"));
                }

                GUIUtility.ExitGUI();
            }
            EditorGUILayout.LabelField(string.Empty, GUILayout.MinWidth(10));
            if (GUILayout.Button(new GUIContent("Generate and Convert"), GUILayout.Width(140), GUILayout.Height(25)))
            {
                this.saveData();
                if (WXConvertCore.DoExport() == WXConvertCore.WXExportError.SUCCEED)
                {
                    if (!WXConvertCore.IsInstantGameAutoStreaming())
                        window.ShowNotification(new GUIContent("Conversion Complete"));
                    else
                    {
#if (UNITY_WEBGL || WEIXINMINIGAME) && UNITY_INSTANTGAME
                        // Upload initial package resources
                        if (!string.IsNullOrEmpty(WXConvertCore.FirstBundlePath) && File.Exists(WXConvertCore.FirstBundlePath))
                        {
                            if (Unity.InstantGame.IGBuildPipeline.UploadWeChatDataFile(WXConvertCore.FirstBundlePath))
                            {
                                Debug.Log("Conversion complete and initial package resources uploaded successfully");
                                window.ShowNotification(new GUIContent("Conversion and upload complete"));
                            }
                            else
                            {
                                Debug.LogError("Initial package resource upload failed, please check network and Auto Streaming configuration.");
                                window.ShowNotification(new GUIContent("Upload failed"));
                            }
                        }
                        else
                        {
                            Debug.LogError("Conversion failed");
                            window.ShowNotification(new GUIContent("Conversion failed"));
                        }
#else
                        window.ShowNotification(new GUIContent("Conversion Complete"));
#endif
                    }
                }
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty);
            if (GUILayout.Button(new GUIContent("Learn how to implement custom build", ""), linkStyle))
            {
                Application.OpenURL("https://wechat-miniprogram.github.io/minigame-unity-webgl-transform/Design/DevelopmentQAList.html#_13-%E5%A6%82%E4%BD%95%E8%87%AA%E5%AE%9A%E4%B9%89%E6%8E%A5%E5%85%A5%E6%9E%84%E5%BB%BA%E6%B5%81%E7%A8%8B");
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        private Vector2 scrollRoot;
        private bool foldBaseInfo = true;
        private bool foldLoadingConfig = true;
        private bool foldSDKOptions = true;
        private bool foldDebugOptions = true;
        private bool foldInstantGame = false;
        private bool foldFontOptions = false;
        private Dictionary<string, string> formInputData = new Dictionary<string, string>();
        private Dictionary<string, int> formIntPopupData = new Dictionary<string, int>();
        private Dictionary<string, bool> formCheckboxData = new Dictionary<string, bool>();

        private string SDKFilePath;

        private void addBundlePathIdentifier(string value)
        {
            string identifier = config.ProjectConf.bundlePathIdentifier;
            if (identifier[identifier.Length - 1] != ';')
            {
                identifier += ";";
            }
            identifier += value;
            config.ProjectConf.bundlePathIdentifier = identifier;
        }
        private void loadData()
        {
            // SDKFilePath = Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Runtime", "wechat-default", "unity-sdk", "index.js");
            SDKFilePath = Path.Combine(UnityUtil.GetWxSDKRootPath(), "Runtime", "wechat-default", "unity-sdk", "index.js");
            config = UnityUtil.GetEditorConf();
            _dstCache = config.ProjectConf.DST;

            // Instant Game
            if (WXConvertCore.IsInstantGameAutoStreaming())
            {
                config.ProjectConf.CDN = WXConvertCore.GetInstantGameAutoStreamingCDN();
                string identifier = config.ProjectConf.bundlePathIdentifier;
                string[] identifiers = identifier.Split(';');
                bool AS = false;
                bool CUS = false;
                foreach (string id in identifiers)
                {
                    if (id == "AS")
                    {
                        AS = true;
                    }
                    if (id == "CUS/CustomAB")
                    {
                        CUS = true;
                    }
                }
                if (!AS)
                {
                    this.addBundlePathIdentifier("AS");
                }
                if (!CUS)
                {
                    this.addBundlePathIdentifier("CUS/CustomAB");
                }
                if (config.ProjectConf.dataFileSubPrefix != "CUS")
                {
                    config.ProjectConf.dataFileSubPrefix = "CUS";
                }
            }

            this.setData("projectName", config.ProjectConf.projectName);
            this.setData("appid", config.ProjectConf.Appid);
            this.setData("cdn", config.ProjectConf.CDN);
            this.setData("assetLoadType", config.ProjectConf.assetLoadType);
            this.setData("compressDataPackage", config.ProjectConf.compressDataPackage);
            this.setData("videoUrl", config.ProjectConf.VideoUrl);
            this.setData("orientation", (int)config.ProjectConf.Orientation);
            this.setData("dst", _dstCache);
            this.setData("bundleHashLength", config.ProjectConf.bundleHashLength.ToString());
            this.setData("bundlePathIdentifier", config.ProjectConf.bundlePathIdentifier);
            this.setData("bundleExcludeExtensions", config.ProjectConf.bundleExcludeExtensions);
            this.setData("preloadFiles", config.ProjectConf.preloadFiles);
            this.setData("developBuild", config.CompileOptions.DevelopBuild);
            this.setData("autoProfile", config.CompileOptions.AutoProfile);
            this.setData("scriptOnly", config.CompileOptions.ScriptOnly);
            this.setData("il2CppOptimizeSize", config.CompileOptions.Il2CppOptimizeSize);
            this.setData("profilingFuncs", config.CompileOptions.profilingFuncs);
            this.setData("profilingMemory", config.CompileOptions.ProfilingMemory);
            this.setData("deleteStreamingAssets", config.CompileOptions.DeleteStreamingAssets);
            this.setData("cleanBuild", config.CompileOptions.CleanBuild);
            this.setData("customNodePath", config.CompileOptions.CustomNodePath);
            this.setData("webgl2", config.CompileOptions.Webgl2);
            this.setData("iOSPerformancePlus", config.CompileOptions.enableIOSPerformancePlus);
            this.setData("fbslim", config.CompileOptions.fbslim);
            this.setData("useFriendRelation", config.SDKOptions.UseFriendRelation);
            this.setData("useMiniGameChat", config.SDKOptions.UseMiniGameChat);
            this.setData("preloadWXFont", config.SDKOptions.PreloadWXFont);
            this.setData("disableMultiTouch", config.SDKOptions.disableMultiTouch);
            this.setData("bgImageSrc", config.ProjectConf.bgImageSrc);
            tex = AssetDatabase.LoadAssetAtPath<Texture>(config.ProjectConf.bgImageSrc);
            this.setData("memorySize", config.ProjectConf.MemorySize.ToString());
            this.setData("hideAfterCallMain", config.ProjectConf.HideAfterCallMain);

            this.setData("dataFileSubPrefix", config.ProjectConf.dataFileSubPrefix);
            this.setData("maxStorage", config.ProjectConf.maxStorage.ToString());
            this.setData("defaultReleaseSize", config.ProjectConf.defaultReleaseSize.ToString());
            this.setData("texturesHashLength", config.ProjectConf.texturesHashLength.ToString());
            this.setData("texturesPath", config.ProjectConf.texturesPath);
            this.setData("needCacheTextures", config.ProjectConf.needCacheTextures);
            this.setData("loadingBarWidth", config.ProjectConf.loadingBarWidth.ToString());
            this.setData("needCheckUpdate", config.ProjectConf.needCheckUpdate);
            this.setData("disableHighPerformanceFallback", config.ProjectConf.disableHighPerformanceFallback);
            this.setData("autoAdaptScreen", config.CompileOptions.autoAdaptScreen);
            this.setData("showMonitorSuggestModal", config.CompileOptions.showMonitorSuggestModal);
            this.setData("enableProfileStats", config.CompileOptions.enableProfileStats);
            this.setData("enableRenderAnalysis", config.CompileOptions.enableRenderAnalysis);
            this.setData("brotliMT", config.CompileOptions.brotliMT);
#if UNITY_6000_0_OR_NEWER
            this.setData("enableWasm2023", config.CompileOptions.enableWasm2023);
#endif      
            this.setData("enablePerfAnalysis", config.CompileOptions.enablePerfAnalysis);
            this.setData("autoUploadFirstBundle", true);

            // font options
            this.setData("CJK_Unified_Ideographs", config.FontOptions.CJK_Unified_Ideographs);
            this.setData("C0_Controls_and_Basic_Latin", config.FontOptions.C0_Controls_and_Basic_Latin);
            this.setData("CJK_Symbols_and_Punctuation", config.FontOptions.CJK_Symbols_and_Punctuation);
            this.setData("General_Punctuation", config.FontOptions.General_Punctuation);
            this.setData("Enclosed_CJK_Letters_and_Months", config.FontOptions.Enclosed_CJK_Letters_and_Months);
            this.setData("Vertical_Forms", config.FontOptions.Vertical_Forms);
            this.setData("CJK_Compatibility_Forms", config.FontOptions.CJK_Compatibility_Forms);
            this.setData("Miscellaneous_Symbols", config.FontOptions.Miscellaneous_Symbols);
            this.setData("CJK_Compatibility", config.FontOptions.CJK_Compatibility);
            this.setData("Halfwidth_and_Fullwidth_Forms", config.FontOptions.Halfwidth_and_Fullwidth_Forms);
            this.setData("Dingbats", config.FontOptions.Dingbats);
            this.setData("Letterlike_Symbols", config.FontOptions.Letterlike_Symbols);
            this.setData("Enclosed_Alphanumerics", config.FontOptions.Enclosed_Alphanumerics);
            this.setData("Number_Forms", config.FontOptions.Number_Forms);
            this.setData("Currency_Symbols", config.FontOptions.Currency_Symbols);
            this.setData("Arrows", config.FontOptions.Arrows);
            this.setData("Geometric_Shapes", config.FontOptions.Geometric_Shapes);
            this.setData("Mathematical_Operators", config.FontOptions.Mathematical_Operators);
            this.setData("CustomUnicode", config.FontOptions.CustomUnicode);
        }

        private void saveData()
        {
            config.ProjectConf.projectName = this.getDataInput("projectName");
            config.ProjectConf.Appid = this.getDataInput("appid");
            config.ProjectConf.CDN = this.getDataInput("cdn");
            config.ProjectConf.assetLoadType = this.getDataPop("assetLoadType");
            config.ProjectConf.compressDataPackage = this.getDataCheckbox("compressDataPackage");
            config.ProjectConf.VideoUrl = this.getDataInput("videoUrl");
            config.ProjectConf.Orientation = (WXScreenOritation)this.getDataPop("orientation");
            _dstCache = this.getDataInput("dst");
            config.ProjectConf.DST = GetAbsolutePath(_dstCache);
            config.ProjectConf.bundleHashLength = int.Parse(this.getDataInput("bundleHashLength"));
            config.ProjectConf.bundlePathIdentifier = this.getDataInput("bundlePathIdentifier");
            config.ProjectConf.bundleExcludeExtensions = this.getDataInput("bundleExcludeExtensions");
            config.ProjectConf.preloadFiles = this.getDataInput("preloadFiles");
            config.CompileOptions.DevelopBuild = this.getDataCheckbox("developBuild");
            config.CompileOptions.AutoProfile = this.getDataCheckbox("autoProfile");
            config.CompileOptions.ScriptOnly = this.getDataCheckbox("scriptOnly");
            config.CompileOptions.Il2CppOptimizeSize = this.getDataCheckbox("il2CppOptimizeSize");
            config.CompileOptions.profilingFuncs = this.getDataCheckbox("profilingFuncs");
            config.CompileOptions.ProfilingMemory = this.getDataCheckbox("profilingMemory");
            config.CompileOptions.DeleteStreamingAssets = this.getDataCheckbox("deleteStreamingAssets");
            config.CompileOptions.CleanBuild = this.getDataCheckbox("cleanBuild");
            config.CompileOptions.CustomNodePath = this.getDataInput("customNodePath");
            config.CompileOptions.Webgl2 = this.getDataCheckbox("webgl2");
            config.CompileOptions.enableIOSPerformancePlus = this.getDataCheckbox("iOSPerformancePlus");
            config.CompileOptions.fbslim = this.getDataCheckbox("fbslim");
            config.SDKOptions.UseFriendRelation = this.getDataCheckbox("useFriendRelation");
            config.SDKOptions.UseMiniGameChat = this.getDataCheckbox("useMiniGameChat");
            config.SDKOptions.PreloadWXFont = this.getDataCheckbox("preloadWXFont");
            config.SDKOptions.disableMultiTouch = this.getDataCheckbox("disableMultiTouch");
            config.ProjectConf.bgImageSrc = this.getDataInput("bgImageSrc");
            config.ProjectConf.MemorySize = int.Parse(this.getDataInput("memorySize"));
            config.ProjectConf.HideAfterCallMain = this.getDataCheckbox("hideAfterCallMain");
            config.ProjectConf.dataFileSubPrefix = this.getDataInput("dataFileSubPrefix");
            config.ProjectConf.maxStorage = int.Parse(this.getDataInput("maxStorage"));
            config.ProjectConf.defaultReleaseSize = int.Parse(this.getDataInput("defaultReleaseSize"));
            config.ProjectConf.texturesHashLength = int.Parse(this.getDataInput("texturesHashLength"));
            config.ProjectConf.texturesPath = this.getDataInput("texturesPath");
            config.ProjectConf.needCacheTextures = this.getDataCheckbox("needCacheTextures");
            config.ProjectConf.loadingBarWidth = int.Parse(this.getDataInput("loadingBarWidth"));
            config.ProjectConf.needCheckUpdate = this.getDataCheckbox("needCheckUpdate");
            config.ProjectConf.disableHighPerformanceFallback = this.getDataCheckbox("disableHighPerformanceFallback");
            config.CompileOptions.autoAdaptScreen = this.getDataCheckbox("autoAdaptScreen");
            config.CompileOptions.showMonitorSuggestModal = this.getDataCheckbox("showMonitorSuggestModal");
            config.CompileOptions.enableProfileStats = this.getDataCheckbox("enableProfileStats");
            config.CompileOptions.enableRenderAnalysis = this.getDataCheckbox("enableRenderAnalysis");
            config.CompileOptions.brotliMT = this.getDataCheckbox("brotliMT");
#if UNITY_6000_0_OR_NEWER
            config.CompileOptions.enableWasm2023 = this.getDataCheckbox("enableWasm2023");
#endif
            config.CompileOptions.enablePerfAnalysis = this.getDataCheckbox("enablePerfAnalysis");

            // font options
            config.FontOptions.CJK_Unified_Ideographs = this.getDataCheckbox("CJK_Unified_Ideographs");
            config.FontOptions.C0_Controls_and_Basic_Latin = this.getDataCheckbox("C0_Controls_and_Basic_Latin");
            config.FontOptions.CJK_Symbols_and_Punctuation = this.getDataCheckbox("CJK_Symbols_and_Punctuation");
            config.FontOptions.General_Punctuation = this.getDataCheckbox("General_Punctuation");
            config.FontOptions.Enclosed_CJK_Letters_and_Months = this.getDataCheckbox("Enclosed_CJK_Letters_and_Months");
            config.FontOptions.Vertical_Forms = this.getDataCheckbox("Vertical_Forms");
            config.FontOptions.CJK_Compatibility_Forms = this.getDataCheckbox("CJK_Compatibility_Forms");
            config.FontOptions.Miscellaneous_Symbols = this.getDataCheckbox("Miscellaneous_Symbols");
            config.FontOptions.CJK_Compatibility = this.getDataCheckbox("CJK_Compatibility");
            config.FontOptions.Halfwidth_and_Fullwidth_Forms = this.getDataCheckbox("Halfwidth_and_Fullwidth_Forms");
            config.FontOptions.Dingbats = this.getDataCheckbox("Dingbats");
            config.FontOptions.Letterlike_Symbols = this.getDataCheckbox("Letterlike_Symbols");
            config.FontOptions.Enclosed_Alphanumerics = this.getDataCheckbox("Enclosed_Alphanumerics");
            config.FontOptions.Number_Forms = this.getDataCheckbox("Number_Forms");
            config.FontOptions.Currency_Symbols = this.getDataCheckbox("Currency_Symbols");
            config.FontOptions.Arrows = this.getDataCheckbox("Arrows");
            config.FontOptions.Geometric_Shapes = this.getDataCheckbox("Geometric_Shapes");
            config.FontOptions.Mathematical_Operators = this.getDataCheckbox("Mathematical_Operators");
            config.FontOptions.CustomUnicode = this.getDataInput("CustomUnicode");

            ApplyPerfAnalysisSetting();
        }

        private string getDataInput(string target)
        {
            if (this.formInputData.ContainsKey(target))
                return this.formInputData[target];
            return "";
        }
        private int getDataPop(string target)
        {
            if (this.formIntPopupData.ContainsKey(target))
                return this.formIntPopupData[target];
            return 0;
        }
        private bool getDataCheckbox(string target)
        {
            if (this.formCheckboxData.ContainsKey(target))
                return this.formCheckboxData[target];
            return false;
        }

        private void setData(string target, string value)
        {
            if (formInputData.ContainsKey(target))
            {
                formInputData[target] = value;
            }
            else
            {
                formInputData.Add(target, value);
            }
        }
        private void setData(string target, bool value)
        {
            if (formCheckboxData.ContainsKey(target))
            {
                formCheckboxData[target] = value;
            }
            else
            {
                formCheckboxData.Add(target, value);
            }
        }
        private void setData(string target, int value)
        {
            if (formIntPopupData.ContainsKey(target))
            {
                formIntPopupData[target] = value;
            }
            else
            {
                formIntPopupData.Add(target, value);
            }
        }
        private void formInput(string target, string label, string help = null)
        {
            if (!formInputData.ContainsKey(target))
            {
                formInputData[target] = "";
            }
            GUILayout.BeginHorizontal();
            
            if (help == null)
            {
                GUILayout.Label(label, GUILayout.Width(220));
            }
            else
            {
                GUILayout.Label(new GUIContent(label, help), GUILayout.Width(220));
            }
            formInputData[target] = GUILayout.TextField(formInputData[target], GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private void formIntPopup(string target, string label, string[] options, int[] values)
        {
            if (!formIntPopupData.ContainsKey(target))
            {
                formIntPopupData[target] = 0;
            }
            GUILayout.BeginHorizontal();
            
            GUILayout.Label(label, GUILayout.Width(220));
            formIntPopupData[target] = EditorGUILayout.IntPopup(formIntPopupData[target], options, values, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private void formCheckbox(string target, string label, string help = null, bool disable = false, Action<bool> setting = null, Action<bool> onValueChanged = null)
        {
            if (!formCheckboxData.ContainsKey(target))
            {
                formCheckboxData[target] = false;
            }
            GUILayout.BeginHorizontal();
            
            if (help == null)
            {
                GUILayout.Label(label, GUILayout.Width(220));
            }
            else
            {
                GUILayout.Label(new GUIContent(label, help), GUILayout.Width(220));
            }
            EditorGUI.BeginDisabledGroup(disable);

            // Toggle the checkbox value based on the disable condition
            bool newValue = EditorGUILayout.Toggle(disable ? false : formCheckboxData[target], GUILayout.Width(20));
            // Update the checkbox data if the value has changed and invoke the onValueChanged action
            if (newValue != formCheckboxData[target])
            {
                formCheckboxData[target] = newValue;
                onValueChanged?.Invoke(newValue);
            }

            if (setting != null)
            {
                EditorGUILayout.LabelField("", GUILayout.Width(10));
                // Configuration button
                if (GUILayout.Button(new GUIContent("Config"), GUILayout.Width(50), GUILayout.Height(18)))
                {
                    setting?.Invoke(true);
                }
            }

            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace(); // 添加弹性空间，使复选框右对齐
            GUILayout.EndHorizontal();
        }

        private void OnDevelopmentBuildToggleChanged(bool InNewValue)
        {
            // For non-dev build, disable performance analysis tool integration
            if (!InNewValue)
            {
                this.setData("enablePerfAnalysis", false);
            }
        }

        private void OnPerfAnalysisFeatureToggleChanged(bool InNewValue)
        {
            // For non-dev build, disable performance analysis tool integration
            if (!formCheckboxData["developBuild"] && InNewValue)
            {
                this.setData("enablePerfAnalysis", false);
            }
        }

        private void ApplyPerfAnalysisSetting()
        {
            const string MACRO_ENABLE_WX_PERF_FEATURE = "ENABLE_WX_PERF_FEATURE";
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            bool shouldAddSymbol = this.getDataCheckbox("enablePerfAnalysis") && this.getDataCheckbox("developBuild");

#if !UNITY_2021_2_OR_NEWER || UNITY_2023_2_OR_NEWER
            if (shouldAddSymbol)
            {
                shouldAddSymbol = false;
                EditorUtility.DisplayDialog("Warning", $"Current Unity version ({Application.unityVersion}) is not in the supported range (2021.2-2023.1) for performance analysis tool, the tool will be disabled.", "OK");
                config.CompileOptions.enablePerfAnalysis = false;
                this.setData("enablePerfAnalysis", false);
            }
#endif

            if (shouldAddSymbol)
            {
                if (defineSymbols.IndexOf(MACRO_ENABLE_WX_PERF_FEATURE) == -1)
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, MACRO_ENABLE_WX_PERF_FEATURE + $";{defineSymbols}");
                }
            }
            else
            {
                // Remove existing ENABLE_WX_PERF_FEATURE
                if (defineSymbols.IndexOf(MACRO_ENABLE_WX_PERF_FEATURE) != -1)
                {
                    defineSymbols = defineSymbols.Replace(MACRO_ENABLE_WX_PERF_FEATURE, "").Replace(";;", ";").Trim(';');
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, defineSymbols);
                }
            }
        }

        public static bool IsAbsolutePath(string path)
        {
            // Check if empty or whitespace
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // On Windows, check for drive letter or network path
            if (Application.platform == RuntimePlatform.WindowsEditor && Path.IsPathRooted(path))
            {
                return true;
            }

            // On Unix/Linux and macOS, check if starts with '/'
            if (Application.platform == RuntimePlatform.OSXEditor && path.StartsWith("/"))
            {
                return true;
            }

            return false; // Otherwise, it's a relative path
        }

        public static string GetAbsolutePath(string path)
        {
            if (IsAbsolutePath(path))
            {
                return path;
            }

            return Path.Combine(projectRootPath, path);
        }
    }
}
