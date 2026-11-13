using System.Globalization;
using UnityEngine;

namespace StellarEffects.Auroras
{
    internal static class AuroraLimits
    {
        public const float BrightnessMax = 20f;
        public const float LatitudeMin = 5f;
        public const float LatitudeMax = 89f;
        public const float WidthMin = 0.2f;
        public const float WidthMax = 30f;
        public const float WobbleMax = 30f;
        public const float AltitudeMin = 0.0005f;
        public const float AltitudeMax = 0.5f;
        public const float HeightMin = 0.002f;
        public const float HeightMax = 1f;
        public const float RayDropMax = 0.5f;
        public const float RayScaleMin = 1f;
        public const float RayScaleMax = 400f;
        public const float FlowSpeedMax = 20f;
        public const float PulseMax = 20f;
        public const float SeedMax = 10000f;
        public const float ColorMax = 4f;
        public const float NormalMax = 1000f;
        public const int StepsMax = 400;
    }

    internal class AuroraDefinition
    {
        public string Body = "Kerbin";
        public float Radius = 0f;
        public float Brightness = 1.5f;
        public float Latitude = 68f;
        public float Width = 3.5f;
        public float Wobble = 4f;
        public float Altitude = 0.012f;
        public float Height = 0.055f;
        public float RayDrop = 0.03f;
        public float RayScale = 28f;
        public float RayContrast = 0.55f;
        public float FlowSpeed = 0.04f;
        public float Pulse = 1f;
        public float Seed = 0f;
        public Vector3 LowColor = new Vector3(0.45f, 1f, 0.2f);
        public Vector3 HighColor = new Vector3(1f, 0.1f, 0.25f);
        public Vector3 BaseColor = new Vector3(0.3f, 0.55f, 1f);
        public int Steps = 128;
        public Vector3 Normal = Vector3.up;

        public CelestialBody Target;
        public bool Missing;
        public bool Draw = true;
        public string SourcePath;

        private AuroraDefinition baseline;

        public static AuroraDefinition CreateNew(string body)
        {
            var definition = new AuroraDefinition();
            definition.Body = body;
            definition.Commit();
            return definition;
        }

        public static AuroraDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new AuroraDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("brightness", ref definition.Brightness);
            node.TryGetValue("latitude", ref definition.Latitude);
            node.TryGetValue("width", ref definition.Width);
            node.TryGetValue("wobble", ref definition.Wobble);
            node.TryGetValue("altitude", ref definition.Altitude);
            node.TryGetValue("height", ref definition.Height);
            node.TryGetValue("rayDrop", ref definition.RayDrop);
            node.TryGetValue("rayScale", ref definition.RayScale);
            node.TryGetValue("rayContrast", ref definition.RayContrast);
            node.TryGetValue("flowSpeed", ref definition.FlowSpeed);
            node.TryGetValue("pulse", ref definition.Pulse);
            node.TryGetValue("seed", ref definition.Seed);
            node.TryGetValue("lowColor", ref definition.LowColor);
            node.TryGetValue("highColor", ref definition.HighColor);
            node.TryGetValue("baseColor", ref definition.BaseColor);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("normal", ref definition.Normal);

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            Brightness = Mathf.Clamp(Brightness, 0f, AuroraLimits.BrightnessMax);
            Latitude = Mathf.Clamp(Latitude, AuroraLimits.LatitudeMin, AuroraLimits.LatitudeMax);
            Width = Mathf.Clamp(Width, AuroraLimits.WidthMin, AuroraLimits.WidthMax);
            Wobble = Mathf.Clamp(Wobble, 0f, AuroraLimits.WobbleMax);
            Altitude = Mathf.Clamp(Altitude, AuroraLimits.AltitudeMin, AuroraLimits.AltitudeMax);
            Height = Mathf.Clamp(Height, AuroraLimits.HeightMin, AuroraLimits.HeightMax);
            RayDrop = Mathf.Clamp(RayDrop, 0f, AuroraLimits.RayDropMax);
            RayScale = Mathf.Clamp(RayScale, AuroraLimits.RayScaleMin, AuroraLimits.RayScaleMax);
            RayContrast = Mathf.Clamp01(RayContrast);
            FlowSpeed = Mathf.Clamp(FlowSpeed, -AuroraLimits.FlowSpeedMax, AuroraLimits.FlowSpeedMax);
            Pulse = Mathf.Clamp(Pulse, 0f, AuroraLimits.PulseMax);
            Seed = Mathf.Clamp(Seed, -AuroraLimits.SeedMax, AuroraLimits.SeedMax);
            LowColor = ClampColor(LowColor);
            HighColor = ClampColor(HighColor);
            BaseColor = ClampColor(BaseColor);
            Steps = Mathf.Clamp(Steps, 32, AuroraLimits.StepsMax);

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        private static Vector3 ClampColor(Vector3 c)
        {
            return new Vector3(
                Mathf.Clamp(c.x, 0f, AuroraLimits.ColorMax),
                Mathf.Clamp(c.y, 0f, AuroraLimits.ColorMax),
                Mathf.Clamp(c.z, 0f, AuroraLimits.ColorMax));
        }

        public void Commit()
        {
            baseline = new AuroraDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(AuroraDefinition other)
        {
            Body = other.Body;
            Radius = other.Radius;
            Brightness = other.Brightness;
            Latitude = other.Latitude;
            Width = other.Width;
            Wobble = other.Wobble;
            Altitude = other.Altitude;
            Height = other.Height;
            RayDrop = other.RayDrop;
            RayScale = other.RayScale;
            RayContrast = other.RayContrast;
            FlowSpeed = other.FlowSpeed;
            Pulse = other.Pulse;
            Seed = other.Seed;
            LowColor = other.LowColor;
            HighColor = other.HighColor;
            BaseColor = other.BaseColor;
            Steps = other.Steps;
            Normal = other.Normal;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("latitude", Format(Latitude), true);
            node.SetValue("width", Format(Width), true);
            node.SetValue("wobble", Format(Wobble), true);
            node.SetValue("altitude", Format(Altitude), true);
            node.SetValue("height", Format(Height), true);
            node.SetValue("rayDrop", Format(RayDrop), true);
            node.SetValue("rayScale", Format(RayScale), true);
            node.SetValue("rayContrast", Format(RayContrast), true);
            node.SetValue("flowSpeed", Format(FlowSpeed), true);
            node.SetValue("pulse", Format(Pulse), true);
            node.SetValue("seed", Format(Seed), true);
            node.SetValue("lowColor", Format(LowColor), true);
            node.SetValue("highColor", Format(HighColor), true);
            node.SetValue("baseColor", Format(BaseColor), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("normal", Format(Normal), true);
        }

        private static string Format(Vector3 v)
        {
            return Format(v.x) + ", " + Format(v.y) + ", " + Format(v.z);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}