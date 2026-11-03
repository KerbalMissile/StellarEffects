using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Quasars
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class QuasarManager : MonoBehaviour
    {
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/Quasar";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<QuasarDefinition> quasars = new List<QuasarDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static QuasarManager Instance { get; private set; }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public List<QuasarDefinition> Quasars
        {
            get { return quasars; }
        }

        public QuasarDefinition Add(string body)
        {
            QuasarDefinition existing = quasars.Find(q => q.Body == body);
            if (existing != null) return existing;

            QuasarDefinition created = QuasarDefinition.CreateNew(body);
            quasars.Add(created);
            return created;
        }

        public void Remove(QuasarDefinition quasar)
        {
            quasars.Remove(quasar);
            if (scaledCamera == null) return;

            var quasarRenderer = scaledCamera.GetComponent<QuasarRenderer>();
            if (quasarRenderer != null) quasarRenderer.Release(quasar);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(QuasarDefinition.NodeName))
                quasars.Add(QuasarDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + quasars.Count + " quasar definition(s).");

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            Debug.Log("[StellarEffects] Quasar shader loaded, supported=" + shader.isSupported);
        }

        void Update()
        {
            if (FlightGlobals.fetch == null || FlightGlobals.Bodies == null || FlightGlobals.Bodies.Count == 0)
                return;

            ResolveTargets();

            if (scaledCamera == null)
                FindScaledSpaceCamera();

            if (scaledCamera == null) return;

            EnsureRenderer();
        }

        private void ResolveTargets()
        {
            foreach (QuasarDefinition quasar in quasars)
            {
                if (quasar.Target != null || quasar.Missing) continue;

                quasar.Target = FlightGlobals.Bodies.Find(b => b.bodyName == quasar.Body);
                if (quasar.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + quasar.Body + "', skipping quasar.");
                    quasar.Missing = true;
                }
            }
        }

        private void FindScaledSpaceCamera()
        {
            foreach (Camera candidate in Camera.allCameras)
            {
                if (candidate.name == CameraName)
                {
                    scaledCamera = candidate;
                    return;
                }
            }
        }

        private void EnsureRenderer()
        {
            var quasarRenderer = scaledCamera.GetComponent<QuasarRenderer>();
            if (quasarRenderer != null) return;

            quasarRenderer = scaledCamera.gameObject.AddComponent<QuasarRenderer>();
            quasarRenderer.Shader = shader;
            quasarRenderer.Quasars = quasars;
        }

        private static Shader LoadShader()
        {
            Shader found = Shader.Find(ShaderName);
            if (found != null) return found;

            string path = Path.Combine(KSPUtil.ApplicationRootPath, BundlePath);

            if (!File.Exists(path))
            {
                Debug.LogError("[StellarEffects] Shader bundle not found at " + path);
                return null;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                foreach (AssetBundle loadedBundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    foreach (Shader shaderAsset in loadedBundle.LoadAllAssets<Shader>())
                    {
                        if (shaderAsset.name == ShaderName)
                            return shaderAsset;
                    }
                }

                Debug.LogError("[StellarEffects] Could not load " + path);
                return null;
            }

            foreach (Shader shaderAsset in bundle.LoadAllAssets<Shader>())
            {
                if (shaderAsset.name == ShaderName)
                    found = shaderAsset;
            }

            bundle.Unload(false);

            if (found == null)
                Debug.LogError("[StellarEffects] " + ShaderName + " is missing from the bundle.");

            return found;
        }
    }
}
