using System.Globalization;
using UnityEngine;

namespace StellarEffects.Quasars
{
    internal static class QuasarLimits
    {
        public const float CoreBrightnessMax = 1000f;
        public const float CoreSizeMin = 0.1f;
        public const float CoreSizeMax = 10000f;
        public const float CoreTemperatureMax = 100000f;
        public const float ColorSaturationMax = 100f;
        public const float GlareMax = 1000f;
        public const float GlareSizeMax = 50000f;
        public const float TorusInnerMax = 100000f;
        public const float TorusOuterMax = 200000f;
        public const float TorusThicknessMin = 0.005f;
        public const float TorusThicknessMax = 5f;
        public const float TorusDensityMax = 100f;
        public const float TorusTemperatureMax = 50000f;
        public const float TorusGlowMax = 1000f;
        public const float TorusTurbulenceMax = 50f;
        public const float RotationSpeedMax = 1000f;
        public const int StepsMax = 2000;
        public const float JetBrightnessMax = 5000f;
        public const float JetLengthMax = 1000000f;
        public const float JetWidthMax = 10000f;
        public const float JetSpreadMax = 10f;
        public const float JetTwistMax = 1f;
        public const float JetTemperatureMax = 100000f;
        public const float JetSpeedMax = 0.9999f;
        public const float NormalMax = 1000f;
        public const float JetPlumeStartMin = 0.05f;
        public const float JetPlumeStartMax = 5f;
        public const float JetPlumeWidthMax = 2f;
        public const float JetPlumeBoostMax = 100f;
    }

    internal class QuasarDefinition
    {
        public const string NodeName = "STELLAREFFECTS_QUASAR";

        public string Body = "Sun";
        public float Radius = 0f;
        public float CoreBrightness = 4f;
        public float CoreSize = 4f;
        public float CoreTemperature = 14000f;
        public float ColorSaturation = 1.5f;
        public float Glare = 1f;
        public float GlareSize = 30f;
        public float TorusInner = 12f;
        public float TorusOuter = 48f;
        public float TorusThickness = 0.4f;
        public float TorusDensity = 1f;
        public float TorusTemperature = 3000f;
        public float TorusGlow = 1.5f;
        public float TorusClumpiness = 1f;
        public float TorusTurbulence = 1f;
        public float RotationSpeed = 1f;
        public float Spin = 1f;
        public int Steps = 300;
        public Vector3 Normal = Vector3.up;
        public bool HideBody = true;
        public bool Jets = true;
        public float JetBrightness = 3f;
        public float JetLength = 200f;
        public float JetWidth = 1.2f;
        public float JetSpread = 0.02f;
        public float JetTwist = 0.004f;
        public float JetTemperature = 20000f;
        public float JetSpeed = 0.9f;
        public float JetPlumeStart = 0.625f;
        public float JetPlumeWidth = 0.045f;
        public float JetPlumeBoost = 3f;

        public CelestialBody Target;
        public bool Missing;
        public string SourcePath;

        private QuasarDefinition baseline;

        public static QuasarDefinition CreateNew(string body)
        {
            var definition = new QuasarDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static QuasarDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new QuasarDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("coreBrightness", ref definition.CoreBrightness);
            node.TryGetValue("coreSize", ref definition.CoreSize);
            node.TryGetValue("coreTemperature", ref definition.CoreTemperature);
            node.TryGetValue("colorSaturation", ref definition.ColorSaturation);
            node.TryGetValue("glare", ref definition.Glare);
            node.TryGetValue("glareSize", ref definition.GlareSize);
            node.TryGetValue("torusInner", ref definition.TorusInner);
            node.TryGetValue("torusOuter", ref definition.TorusOuter);
            node.TryGetValue("torusThickness", ref definition.TorusThickness);
            node.TryGetValue("torusDensity", ref definition.TorusDensity);
            node.TryGetValue("torusTemperature", ref definition.TorusTemperature);
            node.TryGetValue("torusGlow", ref definition.TorusGlow);
            node.TryGetValue("torusClumpiness", ref definition.TorusClumpiness);
            node.TryGetValue("torusTurbulence", ref definition.TorusTurbulence);
            node.TryGetValue("rotationSpeed", ref definition.RotationSpeed);
            node.TryGetValue("spin", ref definition.Spin);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("normal", ref definition.Normal);
            node.TryGetValue("hideBody", ref definition.HideBody);
            node.TryGetValue("jets", ref definition.Jets);
            node.TryGetValue("jetBrightness", ref definition.JetBrightness);
            node.TryGetValue("jetLength", ref definition.JetLength);
            node.TryGetValue("jetWidth", ref definition.JetWidth);
            node.TryGetValue("jetSpread", ref definition.JetSpread);
            node.TryGetValue("jetTwist", ref definition.JetTwist);
            node.TryGetValue("jetTemperature", ref definition.JetTemperature);
            node.TryGetValue("jetSpeed", ref definition.JetSpeed);
            node.TryGetValue("jetPlumeStart", ref definition.JetPlumeStart);
            node.TryGetValue("jetPlumeWidth", ref definition.JetPlumeWidth);
            node.TryGetValue("jetPlumeBoost", ref definition.JetPlumeBoost);

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            CoreBrightness = Mathf.Clamp(CoreBrightness, 0f, QuasarLimits.CoreBrightnessMax);
            CoreSize = Mathf.Clamp(CoreSize, QuasarLimits.CoreSizeMin, QuasarLimits.CoreSizeMax);
            CoreTemperature = Mathf.Clamp(CoreTemperature, 1000f, QuasarLimits.CoreTemperatureMax);
            ColorSaturation = Mathf.Clamp(ColorSaturation, 0f, QuasarLimits.ColorSaturationMax);
            Glare = Mathf.Clamp(Glare, 0f, QuasarLimits.GlareMax);
            GlareSize = Mathf.Clamp(GlareSize, 2f, QuasarLimits.GlareSizeMax);
            TorusInner = Mathf.Clamp(TorusInner, 2f, QuasarLimits.TorusInnerMax);
            TorusOuter = Mathf.Clamp(TorusOuter, TorusInner + 2f, QuasarLimits.TorusOuterMax);
            TorusThickness = Mathf.Clamp(TorusThickness, QuasarLimits.TorusThicknessMin, QuasarLimits.TorusThicknessMax);
            TorusDensity = Mathf.Clamp(TorusDensity, 0f, QuasarLimits.TorusDensityMax);
            TorusTemperature = Mathf.Clamp(TorusTemperature, 800f, QuasarLimits.TorusTemperatureMax);
            TorusGlow = Mathf.Clamp(TorusGlow, 0f, QuasarLimits.TorusGlowMax);
            TorusClumpiness = Mathf.Clamp01(TorusClumpiness);
            TorusTurbulence = Mathf.Clamp(TorusTurbulence, 0f, QuasarLimits.TorusTurbulenceMax);
            RotationSpeed = Mathf.Clamp(RotationSpeed, -QuasarLimits.RotationSpeedMax, QuasarLimits.RotationSpeedMax);
            Steps = Mathf.Clamp(Steps, 32, QuasarLimits.StepsMax);
            JetBrightness = Mathf.Clamp(JetBrightness, 0f, QuasarLimits.JetBrightnessMax);
            JetLength = Mathf.Clamp(JetLength, 5f, QuasarLimits.JetLengthMax);
            JetWidth = Mathf.Clamp(JetWidth, 0.2f, QuasarLimits.JetWidthMax);
            JetSpread = Mathf.Clamp(JetSpread, 0f, QuasarLimits.JetSpreadMax);
            JetTwist = Mathf.Clamp(JetTwist, -QuasarLimits.JetTwistMax, QuasarLimits.JetTwistMax);
            JetTemperature = Mathf.Clamp(JetTemperature, 3000f, QuasarLimits.JetTemperatureMax);
            JetSpeed = Mathf.Clamp(JetSpeed, 0f, QuasarLimits.JetSpeedMax);
            JetPlumeStart = Mathf.Clamp(JetPlumeStart, QuasarLimits.JetPlumeStartMin, QuasarLimits.JetPlumeStartMax);
            JetPlumeWidth = Mathf.Clamp(JetPlumeWidth, 0f, QuasarLimits.JetPlumeWidthMax);
            JetPlumeBoost = Mathf.Clamp(JetPlumeBoost, 0f, QuasarLimits.JetPlumeBoostMax);

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        public void Commit()
        {
            baseline = new QuasarDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(QuasarDefinition other)
        {
            Body = other.Body;
            Radius = other.Radius;
            CoreBrightness = other.CoreBrightness;
            CoreSize = other.CoreSize;
            CoreTemperature = other.CoreTemperature;
            ColorSaturation = other.ColorSaturation;
            Glare = other.Glare;
            GlareSize = other.GlareSize;
            TorusInner = other.TorusInner;
            TorusOuter = other.TorusOuter;
            TorusThickness = other.TorusThickness;
            TorusDensity = other.TorusDensity;
            TorusTemperature = other.TorusTemperature;
            TorusGlow = other.TorusGlow;
            TorusClumpiness = other.TorusClumpiness;
            TorusTurbulence = other.TorusTurbulence;
            RotationSpeed = other.RotationSpeed;
            Spin = other.Spin;
            Steps = other.Steps;
            Normal = other.Normal;
            HideBody = other.HideBody;
            Jets = other.Jets;
            JetBrightness = other.JetBrightness;
            JetLength = other.JetLength;
            JetWidth = other.JetWidth;
            JetSpread = other.JetSpread;
            JetTwist = other.JetTwist;
            JetTemperature = other.JetTemperature;
            JetSpeed = other.JetSpeed;
            JetPlumeStart = other.JetPlumeStart;
            JetPlumeWidth = other.JetPlumeWidth;
            JetPlumeBoost = other.JetPlumeBoost;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("coreBrightness", Format(CoreBrightness), true);
            node.SetValue("coreSize", Format(CoreSize), true);
            node.SetValue("coreTemperature", Format(CoreTemperature), true);
            node.SetValue("colorSaturation", Format(ColorSaturation), true);
            node.SetValue("glare", Format(Glare), true);
            node.SetValue("glareSize", Format(GlareSize), true);
            node.SetValue("torusInner", Format(TorusInner), true);
            node.SetValue("torusOuter", Format(TorusOuter), true);
            node.SetValue("torusThickness", Format(TorusThickness), true);
            node.SetValue("torusDensity", Format(TorusDensity), true);
            node.SetValue("torusTemperature", Format(TorusTemperature), true);
            node.SetValue("torusGlow", Format(TorusGlow), true);
            node.SetValue("torusClumpiness", Format(TorusClumpiness), true);
            node.SetValue("torusTurbulence", Format(TorusTurbulence), true);
            node.SetValue("rotationSpeed", Format(RotationSpeed), true);
            node.SetValue("spin", Format(Spin), true);
            node.SetValue("normal", Format(Normal.x) + ", " + Format(Normal.y) + ", " + Format(Normal.z), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);
            node.SetValue("jets", Jets ? "true" : "false", true);
            node.SetValue("jetBrightness", Format(JetBrightness), true);
            node.SetValue("jetLength", Format(JetLength), true);
            node.SetValue("jetWidth", Format(JetWidth), true);
            node.SetValue("jetSpread", Format(JetSpread), true);
            node.SetValue("jetTwist", Format(JetTwist), true);
            node.SetValue("jetTemperature", Format(JetTemperature), true);
            node.SetValue("jetSpeed", Format(JetSpeed), true);
            node.SetValue("jetPlumeStart", Format(JetPlumeStart), true);
            node.SetValue("jetPlumeWidth", Format(JetPlumeWidth), true);
            node.SetValue("jetPlumeBoost", Format(JetPlumeBoost), true);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}