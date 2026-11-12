using System.IO;
using UnityEditor;
using UnityEngine;

namespace StellarEffects.Editor
{
    public static class BuildBundles
    {
        private const string OutputFolder = "AssetBundles";

        [MenuItem("StellarEffects/Build Asset Bundles")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);

            var bundle = new AssetBundleBuild
            {
                assetBundleName = "stellareffects",
                assetNames = new[]
                {
                    "Assets/Shaders/BlackHole.shader",
                    "Assets/Shaders/Quasar.shader",
                    "Assets/Shaders/Nebula.shader",
                    "Assets/Shaders/Comet.shader",
                    "Assets/Shaders/Evaporation.shader"
                }
            };

            BuildTarget[] targets =
            {
                BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneLinux64,
                BuildTarget.StandaloneOSX
            };

            foreach (BuildTarget target in targets)
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                {
                    Debug.LogWarning("Skipping " + target + ", its build support isn't installed.");
                    continue;
                }

                string folder = Path.Combine(OutputFolder, target.ToString());
                Directory.CreateDirectory(folder);
                BuildPipeline.BuildAssetBundles(folder, new[] { bundle }, BuildAssetBundleOptions.ForceRebuildAssetBundle, target);
            }

            Debug.Log("StellarEffects bundles built into " + Path.GetFullPath(OutputFolder));
        }
    }
}