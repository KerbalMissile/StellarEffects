using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Auroras
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class AuroraManager : MonoBehaviour
    {
        private const string NodeName = "STELLAREFFECTS_AURORA";
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/Aurora";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<AuroraDefinition> auroras = new List<AuroraDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static AuroraManager Instance { get; private set; }

        public List<AuroraDefinition> Auroras
        {
            get { return auroras; }
        }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public void Add(string body)
        {
            auroras.Add(AuroraDefinition.CreateNew(body));
        }

        public void Remove(AuroraDefinition aurora)
        {
            auroras.Remove(aurora);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(NodeName))
                auroras.Add(AuroraDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + auroras.Count + " aurora definition(s).");
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
            foreach (AuroraDefinition aurora in auroras)
            {
                if (aurora.Target != null || aurora.Missing) continue;

                aurora.Target = FlightGlobals.Bodies.Find(b => b.bodyName == aurora.Body);
                if (aurora.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + aurora.Body + "' for aurora, skipping.");
                    aurora.Missing = true;
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
            var auroraRenderer = scaledCamera.GetComponent<AuroraRenderer>();
            if (auroraRenderer != null) return;

            auroraRenderer = scaledCamera.gameObject.AddComponent<AuroraRenderer>();
            auroraRenderer.Shader = shader;
            auroraRenderer.Auroras = auroras;
        }

        private static Shader LoadShader()
        {
            string path = Path.Combine(KSPUtil.ApplicationRootPath, BundlePath);

            if (!File.Exists(path))
            {
                Debug.LogError("[StellarEffects] Shader bundle not found at " + path);
                return null;
            }

            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogError("[StellarEffects] Could not load " + path);
                return null;
            }

            Shader found = null;
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