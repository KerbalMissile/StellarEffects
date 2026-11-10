using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Comets
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class CometManager : MonoBehaviour
    {
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/Comet";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<CometDefinition> comets = new List<CometDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static CometManager Instance { get; private set; }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public List<CometDefinition> Comets
        {
            get { return comets; }
        }

        public CometDefinition Add(string body)
        {
            CometDefinition existing = comets.Find(c => c.Body == body);
            if (existing != null) return existing;

            CometDefinition created = CometDefinition.CreateNew(body);
            comets.Add(created);
            return created;
        }

        public void Remove(CometDefinition comet)
        {
            comets.Remove(comet);
            if (scaledCamera == null) return;

            var cometRenderer = scaledCamera.GetComponent<CometRenderer>();
            if (cometRenderer != null) cometRenderer.Release(comet);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(CometDefinition.NodeName))
                comets.Add(CometDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + comets.Count + " comet definition(s).");

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            Debug.Log("[StellarEffects] Comet shader loaded, supported=" + shader.isSupported);
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
            foreach (CometDefinition comet in comets)
            {
                if (comet.Target != null || comet.Missing) continue;

                comet.Target = FlightGlobals.Bodies.Find(b => b.bodyName == comet.Body);
                if (comet.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + comet.Body + "', skipping comet.");
                    comet.Missing = true;
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
            var cometRenderer = scaledCamera.GetComponent<CometRenderer>();
            if (cometRenderer != null) return;

            cometRenderer = scaledCamera.gameObject.AddComponent<CometRenderer>();
            cometRenderer.Shader = shader;
            cometRenderer.Comets = comets;
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
