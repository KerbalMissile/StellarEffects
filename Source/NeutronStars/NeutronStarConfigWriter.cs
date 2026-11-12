using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StellarEffects.NeutronStars
{
    internal static class NeutronStarConfigWriter
    {
        private const string ConfigFolder = "GameData/StellarEffects/Configs";

        public static string Save(NeutronStarDefinition star)
        {
            string path = star.SourcePath;
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(star.Body));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, star.Body);
            if (node == null)
            {
                node = root.AddNode(NeutronStarDefinition.NodeName);
                star.WriteTo(node);
            }
            else
            {
                var fresh = new ConfigNode();
                star.WriteTo(fresh);
                Merge(node, fresh);
            }
            root.Save(path);

            star.SourcePath = path;
            star.Commit();

            return path;
        }

        public static void Delete(NeutronStarDefinition star)
        {
            string path = star.SourcePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            ConfigNode root = ConfigNode.Load(path);
            if (root == null) return;

            foreach (ConfigNode candidate in FindNodes(root, star.Body))
                root.RemoveNode(candidate);

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);

            star.SourcePath = null;
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
                if (BaseName(candidate.name) == NeutronStarDefinition.NodeName && MatchesBody(candidate, body))
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
        }

        private static string FileName(string body)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                body = body.Replace(c, '_');

            return body + "_NeutronStar.cfg";
        }
    }
}