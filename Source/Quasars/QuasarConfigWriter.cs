using System.IO;
using UnityEngine;

namespace StellarEffects.Quasars
{
    internal static class QuasarConfigWriter
    {
        private const string ConfigFolder = "GameData/StellarEffects/Configs";

        public static string Save(QuasarDefinition quasar)
        {
            string path = quasar.SourcePath;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(quasar.Body));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, quasar.Body);
            if (node == null) node = root.AddNode(QuasarDefinition.NodeName);

            quasar.WriteTo(node);
            root.Save(path);

            quasar.SourcePath = path;
            quasar.Commit();

            return path;
        }

        public static void Delete(QuasarDefinition quasar)
        {
            string path = quasar.SourcePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            ConfigNode root = ConfigNode.Load(path);
            if (root == null) return;

            foreach (ConfigNode candidate in root.GetNodes(QuasarDefinition.NodeName))
            {
                if (candidate.GetValue("body") == quasar.Body)
                    root.RemoveNode(candidate);
            }

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);

            quasar.SourcePath = null;
        }

        private static ConfigNode FindNode(ConfigNode root, string body)
        {
            foreach (ConfigNode candidate in root.GetNodes(QuasarDefinition.NodeName))
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

            return body + "_Quasar.cfg";
        }
    }
}
