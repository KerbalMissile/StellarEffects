using System.Collections.Generic;
using System.Globalization;
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
                path = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, ConfigFolder), FileName(nebula.Body, nebula.Index));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ConfigNode root = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (root == null) root = new ConfigNode();

            ConfigNode node = FindNode(root, nebula);
            if (node == null)
            {
                node = root.AddNode(NebulaDefinition.NodeName);
                nebula.WriteTo(node);
            }
            else
            {
                var fresh = new ConfigNode();
                nebula.WriteTo(fresh);
                Merge(node, fresh);
            }
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

            ConfigNode target = FindNode(root, nebula);
            if (target != null)
            {
                root.RemoveNode(target);
                ShiftRanks(nebula, path);
            }

            if (root.CountNodes == 0 && root.CountValues == 0)
                File.Delete(path);
            else
                root.Save(path);

            nebula.SourcePath = null;
        }

        private static ConfigNode FindNode(ConfigNode root, NebulaDefinition nebula)
        {
            List<ConfigNode> matches = FindNodes(root, nebula.Body, nebula.Index);
            if (matches.Count == 0) return null;

            return matches[Mathf.Clamp(nebula.SourceRank, 0, matches.Count - 1)];
        }

        private static void ShiftRanks(NebulaDefinition removed, string path)
        {
            NebulaManager manager = NebulaManager.Instance;
            if (manager == null) return;

            foreach (NebulaDefinition other in manager.Definitions)
            {
                if (other != removed && other.SourcePath == path && other.Body == removed.Body && other.Index == removed.Index && other.SourceRank > removed.SourceRank)
                    other.SourceRank--;
            }
        }

        private static List<ConfigNode> FindNodes(ConfigNode root, string body, int index)
        {
            var matches = new List<ConfigNode>();

            for (int i = 0; i < root.nodes.Count; i++)
            {
                ConfigNode candidate = root.nodes[i];
                if (BaseName(candidate.name) == NebulaDefinition.NodeName && MatchesBody(candidate, body, index))
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

        private static bool MatchesBody(ConfigNode candidate, string body, int index)
        {
            if (NodeIndex(candidate) != index) return false;

            string value = candidate.GetValue("body") ?? candidate.GetValue("@body") ?? candidate.GetValue("%body");
            if (value != null) return value.Trim() == body;

            string name = candidate.name;
            int open = name.IndexOf('[');
            int tag = name.IndexOf(':');
            if (open < 0 || (tag >= 0 && open > tag)) return false;

            int close = name.IndexOf(']', open);
            return close > open && name.Substring(open + 1, close - open - 1).Trim() == body;
        }

        private static int NodeIndex(ConfigNode candidate)
        {
            string value = candidate.GetValue("index") ?? candidate.GetValue("@index") ?? candidate.GetValue("%index");

            int parsed;
            if (value != null && int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;

            return 0;
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

            for (int i = target.nodes.Count - 1; i >= 0; i--)
            {
                string childName = BaseName(target.nodes[i].name);
                if (childName.StartsWith("gas_") && !source.HasNode(childName))
                    target.RemoveNode(target.nodes[i]);
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

        private static string FileName(string body, int index)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                body = body.Replace(c, '_');

            return index == 0 ? body + "_Nebula.cfg" : body + "_Nebula_" + (index + 1) + ".cfg";
        }
    }
}