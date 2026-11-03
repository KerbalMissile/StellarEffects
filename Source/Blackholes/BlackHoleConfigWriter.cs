using System.IO;
using UnityEngine;

namespace StellarEffects.Blackholes
{
    internal static class BlackHoleConfigWriter
    {
        private const string NodeName = "STELLAREFFECTS_BLACKHOLE";
        private const string ConfigFolder = "GameData/StellarEffects/Configs";

        public static string Save(BlackHoleDefinition hole)
        {
            string path = hole.SourcePath;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(hole.Body));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, hole.Body);
            if (node == null) node = root.AddNode(NodeName);

            hole.WriteTo(node);
            root.Save(path);

            hole.SourcePath = path;
            hole.Commit();

            return path;
        }

        public static void Delete(BlackHoleDefinition hole)
        {
            string path = hole.SourcePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            ConfigNode root = ConfigNode.Load(path);
            if (root == null) return;

            foreach (ConfigNode candidate in root.GetNodes(NodeName))
            {
                if (candidate.GetValue("body") == hole.Body)
                    root.RemoveNode(candidate);
            }

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);
        }

        private static ConfigNode FindNode(ConfigNode root, string body)
        {
            foreach (ConfigNode candidate in root.GetNodes(NodeName))
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

            return body + ".cfg";
        }
    }
}
