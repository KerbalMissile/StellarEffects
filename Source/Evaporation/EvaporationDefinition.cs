using System.Globalization;
using UnityEngine;

namespace StellarEffects.Evaporation
{
    internal static class EvaporationLimits
    {
        public const float BrightnessMax = 100000f;
        public const float ComaSizeMin = 0.05f;
        public const float ComaSizeMax = 1000000f;
        public const float GlowMax = 100000f;
        public const float FlowSpeedMax = 100000f;
        public const int StepsMax = 8192;
        public const float TailBrightnessMax = 500000f;
        public const float TailLengthMin = 1f;
        public const float TailLengthMax = 100000000f;
        public const float TailWidthMin = 0.01f;
        public const float TailWidthMax = 1000000f;
        public const float TailSpreadMax = 1000f;
        public const float TailCurveMax = 1000f;
        public const float TailDensityMax = 10000f;
        public const float TailStriationMax = 500f;
        public const float TailTurbulenceMax = 5000f;
        public const float TailDecayMin = 0.001f;
        public const float TailDecayMax = 1000f;
        public const float TailSeedMax = 100000f;
        public const float TailAngleMax = 180f;
    }

    internal class EvaporationDefinition
    {
        public const string NodeName = "STELLAREFFECTS_EVAPORATION";

        public string Body = "Sun";
        public float Radius = 0f;
        public float Brightness = 1.2f;
        public float ComaSize = 1.6f;
        public Vector3 ComaColor = new Vector3(1f, 0.85f, 0.6f);
        public float Glow = 1.5f;
        public float FlowSpeed = 35f;
        public int Steps = 300;
        public bool HideBody = false;
        public float TailBrightness = 2.5f;
        public float TailLength = 250f;
        public float TailWidth = 1.2f;
        public float TailSpread = 0.25f;
        public float TailCurve = 1f;
        public Vector3 TailColor = new Vector3(1f, 0.72f, 0.45f);
        public float TailDensity = 1.4f;
        public float TailStriation = 0.6f;
        public float TailTurbulence = 0.7f;
        public float TailDecay = 0.4f;
        public float TailSeed = 17.3f;
        public float TailYaw = 0f;
        public float TailPitch = 0f;

        public CelestialBody Target;
        public bool Missing;
        public bool Draw = true;
        public string SourcePath;

        private EvaporationDefinition baseline;

        public static EvaporationDefinition CreateNew(string body)
        {
            var definition = new EvaporationDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static EvaporationDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new EvaporationDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("brightness", ref definition.Brightness);
            node.TryGetValue("comaSize", ref definition.ComaSize);
            node.TryGetValue("comaColor", ref definition.ComaColor);
            node.TryGetValue("glow", ref definition.Glow);
            node.TryGetValue("flowSpeed", ref definition.FlowSpeed);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("hideBody", ref definition.HideBody);
            node.TryGetValue("tailBrightness", ref definition.TailBrightness);
            node.TryGetValue("tailLength", ref definition.TailLength);
            node.TryGetValue("tailWidth", ref definition.TailWidth);
            node.TryGetValue("tailSpread", ref definition.TailSpread);
            node.TryGetValue("tailCurve", ref definition.TailCurve);
            node.TryGetValue("tailColor", ref definition.TailColor);
            node.TryGetValue("tailDensity", ref definition.TailDensity);
            node.TryGetValue("tailStriation", ref definition.TailStriation);
            node.TryGetValue("tailTurbulence", ref definition.TailTurbulence);
            node.TryGetValue("tailDecay", ref definition.TailDecay);
            node.TryGetValue("tailSeed", ref definition.TailSeed);
            node.TryGetValue("tailYaw", ref definition.TailYaw);
            node.TryGetValue("tailPitch", ref definition.TailPitch);

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            Brightness = Mathf.Clamp(Brightness, 0f, EvaporationLimits.BrightnessMax);
            ComaSize = Mathf.Clamp(ComaSize, EvaporationLimits.ComaSizeMin, EvaporationLimits.ComaSizeMax);
            ComaColor = ClampColor(ComaColor);
            Glow = Mathf.Clamp(Glow, 0f, EvaporationLimits.GlowMax);
            FlowSpeed = Mathf.Clamp(FlowSpeed, 0f, EvaporationLimits.FlowSpeedMax);
            Steps = Mathf.Clamp(Steps, 32, EvaporationLimits.StepsMax);
            TailBrightness = Mathf.Clamp(TailBrightness, 0f, EvaporationLimits.TailBrightnessMax);
            TailLength = Mathf.Clamp(TailLength, EvaporationLimits.TailLengthMin, EvaporationLimits.TailLengthMax);
            TailWidth = Mathf.Clamp(TailWidth, EvaporationLimits.TailWidthMin, EvaporationLimits.TailWidthMax);
            TailSpread = Mathf.Clamp(TailSpread, 0f, EvaporationLimits.TailSpreadMax);
            TailCurve = Mathf.Clamp(TailCurve, 0f, EvaporationLimits.TailCurveMax);
            TailColor = ClampColor(TailColor);
            TailDensity = Mathf.Clamp(TailDensity, 0f, EvaporationLimits.TailDensityMax);
            TailStriation = Mathf.Clamp(TailStriation, 0f, EvaporationLimits.TailStriationMax);
            TailTurbulence = Mathf.Clamp(TailTurbulence, 0f, EvaporationLimits.TailTurbulenceMax);
            TailDecay = Mathf.Clamp(TailDecay, EvaporationLimits.TailDecayMin, EvaporationLimits.TailDecayMax);
            TailSeed = Mathf.Clamp(TailSeed, -EvaporationLimits.TailSeedMax, EvaporationLimits.TailSeedMax);
            TailYaw = Mathf.Clamp(TailYaw, -EvaporationLimits.TailAngleMax, EvaporationLimits.TailAngleMax);
            TailPitch = Mathf.Clamp(TailPitch, -EvaporationLimits.TailAngleMax, EvaporationLimits.TailAngleMax);
        }

        public void Commit()
        {
            baseline = new EvaporationDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(EvaporationDefinition other)
        {
            Body = other.Body;
            Radius = other.Radius;
            Brightness = other.Brightness;
            ComaSize = other.ComaSize;
            ComaColor = other.ComaColor;
            Glow = other.Glow;
            FlowSpeed = other.FlowSpeed;
            Steps = other.Steps;
            HideBody = other.HideBody;
            TailBrightness = other.TailBrightness;
            TailLength = other.TailLength;
            TailWidth = other.TailWidth;
            TailSpread = other.TailSpread;
            TailCurve = other.TailCurve;
            TailColor = other.TailColor;
            TailDensity = other.TailDensity;
            TailStriation = other.TailStriation;
            TailTurbulence = other.TailTurbulence;
            TailDecay = other.TailDecay;
            TailSeed = other.TailSeed;
            TailYaw = other.TailYaw;
            TailPitch = other.TailPitch;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("comaSize", Format(ComaSize), true);
            node.SetValue("comaColor", FormatColor(ComaColor), true);
            node.SetValue("glow", Format(Glow), true);
            node.SetValue("flowSpeed", Format(FlowSpeed), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);
            node.SetValue("tailBrightness", Format(TailBrightness), true);
            node.SetValue("tailLength", Format(TailLength), true);
            node.SetValue("tailWidth", Format(TailWidth), true);
            node.SetValue("tailSpread", Format(TailSpread), true);
            node.SetValue("tailCurve", Format(TailCurve), true);
            node.SetValue("tailColor", FormatColor(TailColor), true);
            node.SetValue("tailDensity", Format(TailDensity), true);
            node.SetValue("tailStriation", Format(TailStriation), true);
            node.SetValue("tailTurbulence", Format(TailTurbulence), true);
            node.SetValue("tailDecay", Format(TailDecay), true);
            node.SetValue("tailSeed", Format(TailSeed), true);
            node.SetValue("tailYaw", Format(TailYaw), true);
            node.SetValue("tailPitch", Format(TailPitch), true);
        }

        private static Vector3 ClampColor(Vector3 color)
        {
            return new Vector3(Mathf.Clamp01(color.x), Mathf.Clamp01(color.y), Mathf.Clamp01(color.z));
        }

        private static string FormatColor(Vector3 color)
        {
            return Format(color.x) + ", " + Format(color.y) + ", " + Format(color.z);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}