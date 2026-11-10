using System.Globalization;
using System.IO;
using UnityEngine;

namespace StellarEffects
{
    internal enum QualityPreset
    {
        High,
        Medium,
        Low
    }

    internal static class StellarQuality
    {
        private const string NodeName = "STELLAREFFECTS_SETTINGS";
        private const string ConfigPath = "GameData/StellarEffects/settings.cfg";
        private const int MinSteps = 35;

        private static QualityPreset current = Load();

        public static QualityPreset Current
        {
            get { return current; }
            set
            {
                if (current == value) return;
                current = value;
                Save();
            }
        }

        public static float NoiseDetail
        {
            get
            {
                switch (current)
                {
                    case QualityPreset.Medium: return 1f;
                    case QualityPreset.Low: return 0f;
                    default: return 2f;
                }
            }
        }

        public static float MarchScale
        {
            get
            {
                switch (current)
                {
                    case QualityPreset.Medium: return 1.35f;
                    case QualityPreset.Low: return 1.9f;
                    default: return 1f;
                }
            }
        }

        public static float Steps(int configured, float surfaceDistance, float effectBounds)
        {
            float floor = Mathf.Min(configured, MinSteps);
            float start = Mathf.Max(effectBounds * 100f, 2000f);

            if (surfaceDistance <= start) return configured;

            float steps = configured * Mathf.Sqrt(start / surfaceDistance);
            return Mathf.Clamp(Mathf.Round(steps), floor, configured);
        }

        private static QualityPreset Load()
        {
            try
            {
                string path = Path.Combine(KSPUtil.ApplicationRootPath, ConfigPath);
                if (!File.Exists(path)) return QualityPreset.High;

                ConfigNode root = ConfigNode.Load(path);
                ConfigNode node = root != null ? root.GetNode(NodeName) : null;
                string value = node != null ? node.GetValue("quality") : null;

                QualityPreset parsed;
                if (value != null && System.Enum.TryParse(value, true, out parsed)) return parsed;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StellarEffects] Could not read quality setting: " + e);
            }

            return QualityPreset.High;
        }

        private static void Save()
        {
            try
            {
                string path = Path.Combine(KSPUtil.ApplicationRootPath, ConfigPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var root = new ConfigNode();
                ConfigNode node = root.AddNode(NodeName);
                node.AddValue("quality", current.ToString());
                root.Save(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StellarEffects] Could not save quality setting: " + e);
            }
        }
    }
}

namespace StellarEffects.Blackholes
{
    internal class BlackHoleDefinition
    {
        public string Body = "Sun";
        public float HorizonRadius = 0f;
        public float Temperature = 6500f;
        public float Brightness = 3.5f;
        public float ColorSaturation = 3f;
        public float DiskInner = 3f;
        public float DiskOuter = 12f;
        public float DiskThickness = 0.03f;
        public float Glow = 1f;
        public float PhotonRing = 1f;
        public float RotationSpeed = 1f;
        public float Spin = 1f;
        public float Turbulence = 0.5f;
        public float Twirl = 1f;
        public int Steps = 200;
        public Vector3 Normal = Vector3.up;
        public bool HideBody = true;
        public bool GlowShift = false;
        public int Debug = 0;
        public bool Jets = true;
        public float JetBrightness = 1f;
        public float JetLength = 45f;
        public float JetWidth = 3f;
        public float JetSpread = 0.1f;
        public float JetTwist = 0.01f;
        public float JetTemperature = 15000f;
        public float JetSpeed = 0.9f;

        public CelestialBody Target;
        public bool Missing;
        public bool Draw = true;
        public string SourcePath;

        private BlackHoleDefinition baseline;

        public static BlackHoleDefinition CreateNew(string body)
        {
            var definition = new BlackHoleDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static BlackHoleDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new BlackHoleDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("horizonRadius", ref definition.HorizonRadius);
            node.TryGetValue("temperature", ref definition.Temperature);
            node.TryGetValue("brightness", ref definition.Brightness);
            node.TryGetValue("colorSaturation", ref definition.ColorSaturation);
            node.TryGetValue("diskInner", ref definition.DiskInner);
            node.TryGetValue("diskOuter", ref definition.DiskOuter);
            node.TryGetValue("diskThickness", ref definition.DiskThickness);
            node.TryGetValue("glow", ref definition.Glow);
            node.TryGetValue("photonRing", ref definition.PhotonRing);
            node.TryGetValue("rotationSpeed", ref definition.RotationSpeed);
            node.TryGetValue("spin", ref definition.Spin);
            node.TryGetValue("turbulence", ref definition.Turbulence);
            node.TryGetValue("twirl", ref definition.Twirl);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("normal", ref definition.Normal);
            node.TryGetValue("hideBody", ref definition.HideBody);
            node.TryGetValue("glowShift", ref definition.GlowShift);
            node.TryGetValue("debug", ref definition.Debug);
            node.TryGetValue("jets", ref definition.Jets);
            node.TryGetValue("jetBrightness", ref definition.JetBrightness);
            node.TryGetValue("jetLength", ref definition.JetLength);
            node.TryGetValue("jetWidth", ref definition.JetWidth);
            node.TryGetValue("jetSpread", ref definition.JetSpread);
            node.TryGetValue("jetTwist", ref definition.JetTwist);
            node.TryGetValue("jetTemperature", ref definition.JetTemperature);
            node.TryGetValue("jetSpeed", ref definition.JetSpeed);

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            HorizonRadius = Mathf.Max(HorizonRadius, 0f);
            Temperature = Mathf.Clamp(Temperature, 1000f, 40000f);
            DiskInner = Mathf.Max(DiskInner, 1.5f);
            DiskOuter = Mathf.Max(DiskOuter, DiskInner + 1f);
            DiskThickness = Mathf.Clamp(DiskThickness, 0.005f, 0.5f);
            Turbulence = Mathf.Clamp01(Turbulence);
            Twirl = Mathf.Clamp(Twirl, 0f, 10f);
            Steps = Mathf.Clamp(Steps, 32, 600);
            JetBrightness = Mathf.Clamp(JetBrightness, 0f, 50f);
            JetLength = Mathf.Clamp(JetLength, 5f, 5000f);
            JetWidth = Mathf.Clamp(JetWidth, 0.2f, 100f);
            JetSpread = Mathf.Clamp(JetSpread, 0f, 1.5f);
            JetTwist = Mathf.Clamp(JetTwist, -0.02f, 0.02f);
            JetTemperature = Mathf.Clamp(JetTemperature, 3000f, 40000f);
            JetSpeed = Mathf.Clamp(JetSpeed, 0f, 0.999f);

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        public void Commit()
        {
            baseline = new BlackHoleDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(BlackHoleDefinition other)
        {
            Body = other.Body;
            HorizonRadius = other.HorizonRadius;
            Temperature = other.Temperature;
            Brightness = other.Brightness;
            ColorSaturation = other.ColorSaturation;
            DiskInner = other.DiskInner;
            DiskOuter = other.DiskOuter;
            DiskThickness = other.DiskThickness;
            Glow = other.Glow;
            PhotonRing = other.PhotonRing;
            RotationSpeed = other.RotationSpeed;
            Spin = other.Spin;
            Turbulence = other.Turbulence;
            Twirl = other.Twirl;
            Steps = other.Steps;
            Normal = other.Normal;
            HideBody = other.HideBody;
            GlowShift = other.GlowShift;
            Debug = other.Debug;
            Jets = other.Jets;
            JetBrightness = other.JetBrightness;
            JetLength = other.JetLength;
            JetWidth = other.JetWidth;
            JetSpread = other.JetSpread;
            JetTwist = other.JetTwist;
            JetTemperature = other.JetTemperature;
            JetSpeed = other.JetSpeed;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("horizonRadius", Format(HorizonRadius), true);
            node.SetValue("temperature", Format(Temperature), true);
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("colorSaturation", Format(ColorSaturation), true);
            node.SetValue("diskInner", Format(DiskInner), true);
            node.SetValue("diskOuter", Format(DiskOuter), true);
            node.SetValue("diskThickness", Format(DiskThickness), true);
            node.SetValue("glow", Format(Glow), true);
            node.SetValue("photonRing", Format(PhotonRing), true);
            node.SetValue("rotationSpeed", Format(RotationSpeed), true);
            node.SetValue("spin", Format(Spin), true);
            node.SetValue("turbulence", Format(Turbulence), true);
            node.SetValue("twirl", Format(Twirl), true);
            node.SetValue("normal", Format(Normal.x) + ", " + Format(Normal.y) + ", " + Format(Normal.z), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);
            node.SetValue("glowShift", GlowShift ? "true" : "false", true);
            node.SetValue("debug", Debug.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("jets", Jets ? "true" : "false", true);
            node.SetValue("jetBrightness", Format(JetBrightness), true);
            node.SetValue("jetLength", Format(JetLength), true);
            node.SetValue("jetWidth", Format(JetWidth), true);
            node.SetValue("jetSpread", Format(JetSpread), true);
            node.SetValue("jetTwist", Format(JetTwist), true);
            node.SetValue("jetTemperature", Format(JetTemperature), true);
            node.SetValue("jetSpeed", Format(JetSpeed), true);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}