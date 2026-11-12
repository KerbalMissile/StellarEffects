using System.Globalization;
using UnityEngine;

namespace StellarEffects.NeutronStars
{
    internal static class NeutronStarLimits
    {
        public const float CoreBrightnessMax = 1000f;
        public const float PolarCapMax = 100f;
        public const float GlareMax = 1000f;
        public const float GlareSizeMax = 50000f;
        public const float SpinRateMax = 1000f;
        public const float TiltMax = 90f;
        public const int StepsMax = 4000;
        public const float NormalMax = 1000f;
        public const float BeamBrightnessMax = 5000f;
        public const float BeamLengthMax = 1000000f;
        public const float BeamWidthMin = 0.05f;
        public const float BeamWidthMax = 10000f;
        public const float BeamSpreadMax = 10f;
        public const float BeamingMax = 100f;
        public const float FieldBrightnessMax = 1000f;
        public const float FieldExtentMin = 2f;
        public const float FieldExtentMax = 100000f;
        public const int FieldShellsMax = 64;
        public const float FieldWidthMin = 0.01f;
        public const float FieldWidthMax = 1f;
        public const int FieldAzimuthMax = 128;
    }

    internal class NeutronStarDefinition
    {
        public const string NodeName = "STELLAREFFECTS_NEUTRONSTAR";

        public string Body = "Sun";
        public float Radius = 0f;
        public float CoreBrightness = 6f;
        public Vector3 CoreColor = new Vector3(0.7f, 0.85f, 1f);
        public float PolarCap = 2f;
        public float Glare = 1.2f;
        public float GlareSize = 25f;
        public float SpinRate = 0.5f;
        public float Spin = 1f;
        public float Tilt = 40f;
        public Vector3 Normal = Vector3.up;
        public int Steps = 300;
        public bool HideBody = true;
        public float BeamBrightness = 3f;
        public float BeamLength = 120f;
        public float BeamWidth = 0.35f;
        public float BeamSpread = 0.04f;
        public Vector3 BeamColor = new Vector3(0.6f, 0.78f, 1f);
        public float Beaming = 3f;
        public float BeamTurbulence = 0.5f;
        public float FieldBrightness = 1f;
        public float FieldExtent = 12f;
        public int FieldShells = 6;
        public float FieldWidth = 0.12f;
        public int FieldAzimuth = 10;
        public Vector3 FieldColor = new Vector3(0.55f, 0.72f, 1f);

        public CelestialBody Target;
        public bool Missing;
        public bool Draw = true;
        public string SourcePath;

        private NeutronStarDefinition baseline;

        public static NeutronStarDefinition CreateNew(string body)
        {
            var definition = new NeutronStarDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static NeutronStarDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new NeutronStarDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("coreBrightness", ref definition.CoreBrightness);
            node.TryGetValue("coreColor", ref definition.CoreColor);
            node.TryGetValue("polarCap", ref definition.PolarCap);
            node.TryGetValue("glare", ref definition.Glare);
            node.TryGetValue("glareSize", ref definition.GlareSize);
            node.TryGetValue("spinRate", ref definition.SpinRate);
            node.TryGetValue("spin", ref definition.Spin);
            node.TryGetValue("tilt", ref definition.Tilt);
            node.TryGetValue("normal", ref definition.Normal);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("hideBody", ref definition.HideBody);
            node.TryGetValue("beamBrightness", ref definition.BeamBrightness);
            node.TryGetValue("beamLength", ref definition.BeamLength);
            node.TryGetValue("beamWidth", ref definition.BeamWidth);
            node.TryGetValue("beamSpread", ref definition.BeamSpread);
            node.TryGetValue("beamColor", ref definition.BeamColor);
            node.TryGetValue("beaming", ref definition.Beaming);
            node.TryGetValue("beamTurbulence", ref definition.BeamTurbulence);
            node.TryGetValue("fieldBrightness", ref definition.FieldBrightness);
            node.TryGetValue("fieldExtent", ref definition.FieldExtent);
            node.TryGetValue("fieldShells", ref definition.FieldShells);
            node.TryGetValue("fieldWidth", ref definition.FieldWidth);
            node.TryGetValue("fieldAzimuth", ref definition.FieldAzimuth);
            node.TryGetValue("fieldColor", ref definition.FieldColor);

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            CoreBrightness = Mathf.Clamp(CoreBrightness, 0f, NeutronStarLimits.CoreBrightnessMax);
            CoreColor = ClampColor(CoreColor);
            PolarCap = Mathf.Clamp(PolarCap, 0f, NeutronStarLimits.PolarCapMax);
            Glare = Mathf.Clamp(Glare, 0f, NeutronStarLimits.GlareMax);
            GlareSize = Mathf.Clamp(GlareSize, 2f, NeutronStarLimits.GlareSizeMax);
            SpinRate = Mathf.Clamp(SpinRate, 0f, NeutronStarLimits.SpinRateMax);
            Tilt = Mathf.Clamp(Tilt, 0f, NeutronStarLimits.TiltMax);
            Steps = Mathf.Clamp(Steps, 32, NeutronStarLimits.StepsMax);
            BeamBrightness = Mathf.Clamp(BeamBrightness, 0f, NeutronStarLimits.BeamBrightnessMax);
            BeamLength = Mathf.Clamp(BeamLength, 2f, NeutronStarLimits.BeamLengthMax);
            BeamWidth = Mathf.Clamp(BeamWidth, NeutronStarLimits.BeamWidthMin, NeutronStarLimits.BeamWidthMax);
            BeamSpread = Mathf.Clamp(BeamSpread, 0f, NeutronStarLimits.BeamSpreadMax);
            BeamColor = ClampColor(BeamColor);
            Beaming = Mathf.Clamp(Beaming, 0f, NeutronStarLimits.BeamingMax);
            BeamTurbulence = Mathf.Clamp01(BeamTurbulence);
            FieldBrightness = Mathf.Clamp(FieldBrightness, 0f, NeutronStarLimits.FieldBrightnessMax);
            FieldExtent = Mathf.Clamp(FieldExtent, NeutronStarLimits.FieldExtentMin, NeutronStarLimits.FieldExtentMax);
            FieldShells = Mathf.Clamp(FieldShells, 1, NeutronStarLimits.FieldShellsMax);
            FieldWidth = Mathf.Clamp(FieldWidth, NeutronStarLimits.FieldWidthMin, NeutronStarLimits.FieldWidthMax);
            FieldAzimuth = Mathf.Clamp(FieldAzimuth, 0, NeutronStarLimits.FieldAzimuthMax);
            FieldColor = ClampColor(FieldColor);

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        public void Commit()
        {
            baseline = new NeutronStarDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(NeutronStarDefinition other)
        {
            Body = other.Body;
            Radius = other.Radius;
            CoreBrightness = other.CoreBrightness;
            CoreColor = other.CoreColor;
            PolarCap = other.PolarCap;
            Glare = other.Glare;
            GlareSize = other.GlareSize;
            SpinRate = other.SpinRate;
            Spin = other.Spin;
            Tilt = other.Tilt;
            Normal = other.Normal;
            Steps = other.Steps;
            HideBody = other.HideBody;
            BeamBrightness = other.BeamBrightness;
            BeamLength = other.BeamLength;
            BeamWidth = other.BeamWidth;
            BeamSpread = other.BeamSpread;
            BeamColor = other.BeamColor;
            Beaming = other.Beaming;
            BeamTurbulence = other.BeamTurbulence;
            FieldBrightness = other.FieldBrightness;
            FieldExtent = other.FieldExtent;
            FieldShells = other.FieldShells;
            FieldWidth = other.FieldWidth;
            FieldAzimuth = other.FieldAzimuth;
            FieldColor = other.FieldColor;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("coreBrightness", Format(CoreBrightness), true);
            node.SetValue("coreColor", FormatColor(CoreColor), true);
            node.SetValue("polarCap", Format(PolarCap), true);
            node.SetValue("glare", Format(Glare), true);
            node.SetValue("glareSize", Format(GlareSize), true);
            node.SetValue("spinRate", Format(SpinRate), true);
            node.SetValue("spin", Format(Spin), true);
            node.SetValue("tilt", Format(Tilt), true);
            node.SetValue("normal", Format(Normal.x) + ", " + Format(Normal.y) + ", " + Format(Normal.z), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);
            node.SetValue("beamBrightness", Format(BeamBrightness), true);
            node.SetValue("beamLength", Format(BeamLength), true);
            node.SetValue("beamWidth", Format(BeamWidth), true);
            node.SetValue("beamSpread", Format(BeamSpread), true);
            node.SetValue("beamColor", FormatColor(BeamColor), true);
            node.SetValue("beaming", Format(Beaming), true);
            node.SetValue("beamTurbulence", Format(BeamTurbulence), true);
            node.SetValue("fieldBrightness", Format(FieldBrightness), true);
            node.SetValue("fieldExtent", Format(FieldExtent), true);
            node.SetValue("fieldShells", FieldShells.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("fieldWidth", Format(FieldWidth), true);
            node.SetValue("fieldAzimuth", FieldAzimuth.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("fieldColor", FormatColor(FieldColor), true);
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