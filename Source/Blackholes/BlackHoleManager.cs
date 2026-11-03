using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Blackholes
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class BlackHoleManager : MonoBehaviour
    {
        private const string NodeName = "STELLAREFFECTS_BLACKHOLE";
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/BlackHole";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<BlackHoleDefinition> holes = new List<BlackHoleDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static BlackHoleManager Instance { get; private set; }

        public List<BlackHoleDefinition> Holes
        {
            get { return holes; }
        }

        public void Remove(BlackHoleDefinition hole)
        {
            holes.Remove(hole);
            if (scaledCamera == null) return;

            var blackHoleRenderer = scaledCamera.GetComponent<BlackHoleRenderer>();
            if (blackHoleRenderer != null) blackHoleRenderer.Release(hole);
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

            Debug.Log("[StellarEffects] Shader loaded, supported=" + shader.isSupported);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(NodeName))
                holes.Add(BlackHoleDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + holes.Count + " black hole definition(s).");
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
            foreach (BlackHoleDefinition hole in holes)
            {
                if (hole.Target != null || hole.Missing) continue;

                hole.Target = FlightGlobals.Bodies.Find(b => b.bodyName == hole.Body);
                if (hole.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + hole.Body + "', skipping.");
                    hole.Missing = true;
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
            var blackHoleRenderer = scaledCamera.GetComponent<BlackHoleRenderer>();
            if (blackHoleRenderer != null) return;

            blackHoleRenderer = scaledCamera.gameObject.AddComponent<BlackHoleRenderer>();
            blackHoleRenderer.Shader = shader;
            blackHoleRenderer.Holes = holes;
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
