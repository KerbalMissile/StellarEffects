using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.Comets
{
    internal static class CometConfigWriter
    {
        private const string ConfigFolder = "GameData/StellarEffects/Configs";

        public static string Save(CometDefinition comet)
        {
            string path = comet.SourcePath;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(comet.Body));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, comet.Body);
            if (node == null)
            {
                node = root.AddNode(CometDefinition.NodeName);
                comet.WriteTo(node);
            }
            else
            {
                var fresh = new ConfigNode();
                comet.WriteTo(fresh);
                Merge(node, fresh);
            }
            root.Save(path);

            comet.SourcePath = path;
            comet.Commit();

            return path;
        }

        public static void Delete(CometDefinition comet)
        {
            string path = comet.SourcePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            ConfigNode root = ConfigNode.Load(path);
            if (root == null) return;

            foreach (ConfigNode candidate in FindNodes(root, comet.Body))
                root.RemoveNode(candidate);

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);

            comet.SourcePath = null;
        }

        private static ConfigNode FindNode(ConfigNode root, string body)
        {
            List<ConfigNode> matches = FindNodes(root, body);
            return matches.Count > 0 ? matches[0] : null;
        }

        private static List<ConfigNode> FindNodes(ConfigNode root, string body)
        {
            var matches = new List<ConfigNode>();

            for (int i = 0; i < root.nodes.Count; i++)
            {
                ConfigNode candidate = root.nodes[i];
                if (BaseName(candidate.name) == CometDefinition.NodeName && MatchesBody(candidate, body))
                    matches.Add(candidate);
            }

            return matches;
        }

        private static string BaseName(string name)
        {
            int end = name.IndexOfAny(new[] { ':', '[', ',', ' ' });
            if (end >= 0) name = name.Substring(0, end);

            return name.Trim().TrimStart('@', '%');
        }

        private static bool MatchesBody(ConfigNode candidate, string body)
        {
            string value = candidate.GetValue("body") ?? candidate.GetValue("@body") ?? candidate.GetValue("%body");
            if (value != null) return value.Trim() == body;

            string name = candidate.name;
            int open = name.IndexOf('[');
            int tag = name.IndexOf(':');
            if (open < 0 || (tag >= 0 && open > tag)) return false;

            int close = name.IndexOf(']', open);
            return close > open && name.Substring(open + 1, close - open - 1).Trim() == body;
        }

        private static void Merge(ConfigNode target, ConfigNode source)
        {
            for (int i = 0; i < source.values.Count; i++)
            {
                string key = source.values[i].name;
                string value = source.values[i].value;
                bool found = false;

                for (int j = 0; j < target.values.Count; j++)
                {
                    ConfigNode.Value existing = target.values[j];
                    string existingName = existing.name.Trim();
                    if (existingName == key || existingName == "@" + key || existingName == "%" + key)
                    {
                        existing.value = value;
                        found = true;
                        break;
                    }
                }

                if (!found) target.AddValue(key, value);
            }

            for (int i = 0; i < source.nodes.Count; i++)
            {
                ConfigNode child = source.nodes[i];
                ConfigNode existing = FindChild(target, child.name);

                if (existing == null) target.AddNode(child.CreateCopy());
                else Merge(existing, child);
            }
        }

        private static ConfigNode FindChild(ConfigNode target, string name)
        {
            for (int i = 0; i < target.nodes.Count; i++)
            {
                if (BaseName(target.nodes[i].name) == name)
                    return target.nodes[i];
            }

            return null;
        }

        private static string FileName(string body)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                body = body.Replace(c, '_');

            return body + "_Comet.cfg";
        }
    }
}
