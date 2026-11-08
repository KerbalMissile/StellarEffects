using System.Globalization;
using UnityEngine;

namespace StellarEffects.Nebulae
{
    internal static class NebulaLimits
    {
        public const int GasMax = 5;
        public const float SizeMin = 1f;
        public const float SizeMax = 10000000f;
        public const float BrightnessMax = 1000f;
        public const float ColorSaturationMax = 50f;
        public const float StarTemperatureMax = 100000f;
        public const float StarBrightnessMax = 1000f;
        public const float StarFalloffMin = 0.01f;
        public const float StarFalloffMax = 20f;
        public const float RotationSpeedMax = 1000f;
        public const int StepsMax = 2000;
        public const float NormalMax = 1000f;
        public const float GasColorMax = 4f;
        public const float GasDensityMax = 20f;
        public const float GasEmissionMax = 100f;
        public const float GasAbsorptionMax = 50f;
        public const float GasScaleMin = 0.1f;
        public const float GasScaleMax = 200f;
        public const float GasWarpMax = 5f;
        public const float GasExtentMin = 0.02f;
        public const float GasExtentMax = 1.5f;
        public const float GasFlattenMin = 0.02f;
        public const float GasHollowMax = 0.9f;
        public const float GasPhaseMax = 0.95f;
        public const float GasShadowMax = 20f;
        public const float GasSeedMax = 1000f;
        public const int ShapeMax = 4;
        public const float ShapeRatioMin = 0.05f;
        public const float ShapeRatioMax = 0.95f;
        public const float ShapeArmsMax = 12f;
        public const float IrregularityMax = 3f;
        public const float SeedMax = 1000f;
    }

    internal class NebulaGas
    {
        private static readonly float[,] Palette =
        {
            { 0.90f, 0.18f, 0.45f },
            { 0.55f, 0.25f, 0.95f },
            { 0.30f, 0.40f, 1.00f },
            { 1.00f, 0.50f, 0.30f },
            { 0.30f, 0.80f, 0.85f }
        };

        public float ColorR = 1f;
        public float ColorG = 1f;
        public float ColorB = 1f;
        public float Density = 1f;
        public float Emission = 1f;
        public float Absorption = 0.6f;
        public float Scale = 4f;
        public float Coverage = 0.35f;
        public float Contrast = 0.4f;
        public float Phase = 0.3f;
        public float Shadow = 1f;
        public float Seed = 0f;

        public static NebulaGas CreateDefault(int index)
        {
            int p = index % 5;
            var gas = new NebulaGas();
            gas.ColorR = Palette[p, 0];
            gas.ColorG = Palette[p, 1];
            gas.ColorB = Palette[p, 2];
            gas.Density = 1f - 0.1f * p;
            gas.Scale = 3f + 2f * p;
            gas.Coverage = 0.3f + 0.05f * p;
            gas.Seed = 13.7f * index;
            return gas;
        }

        public static NebulaGas FromNode(ConfigNode node, int index)
        {
            NebulaGas gas = CreateDefault(index);

            node.TryGetValue("colorR", ref gas.ColorR);
            node.TryGetValue("colorG", ref gas.ColorG);
            node.TryGetValue("colorB", ref gas.ColorB);
            node.TryGetValue("density", ref gas.Density);
            node.TryGetValue("emission", ref gas.Emission);
            node.TryGetValue("absorption", ref gas.Absorption);
            node.TryGetValue("scale", ref gas.Scale);
            node.TryGetValue("coverage", ref gas.Coverage);
            node.TryGetValue("contrast", ref gas.Contrast);
            node.TryGetValue("phase", ref gas.Phase);
            node.TryGetValue("shadow", ref gas.Shadow);
            node.TryGetValue("seed", ref gas.Seed);

            gas.Clamp();
            return gas;
        }

        public void Clamp()
        {
            ColorR = Mathf.Clamp(ColorR, 0f, NebulaLimits.GasColorMax);
            ColorG = Mathf.Clamp(ColorG, 0f, NebulaLimits.GasColorMax);
            ColorB = Mathf.Clamp(ColorB, 0f, NebulaLimits.GasColorMax);
            Density = Mathf.Clamp(Density, 0f, NebulaLimits.GasDensityMax);
            Emission = Mathf.Clamp(Emission, 0f, NebulaLimits.GasEmissionMax);
            Absorption = Mathf.Clamp(Absorption, 0f, NebulaLimits.GasAbsorptionMax);
            Scale = Mathf.Clamp(Scale, NebulaLimits.GasScaleMin, NebulaLimits.GasScaleMax);
            Coverage = Mathf.Clamp(Coverage, 0f, 0.95f);
            Contrast = Mathf.Clamp01(Contrast);
            Phase = Mathf.Clamp(Phase, -NebulaLimits.GasPhaseMax, NebulaLimits.GasPhaseMax);
            Shadow = Mathf.Clamp(Shadow, 0f, NebulaLimits.GasShadowMax);
            Seed = Mathf.Clamp(Seed, -NebulaLimits.GasSeedMax, NebulaLimits.GasSeedMax);
        }

        public void CopyFrom(NebulaGas other)
        {
            ColorR = other.ColorR;
            ColorG = other.ColorG;
            ColorB = other.ColorB;
            Density = other.Density;
            Emission = other.Emission;
            Absorption = other.Absorption;
            Scale = other.Scale;
            Coverage = other.Coverage;
            Contrast = other.Contrast;
            Phase = other.Phase;
            Shadow = other.Shadow;
            Seed = other.Seed;
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("colorR", Format(ColorR), true);
            node.SetValue("colorG", Format(ColorG), true);
            node.SetValue("colorB", Format(ColorB), true);
            node.SetValue("density", Format(Density), true);
            node.SetValue("emission", Format(Emission), true);
            node.SetValue("absorption", Format(Absorption), true);
            node.SetValue("scale", Format(Scale), true);
            node.SetValue("coverage", Format(Coverage), true);
            node.SetValue("contrast", Format(Contrast), true);
            node.SetValue("phase", Format(Phase), true);
            node.SetValue("shadow", Format(Shadow), true);
            node.SetValue("seed", Format(Seed), true);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    internal class NebulaDefinition
    {
        public const string NodeName = "STELLAREFFECTS_NEBULA";

        public string Body = "Sun";
        public int Index = 0;
        public float Radius = 0f;
        public float Size = 3000f;
        public float Brightness = 1f;
        public float ColorSaturation = 1.3f;
        public float StarTemperature = 22000f;
        public float StarBrightness = 2f;
        public float StarFalloff = 0.35f;
        public float RotationSpeed = 0.1f;
        public float Spin = 1f;
        public int Steps = 100;
        public Vector3 Normal = Vector3.up;
        public bool HideBody = false;
        public float Warp = 0.8f;
        public float Extent = 0.9f;
        public float Flatten = 1f;
        public float Hollow = 0.9f;
        public int Shape = 4;
        public float ShapeRatio = 0.5f;
        public float ShapeArms = 3f;
        public float Irregularity = 0.6f;
        public float Variation = 0.6f;
        public float Seed = 0f;
        public int GasCount = 3;
        public NebulaGas[] Gases = new NebulaGas[NebulaLimits.GasMax];

        public CelestialBody Target;
        public bool Missing;
        public string SourcePath;

        private NebulaDefinition baseline;

        public NebulaDefinition()
        {
            for (int i = 0; i < Gases.Length; i++)
                Gases[i] = NebulaGas.CreateDefault(i);
        }

        public static NebulaDefinition CreateNew(string body, int index)
        {
            var definition = new NebulaDefinition();
            definition.Body = body;
            definition.Index = index;
            definition.Commit();
            return definition;
        }

        public static NebulaDefinition FromNode(ConfigNode node, string sourcePath)
        {
            var definition = new NebulaDefinition();

            node.TryGetValue("body", ref definition.Body);
            node.TryGetValue("index", ref definition.Index);
            node.TryGetValue("radius", ref definition.Radius);
            node.TryGetValue("size", ref definition.Size);
            node.TryGetValue("brightness", ref definition.Brightness);
            node.TryGetValue("colorSaturation", ref definition.ColorSaturation);
            node.TryGetValue("starTemperature", ref definition.StarTemperature);
            node.TryGetValue("starBrightness", ref definition.StarBrightness);
            node.TryGetValue("starFalloff", ref definition.StarFalloff);
            node.TryGetValue("rotationSpeed", ref definition.RotationSpeed);
            node.TryGetValue("spin", ref definition.Spin);
            node.TryGetValue("steps", ref definition.Steps);
            node.TryGetValue("normal", ref definition.Normal);
            node.TryGetValue("hideBody", ref definition.HideBody);
            node.TryGetValue("warp", ref definition.Warp);
            node.TryGetValue("extent", ref definition.Extent);
            node.TryGetValue("flatten", ref definition.Flatten);
            node.TryGetValue("hollow", ref definition.Hollow);
            node.TryGetValue("shape", ref definition.Shape);
            node.TryGetValue("shapeRatio", ref definition.ShapeRatio);
            node.TryGetValue("shapeArms", ref definition.ShapeArms);
            node.TryGetValue("irregularity", ref definition.Irregularity);
            node.TryGetValue("variation", ref definition.Variation);
            node.TryGetValue("seed", ref definition.Seed);
            node.TryGetValue("gasCount", ref definition.GasCount);

            for (int i = 0; i < NebulaLimits.GasMax; i++)
            {
                string name = "gas_" + (i + 1);
                if (node.HasNode(name))
                    definition.Gases[i] = NebulaGas.FromNode(node.GetNode(name), i);
            }

            definition.Clamp();
            definition.SourcePath = sourcePath;
            definition.Commit();

            return definition;
        }

        public void Clamp()
        {
            Radius = Mathf.Max(Radius, 0f);
            Size = Mathf.Clamp(Size, NebulaLimits.SizeMin, NebulaLimits.SizeMax);
            Brightness = Mathf.Clamp(Brightness, 0f, NebulaLimits.BrightnessMax);
            ColorSaturation = Mathf.Clamp(ColorSaturation, 0f, NebulaLimits.ColorSaturationMax);
            StarTemperature = Mathf.Clamp(StarTemperature, 1000f, NebulaLimits.StarTemperatureMax);
            StarBrightness = Mathf.Clamp(StarBrightness, 0f, NebulaLimits.StarBrightnessMax);
            StarFalloff = Mathf.Clamp(StarFalloff, NebulaLimits.StarFalloffMin, NebulaLimits.StarFalloffMax);
            RotationSpeed = Mathf.Clamp(RotationSpeed, -NebulaLimits.RotationSpeedMax, NebulaLimits.RotationSpeedMax);
            Steps = Mathf.Clamp(Steps, 32, NebulaLimits.StepsMax);
            Warp = Mathf.Clamp(Warp, 0f, NebulaLimits.GasWarpMax);
            Extent = Mathf.Clamp(Extent, NebulaLimits.GasExtentMin, NebulaLimits.GasExtentMax);
            Flatten = Mathf.Clamp(Flatten, NebulaLimits.GasFlattenMin, 1f);
            Hollow = Mathf.Clamp(Hollow, 0f, NebulaLimits.GasHollowMax);
            Shape = Mathf.Clamp(Shape, 0, NebulaLimits.ShapeMax);
            ShapeRatio = Mathf.Clamp(ShapeRatio, NebulaLimits.ShapeRatioMin, NebulaLimits.ShapeRatioMax);
            ShapeArms = Mathf.Clamp(ShapeArms, 1f, NebulaLimits.ShapeArmsMax);
            Irregularity = Mathf.Clamp(Irregularity, 0f, NebulaLimits.IrregularityMax);
            Variation = Mathf.Clamp01(Variation);
            Seed = Mathf.Clamp(Seed, -NebulaLimits.SeedMax, NebulaLimits.SeedMax);
            GasCount = Mathf.Clamp(GasCount, 1, NebulaLimits.GasMax);

            foreach (NebulaGas gas in Gases)
                gas.Clamp();

            if (Normal.sqrMagnitude < 0.0001f)
                Normal = Vector3.up;
        }

        public void Commit()
        {
            baseline = new NebulaDefinition();
            baseline.CopyFrom(this);
        }

        public void Revert()
        {
            if (baseline != null)
                CopyFrom(baseline);
        }

        public void CopyFrom(NebulaDefinition other)
        {
            Body = other.Body;
            Index = other.Index;
            Radius = other.Radius;
            Size = other.Size;
            Brightness = other.Brightness;
            ColorSaturation = other.ColorSaturation;
            StarTemperature = other.StarTemperature;
            StarBrightness = other.StarBrightness;
            StarFalloff = other.StarFalloff;
            RotationSpeed = other.RotationSpeed;
            Spin = other.Spin;
            Steps = other.Steps;
            Normal = other.Normal;
            HideBody = other.HideBody;
            Warp = other.Warp;
            Extent = other.Extent;
            Flatten = other.Flatten;
            Hollow = other.Hollow;
            Shape = other.Shape;
            ShapeRatio = other.ShapeRatio;
            ShapeArms = other.ShapeArms;
            Irregularity = other.Irregularity;
            Variation = other.Variation;
            Seed = other.Seed;
            GasCount = other.GasCount;

            for (int i = 0; i < Gases.Length; i++)
                Gases[i].CopyFrom(other.Gases[i]);
        }

        public void WriteTo(ConfigNode node)
        {
            node.SetValue("body", Body, true);
            node.SetValue("index", Index.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("radius", Format(Radius), true);
            node.SetValue("size", Format(Size), true);
            node.SetValue("brightness", Format(Brightness), true);
            node.SetValue("colorSaturation", Format(ColorSaturation), true);
            node.SetValue("starTemperature", Format(StarTemperature), true);
            node.SetValue("starBrightness", Format(StarBrightness), true);
            node.SetValue("starFalloff", Format(StarFalloff), true);
            node.SetValue("rotationSpeed", Format(RotationSpeed), true);
            node.SetValue("spin", Format(Spin), true);
            node.SetValue("normal", Format(Normal.x) + ", " + Format(Normal.y) + ", " + Format(Normal.z), true);
            node.SetValue("steps", Steps.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("hideBody", HideBody ? "true" : "false", true);
            node.SetValue("warp", Format(Warp), true);
            node.SetValue("extent", Format(Extent), true);
            node.SetValue("flatten", Format(Flatten), true);
            node.SetValue("hollow", Format(Hollow), true);
            node.SetValue("shape", Shape.ToString(CultureInfo.InvariantCulture), true);
            node.SetValue("shapeRatio", Format(ShapeRatio), true);
            node.SetValue("shapeArms", Format(ShapeArms), true);
            node.SetValue("irregularity", Format(Irregularity), true);
            node.SetValue("variation", Format(Variation), true);
            node.SetValue("seed", Format(Seed), true);
            node.SetValue("gasCount", GasCount.ToString(CultureInfo.InvariantCulture), true);

            for (int i = 0; i < NebulaLimits.GasMax; i++)
            {
                string name = "gas_" + (i + 1);

                if (i < GasCount)
                {
                    ConfigNode gasNode = node.HasNode(name) ? node.GetNode(name) : node.AddNode(name);
                    Gases[i].WriteTo(gasNode);
                }
                else if (node.HasNode(name))
                {
                    node.RemoveNode(name);
                }
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}