using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.NeutronStars
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class NeutronStarManager : MonoBehaviour
    {
        private const string BundlePath = "GameData/StellarEffects/Shaders/stellareffects.bundle";
        private const string ShaderName = "Hidden/StellarEffects/NeutronStar";
        private const string CameraName = "Camera ScaledSpace";

        private readonly List<NeutronStarDefinition> stars = new List<NeutronStarDefinition>();
        private Shader shader;
        private Camera scaledCamera;

        public static NeutronStarManager Instance { get; private set; }

        public bool ShaderReady
        {
            get { return shader != null; }
        }

        public List<NeutronStarDefinition> Stars
        {
            get { return stars; }
        }

        public NeutronStarDefinition Add(string body)
        {
            NeutronStarDefinition existing = stars.Find(p => p.Body == body);
            if (existing != null) return existing;

            NeutronStarDefinition created = NeutronStarDefinition.CreateNew(body);
            stars.Add(created);
            return created;
        }

        public void Remove(NeutronStarDefinition star)
        {
            stars.Remove(star);
            if (scaledCamera == null) return;

            var starRenderer = scaledCamera.GetComponent<NeutronStarRenderer>();
            if (starRenderer != null) starRenderer.Release(star);
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            DontDestroyOnLoad(gameObject);

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs(NeutronStarDefinition.NodeName))
                stars.Add(NeutronStarDefinition.FromNode(url.config, url.parent != null ? url.parent.fullPath : null));

            Debug.Log("[StellarEffects] Loaded " + stars.Count + " neutron star definition(s).");

            shader = LoadShader();
            if (shader == null)
            {
                enabled = false;
                return;
            }

            Debug.Log("[StellarEffects] Neutron star shader loaded, supported=" + shader.isSupported);
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
            foreach (NeutronStarDefinition star in stars)
            {
                if (star.Target != null || star.Missing) continue;

                star.Target = FlightGlobals.Bodies.Find(b => b.bodyName == star.Body);
                if (star.Target == null)
                {
                    Debug.LogWarning("[StellarEffects] No body named '" + star.Body + "', skipping neutron star.");
                    star.Missing = true;
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
            var starRenderer = scaledCamera.GetComponent<NeutronStarRenderer>();
            if (starRenderer != null) return;

            starRenderer = scaledCamera.gameObject.AddComponent<NeutronStarRenderer>();
            starRenderer.Shader = shader;
            starRenderer.Stars = stars;
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