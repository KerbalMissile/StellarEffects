using System.Globalization;
using UnityEngine;

namespace StellarEffects.Comets
{
    internal static class CometLimits
    {
        public const float BrightnessMax = 100000f;
        public const float ComaSizeMin = 0.05f;
        public const float ComaSizeMax = 1000000f;
        public const float ComaTemperatureMax = 1000000f;
        public const float ColorSaturationMax = 500f;
        public const float GlowMax = 100000f;
        public const float FlowSpeedMax = 100000f;
        public const int StepsMax = 8192;
        public const float NormalMax = 100000f;
        public const float TailBrightnessMax = 500000f;
        public const float TailLengthMin = 1f;
        public const float TailLengthMax = 100000000f;
        public const float TailWidthMin = 0.01f;
        public const float TailWidthMax = 1000000f;
        public const float TailSpreadMax = 1000f;
        public const float TailCurveMax = 1000f;
        public const float TailTemperatureMax = 1000000f;
        public const float TailDensityMax = 10000f;
        public const float TailStriationMax = 500f;
        public const float TailTurbulenceMax = 5000f;
        public const float TailDecayMin = 0.001f;
        public const float TailDecayMax = 1000f;
        public const float TailSeedMax = 100000f;
    }

    internal class CometTail
    {
        public float Brightness = 1f;
        public float Length = 300f;
        public float Width = 1f;
        public float Spread = 0.1f;
        public float Curve = 0f;
        public float Temperature = 10000f;
        public float ColorSaturation = 1.5f;
        public float Density = 1f;
        public float Striation = 0.5f;
        public float Turbulence = 0.5f;
        public float Decay = 0.5f;
        public float Seed = 0f;

        public static CometTail CreateIon()
        {
            var tail = new CometTail();
            tail.Brightness = 4.5f;
            tail.Length = 320f;
            tail.Width = 0.35f;
            tail.Spread = 0.02f;
            tail.Curve = 0f;
            tail.Temperature = 18000f;
            tail.ColorSaturation = 2.6f;
            tail.Density = 0.5f;
            tail.Striation = 0.8f;
            tail.Turbulence = 0.35f;
            tail.Decay = 0.45f;
            tail.Seed = 0f;
            return tail;
        }

        public static CometTail CreateDust()
        {
            var tail = new CometTail();
            tail.Brightness = 1.4f;
            tail.Length = 220f;
            tail.Width = 2.2f;
            tail.Spread = 0.5f;
            tail.Curve = 0.35f;
            tail.Temperature = 5600f;
            tail.ColorSaturation = 1.2f;
            tail.Density = 1.6f;
            tail.Striation = 0.6f;
            tail.Turbulence = 0.8f;
            tail.Decay = 0.35f;
            tail.Seed = 17.3f;
            return tail;
        }

        public static CometTail FromNode(ConfigNode node, CometTail defaults)
        {
            var tail = new CometTail();
            tail.CopyFrom(defaults);

            node.TryGetValue("brightness", ref tail.Brightness);
            node.TryGetValue("length", ref tail.Length);
            node.TryGetValue("width", ref tail.Width);
            node.TryGetValue("spread", ref tail.Spread);
            node.TryGetValue("curve", ref tail.Curve);
            node.TryGetValue("temperature", ref tail.Temperature);
            node.TryGetValue("colorSaturation", ref tail.ColorSaturation);
            node.TryGetValue("density", ref tail.Density);
            node.TryGetValue("striation", ref tail.Striation);
            node.TryGetValue("turbulence", ref tail.Turbulence);
            node.TryGetValue("decay", ref tail.Decay);
            node.TryGetValue("seed", ref tail.Seed);

            tail.Clamp();
            return tail;
        }

        public void Clamp()
        {
            Brightness = Mathf.Clamp(Brightness, 0f, CometLimits.TailBrightnessMax);
            Length = Mathf.Clamp(Length, CometLimits.TailLengthMin, CometLimits.TailLengthMax);
            Width = Mathf.Clamp(Width, CometLimits.TailWidthMin, CometLimits.TailWidthMax);
            Spread = Mathf.Clamp(Spread, 0f, CometLimits.TailSpreadMax);
            Curve = Mathf.Clamp(Curve, 0f, CometLimits.TailCurveMax);
            Temperature = Mathf.Clamp(Temperature, 1000f, CometLimits.TailTemperatureMax);
            ColorSaturation = Mathf.Clamp(ColorSaturation, 0f, CometLimits.ColorSaturationMax);
            Density = Mathf.Clamp(Density, 0f, CometLimits.TailDensityMax);
            Striation = Mathf.Clamp(Striation, 0f, CometLimits.TailStriationMax);
            Turbulence = Mathf.Clamp(Turbulence, 0f, CometLimits.TailTurbulenceMax);
            Decay = Mathf.Clamp(Decay, CometLimits.TailDecayMin, CometLimits.TailDecayMax);
            Seed = Mathf.Clamp(Seed, -CometLimits.TailSeedMax, CometLimits.TailSeedMax);
        }

        public void CopyFrom(CometTail other)
        {
            Brightness = other.Brightness;
            Length = other.Length;
            Width = other.Width;
            Spread = other.Spread;
            Curve = other.Curve;
            Temperature = other.Temperature;
            ColorSaturation = other.ColorSaturation;
            Density = other.Density;
            Striation = other.Striation;
            Turbulence = other.Turbulence;
            Decay = other.Decay;
            Seed = other.Seed;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("length", Format(Length), true);
            node.SetValue("width", Format(Width), true);
            node.SetValue("spread", Format(Spread), true);
            node.SetValue("curve", Format(Curve), true);
            node.SetValue("temperature", Format(Temperature), true);
            node.SetValue("colorSaturation", Format(ColorSaturation), true);
            node.SetValue("density", Format(Density), true);
            node.SetValue("striation", Format(Striation), true);
            node.SetValue("turbulence", Format(Turbulence), true);
            node.SetValue("decay", Format(Decay), true);
            node.SetValue("seed", Format(Seed), true);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    internal class CometDefinition
    {
        public const string NodeName = "STELLAREFFECTS_COMET";

        public string Body = "Sun";
        public float Radius = 0f;
        public float Brightness = 1.2f;
        public float ComaSize = 1.4f;
        public float ComaTemperature = 12000f;
        public float ColorSaturation = 1.6f;
        public float Glow = 3f;
        public float FlowSpeed = 35f;
        public int Steps = 300;
        public Vector3 Normal = Vector3.up;
        public bool AutoDirection = true;
        public bool HideBody = false;
        public CometTail Ion = CometTail.CreateIon();
        public CometTail Dust = CometTail.CreateDust();

        public CelestialBody Target;
        public bool Missing;
        public bool Draw = true;
        public string SourcePath;

        private CometDefinition baseline;

        public static CometDefinition CreateNew(string body)
        {
            var definition = new CometDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static CometDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new CometDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("brightness", ref definition.Brightness);
            node.TryGetValue("comaSize", ref definition.ComaSize);
            node.TryGetValue("comaTemperature", ref definition.ComaTemperature);
            node.TryGetValue("colorSaturation", ref definition.ColorSaturation);
            node.TryGetValue("glow", ref definition.Glow);
            node.TryGetValue("flowSpeed", ref definition.FlowSpeed);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("normal", ref definition.Normal);
            node.TryGetValue("autoDirection", ref definition.AutoDirection);
            node.TryGetValue("hideBody", ref definition.HideBody);

            if (node.HasNode("ion"))
                definition.Ion = CometTail.FromNode(node.GetNode("ion"), CometTail.CreateIon());

            if (node.HasNode("dust"))
                definition.Dust = CometTail.FromNode(node.GetNode("dust"), CometTail.CreateDust());

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            Brightness = Mathf.Clamp(Brightness, 0f, CometLimits.BrightnessMax);
            ComaSize = Mathf.Clamp(ComaSize, CometLimits.ComaSizeMin, CometLimits.ComaSizeMax);
            ComaTemperature = Mathf.Clamp(ComaTemperature, 1000f, CometLimits.ComaTemperatureMax);
            ColorSaturation = Mathf.Clamp(ColorSaturation, 0f, CometLimits.ColorSaturationMax);
            Glow = Mathf.Clamp(Glow, 0f, CometLimits.GlowMax);
            FlowSpeed = Mathf.Clamp(FlowSpeed, 0f, CometLimits.FlowSpeedMax);
            Steps = Mathf.Clamp(Steps, 32, CometLimits.StepsMax);

            Ion.Clamp();
            Dust.Clamp();

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        public void Commit()
        {
            baseline = new CometDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(CometDefinition other)
        {
            Body = other.Body;
            Radius = other.Radius;
            Brightness = other.Brightness;
            ComaSize = other.ComaSize;
            ComaTemperature = other.ComaTemperature;
            ColorSaturation = other.ColorSaturation;
            Glow = other.Glow;
            FlowSpeed = other.FlowSpeed;
            Steps = other.Steps;
            Normal = other.Normal;
            AutoDirection = other.AutoDirection;
            HideBody = other.HideBody;

            Ion.CopyFrom(other.Ion);
            Dust.CopyFrom(other.Dust);
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("comaSize", Format(ComaSize), true);
            node.SetValue("comaTemperature", Format(ComaTemperature), true);
            node.SetValue("colorSaturation", Format(ColorSaturation), true);
            node.SetValue("glow", Format(Glow), true);
            node.SetValue("flowSpeed", Format(FlowSpeed), true);
            node.SetValue("normal", Format(Normal.x) + ", " + Format(Normal.y) + ", " + Format(Normal.z), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("autoDirection", AutoDirection ? "true" : "false", true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);

            ConfigNode ion = node.HasNode("ion") ? node.GetNode("ion") : node.AddNode("ion");
            Ion.WriteTo(ion);

            ConfigNode dust = node.HasNode("dust") ? node.GetNode("dust") : node.AddNode("dust");
            Dust.WriteTo(dust);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
