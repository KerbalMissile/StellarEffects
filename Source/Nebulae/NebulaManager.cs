using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Nebulae
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class NebulaManager : MonoBehaviour
    {
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/Nebula";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<NebulaDefinition> definitions = new List<NebulaDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static NebulaManager Instance { get; private set; }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public List<NebulaDefinition> Definitions
        {
            get { return definitions; }
        }

        public NebulaDefinition Add(string body)
        {
            NebulaDefinition existing = definitions.Find(n => n.Body == body);
            if (existing != null) return existing;

            NebulaDefinition created = NebulaDefinition.CreateNew(body);
            definitions.Add(created);
            return created;
        }

        public void Remove(NebulaDefinition nebula)
        {
            definitions.Remove(nebula);
            if (scaledCamera == null) return;

            var nebulaRenderer = scaledCamera.GetComponent<NebulaRenderer>();
            if (nebulaRenderer != null) nebulaRenderer.Release(nebula);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(NebulaDefinition.NodeName))
                definitions.Add(NebulaDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + definitions.Count + " nebula definition(s).");

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            Debug.Log("[StellarEffects] Nebula shader loaded, supported=" + shader.isSupported);
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
            foreach (NebulaDefinition nebula in definitions)
            {
                if (nebula.Target != null || nebula.Missing) continue;

                nebula.Target = FlightGlobals.Bodies.Find(b => b.bodyName == nebula.Body);
                if (nebula.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + nebula.Body + "', skipping nebula.");
                    nebula.Missing = true;
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
            var nebulaRenderer = scaledCamera.GetComponent<NebulaRenderer>();
            if (nebulaRenderer != null) return;

            nebulaRenderer = scaledCamera.gameObject.AddComponent<NebulaRenderer>();
            nebulaRenderer.Shader = shader;
            nebulaRenderer.Definitions = definitions;
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