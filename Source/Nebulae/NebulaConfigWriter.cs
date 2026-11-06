using System.IO;
using UnityEngine;

namespace StellarEffects.Nebulae
{
    internal static class NebulaConfigWriter
    {
        private const string ConfigFolder = "GameData/StellarEffects/Configs";

        public static string Save(NebulaDefinition nebula)
        {
            string path = nebula.SourcePath;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(nebula.Body));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, nebula.Body);
            if (node == null) node = root.AddNode(NebulaDefinition.NodeName);

            nebula.WriteTo(node);
            root.Save(path);

            nebula.SourcePath = path;
            nebula.Commit();

            return path;
        }

        public static void Delete(NebulaDefinition nebula)
        {
            string path = nebula.SourcePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            ConfigNode root = ConfigNode.Load(path);
            if (root == null) return;

            foreach (ConfigNode candidate in root.GetNodes(NebulaDefinition.NodeName))
            {
                if (candidate.GetValue("body") == nebula.Body)
                    root.RemoveNode(candidate);
            }

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);

            nebula.SourcePath = null;
        }

        private static ConfigNode FindNode(ConfigNode root, string body)
        {
            foreach (ConfigNode candidate in root.GetNodes(NebulaDefinition.NodeName))
            {
                if (candidate.GetValue("body") == body)
                    return candidate;
            }

            return null;
        }

        private static string FileName(string body)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                body = body.Replace(c, '_');

            return body + "_Nebula.cfg";
        }
    }
}