using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Evaporation
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class EvaporationManager : MonoBehaviour
    {
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/Evaporation";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<EvaporationDefinition> planets = new List<EvaporationDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static EvaporationManager Instance { get; private set; }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public List<EvaporationDefinition> Planets
        {
            get { return planets; }
        }

        public EvaporationDefinition Add(string body)
        {
            EvaporationDefinition existing = planets.Find(p => p.Body == body);
            if (existing != null) return existing;

            EvaporationDefinition created = EvaporationDefinition.CreateNew(body);
            planets.Add(created);
            return created;
        }

        public void Remove(EvaporationDefinition planet)
        {
            planets.Remove(planet);
            if (scaledCamera == null) return;

            var planetRenderer = scaledCamera.GetComponent<EvaporationRenderer>();
            if (planetRenderer != null) planetRenderer.Release(planet);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(EvaporationDefinition.NodeName))
                planets.Add(EvaporationDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + planets.Count + " evaporating planet definition(s).");

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            Debug.Log("[StellarEffects] Evaporation shader loaded, supported=" + shader.isSupported);
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
            foreach (EvaporationDefinition planet in planets)
            {
                if (planet.Target != null || planet.Missing) continue;

                planet.Target = FlightGlobals.Bodies.Find(b => b.bodyName == planet.Body);
                if (planet.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + planet.Body + "', skipping evaporating planet.");
                    planet.Missing = true;
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
            var planetRenderer = scaledCamera.GetComponent<EvaporationRenderer>();
            if (planetRenderer != null) return;

            planetRenderer = scaledCamera.gameObject.AddComponent<EvaporationRenderer>();
            planetRenderer.Shader = shader;
            planetRenderer.Planets = planets;
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