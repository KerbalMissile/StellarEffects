using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using StellarEffects.Blackholes;
using StellarEffects.Comets;
using StellarEffects.Nebulae;
using StellarEffects.Quasars;
using UnityEngine;

namespace StellarEffects.UI
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal sealed class SettingsWindow : MonoBehaviour
    {
        private const string LockId = "StellarEffects_SettingsWindow";
        private const int WindowId = 0x53544C52;
        private const float ListWidth = 150f;
        private const float LabelWidth = 110f;
        private const float SliderWidth = 220f;
        private const float BoxWidth = 84f;

        private static string version = ReadVersion();

        private readonly Dictionary<string, string> buffers = new Dictionary<string, string>();
        private Rect rect = new Rect(120, 120, 700, 610);
        private Vector2 listScroll;
        private Vector2 fieldScroll;
        private string newBody = "";
        private string status = "";
        private string rowPrefix = "";
        private int selected;
        private int mode;
        private bool visible;
        private bool confirmRemove;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (ctrl && alt && Input.GetKeyDown(KeyCode.V))
            {
                visible = !visible;
                if (!visible) ReleaseLock();
            }
        }

        void OnGUI()
        {
            if (!visible) return;

            GUI.skin = HighLogic.Skin;
            rect = GUI.Window(WindowId, rect, Draw, "StellarEffects v" + version + " (Ctrl + Alt + V)");

            if (rect.Contains(Event.current.mousePosition))
                InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS, LockId);
            else
                ReleaseLock();
        }

        void OnDisable()
        {
            ReleaseLock();
        }

        private void Draw(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(mode == 0, "Black holes", GUI.skin.button) && mode != 0) SetMode(0);
            if (GUILayout.Toggle(mode == 1, "Quasars", GUI.skin.button) && mode != 1) SetMode(1);
            if (GUILayout.Toggle(mode == 2, "Nebulae", GUI.skin.button) && mode != 2) SetMode(2);
            if (GUILayout.Toggle(mode == 3, "Comets", GUI.skin.button) && mode != 3) SetMode(3);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Quality", GUILayout.Width(LabelWidth));
            foreach (QualityPreset preset in new[] { QualityPreset.High, QualityPreset.Medium, QualityPreset.Low })
            {
                if (GUILayout.Toggle(StellarQuality.Current == preset, preset.ToString(), GUI.skin.button) && StellarQuality.Current != preset)
                    StellarQuality.Current = preset;
            }
            GUILayout.EndHorizontal();

            if (mode == 1)
            {
                DrawQuasarTab();
                GUI.DragWindow(new Rect(0, 0, 10000, 22));
                return;
            }

            if (mode == 3)
            {
                DrawCometTab();
                GUI.DragWindow(new Rect(0, 0, 10000, 22));
                return;
            }

            if (mode == 2)
            {
                DrawNebulaTab();
                GUI.DragWindow(new Rect(0, 0, 10000, 22));
                return;
            }

            BlackHoleManager manager = BlackHoleManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("Black holes are not loaded.");
                GUI.DragWindow();
                return;
            }

            List<BlackHoleDefinition> holes = manager.Holes;
            selected = holes.Count == 0 ? -1 : Mathf.Clamp(selected, 0, holes.Count - 1);

            GUILayout.BeginHorizontal();
            DrawList(holes);
            DrawEditor(holes);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = selected >= 0;
            if (GUILayout.Button("Save")) Save(holes[selected]);
            if (GUILayout.Button("Reset Config")) Reset(holes[selected]);
            if (GUILayout.Button(confirmRemove ? "Click again to remove" : "Remove")) Remove(holes[selected]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(status);

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void SetMode(int newMode)
        {
            mode = newMode;
            selected = 0;
            confirmRemove = false;
            buffers.Clear();
            status = "";
            newBody = "";
        }

        private void DrawQuasarTab()
        {
            QuasarManager manager = QuasarManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("Quasars are not loaded.");
                return;
            }

            if (!manager.ShaderReady)
                GUILayout.Label("Quasar shader is missing from stellareffects.bundle. Quasars will not render. See KSP.log.");

            List<QuasarDefinition> quasars = manager.Quasars;
            selected = quasars.Count == 0 ? -1 : Mathf.Clamp(selected, 0, quasars.Count - 1);

            GUILayout.BeginHorizontal();
            DrawQuasarList(quasars);
            DrawQuasarEditor(quasars);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = selected >= 0;
            if (GUILayout.Button("Save")) SaveQuasar(quasars[selected]);
            if (GUILayout.Button("Reset Config")) ResetQuasar(quasars[selected]);
            if (GUILayout.Button(confirmRemove ? "Click again to remove" : "Remove")) RemoveQuasar(quasars[selected]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(status);
        }

        private void DrawNebulaTab()
        {
            NebulaManager manager = NebulaManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("Nebulae are not loaded.");
                return;
            }

            if (!manager.ShaderReady)
                GUILayout.Label("Nebula shader is missing from stellareffects.bundle. Nebulae will not render. See KSP.log.");

            List<NebulaDefinition> nebulae = manager.Definitions;
            selected = nebulae.Count == 0 ? -1 : Mathf.Clamp(selected, 0, nebulae.Count - 1);

            GUILayout.BeginHorizontal();
            DrawNebulaList(nebulae);
            DrawNebulaEditor(nebulae);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = selected >= 0;
            if (GUILayout.Button("Save")) SaveNebula(nebulae[selected]);
            if (GUILayout.Button("Reset Config")) ResetNebula(nebulae[selected]);
            if (GUILayout.Button(confirmRemove ? "Click again to remove" : "Remove")) RemoveNebula(nebulae[selected]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(status);
        }

        private void DrawCometTab()
        {
            StellarEffects.Comets.CometManager manager = StellarEffects.Comets.CometManager.Instance;
            if (manager == null)
            {
                GUILayout.Label("Comets are not loaded.");
                return;
            }

            if (!manager.ShaderReady)
                GUILayout.Label("Comet shader is missing from stellareffects.bundle. Comets will not render. See KSP.log.");

            List<StellarEffects.Comets.CometDefinition> comets = manager.Comets;
            selected = comets.Count == 0 ? -1 : Mathf.Clamp(selected, 0, comets.Count - 1);

            GUILayout.BeginHorizontal();
            DrawCometList(comets);
            DrawCometEditor(comets);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = selected >= 0;
            if (GUILayout.Button("Save")) SaveComet(comets[selected]);
            if (GUILayout.Button("Reset Config")) ResetComet(comets[selected]);
            if (GUILayout.Button(confirmRemove ? "Click again to remove" : "Remove")) RemoveComet(comets[selected]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(status);
        }

        private void DrawList(List<BlackHoleDefinition> holes)
        {
            GUILayout.BeginVertical(GUILayout.Width(ListWidth));

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(320));
            for (int i = 0; i < holes.Count; i++)
            {
                bool on = GUILayout.Toggle(selected == i, holes[i].Body, GUI.skin.button);
                if (on && selected != i) Select(i);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("New body");
            newBody = GUILayout.TextField(newBody);
            if (GUILayout.Button("Add")) AddBody();

            GUILayout.EndVertical();
        }

        private void DrawQuasarList(List<QuasarDefinition> quasars)
        {
            GUILayout.BeginVertical(GUILayout.Width(ListWidth));

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(320));
            for (int i = 0; i < quasars.Count; i++)
            {
                bool on = GUILayout.Toggle(selected == i, quasars[i].Body, GUI.skin.button);
                if (on && selected != i) Select(i);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("New body");
            newBody = GUILayout.TextField(newBody);
            if (GUILayout.Button("Add")) AddQuasar();

            GUILayout.EndVertical();
        }

        private void DrawNebulaList(List<NebulaDefinition> nebulae)
        {
            GUILayout.BeginVertical(GUILayout.Width(ListWidth));

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(320));
            for (int i = 0; i < nebulae.Count; i++)
            {
                bool on = GUILayout.Toggle(selected == i, nebulae[i].Index == 0 ? nebulae[i].Body : nebulae[i].Body + " #" + (nebulae[i].Index + 1), GUI.skin.button);
                if (on && selected != i) Select(i);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("New body");
            newBody = GUILayout.TextField(newBody);
            if (GUILayout.Button("Add")) AddNebula();

            GUILayout.EndVertical();
        }

        private void DrawCometList(List<StellarEffects.Comets.CometDefinition> comets)
        {
            GUILayout.BeginVertical(GUILayout.Width(ListWidth));

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(320));
            for (int i = 0; i < comets.Count; i++)
            {
                bool on = GUILayout.Toggle(selected == i, comets[i].Body, GUI.skin.button);
                if (on && selected != i) Select(i);
            }
            GUILayout.EndScrollView();

            GUILayout.Label("New body");
            newBody = GUILayout.TextField(newBody);
            if (GUILayout.Button("Add")) AddComet();

            GUILayout.EndVertical();
        }

        private void DrawEditor(List<BlackHoleDefinition> holes)
        {
            fieldScroll = GUILayout.BeginScrollView(fieldScroll, GUILayout.Height(400));

            if (selected < 0)
            {
                GUILayout.Label("No black holes defined. Add a body on the left.");
                GUILayout.EndScrollView();
                return;
            }

            BlackHoleDefinition h = holes[selected];

            h.Draw = GUILayout.Toggle(h.Draw, "draw");
            h.HorizonRadius = Row("horizonRadius", h.HorizonRadius, 0f, 1e9f, 0f, 1e9f, false, true);
            h.Temperature = Row("temperature", h.Temperature, 1000f, 40000f);
            h.Brightness = Row("brightness", h.Brightness, 0f, 20f);
            h.ColorSaturation = Row("colorSaturation", h.ColorSaturation, 0f, 10f);
            h.DiskInner = Row("diskInner", h.DiskInner, 1.5f, 30f, 1.5f, float.MaxValue, true, true);
            h.DiskOuter = Row("diskOuter", h.DiskOuter, 2.5f, 80f, 2.5f, float.MaxValue, true, true);
            h.DiskThickness = Row("diskThickness", h.DiskThickness, 0.005f, 0.5f);
            h.Glow = Row("glow", h.Glow, 0f, 5f);
            h.PhotonRing = Row("photonRing", h.PhotonRing, 0f, 5f);
            h.RotationSpeed = Row("rotationSpeed", h.RotationSpeed, -20f, 20f);
            h.Turbulence = Row("turbulence", h.Turbulence, 0f, 1f);
            h.Twirl = Row("twirl", h.Twirl, 0f, 5f, 0f, 10f);
            h.GlowShift = GUILayout.Toggle(h.GlowShift, "glowShift");
            h.Jets = GUILayout.Toggle(h.Jets, "jets");
            h.JetBrightness = Row("jetBrightness", h.JetBrightness, 0f, 50f);
            h.JetLength = Row("jetLength", h.JetLength, 5f, 5000f, 5f, float.MaxValue, true, true);
            h.JetWidth = Row("jetWidth", h.JetWidth, 0.2f, 100f, 0.2f, float.MaxValue, true, true);
            h.JetSpread = Row("jetSpread", h.JetSpread, 0f, 1.5f);
            h.JetTwist = Row("jetTwist", h.JetTwist, 0f, 0.02f);
            h.JetTemperature = Row("jetTemperature", h.JetTemperature, 3000f, 40000f);
            h.JetSpeed = Row("jetSpeed", h.JetSpeed, 0f, 0.999f);
            h.Steps = Mathf.RoundToInt(Row("steps", h.Steps, 32f, 600f));
            h.Debug = Mathf.RoundToInt(Row("debug", h.Debug, 0f, 3f));

            Vector3 normal = h.Normal;
            normal.x = Row("normal x", normal.x, -20f, 20f);
            normal.y = Row("normal y", normal.y, -20f, 20f);
            normal.z = Row("normal z", normal.z, -20f, 20f);
            h.Normal = normal;

            h.HideBody = GUILayout.Toggle(h.HideBody, "hideBody");
            h.Spin = GUILayout.Toggle(h.Spin < 0f, "reverse spin") ? -1f : 1f;

            h.Clamp();

            GUILayout.EndScrollView();
        }

        private void DrawQuasarEditor(List<QuasarDefinition> quasars)
        {
            fieldScroll = GUILayout.BeginScrollView(fieldScroll, GUILayout.Height(400));

            if (selected < 0)
            {
                GUILayout.Label("No quasars defined. Add a body on the left.");
                GUILayout.EndScrollView();
                return;
            }

            QuasarDefinition q = quasars[selected];

            q.Draw = GUILayout.Toggle(q.Draw, "draw");
            q.Radius = Row("radius", q.Radius, 0f, 1e9f, 0f, 1e9f, false, true);
            q.CoreBrightness = Row("coreBrightness", q.CoreBrightness, 0f, 30f, 0f, QuasarLimits.CoreBrightnessMax);
            q.CoreSize = Row("coreSize", q.CoreSize, 0.5f, 100f, QuasarLimits.CoreSizeMin, float.MaxValue, true, true);
            q.CoreTemperature = Row("coreTemperature", q.CoreTemperature, 1000f, 40000f, 1000f, QuasarLimits.CoreTemperatureMax);
            q.ColorSaturation = Row("colorSaturation", q.ColorSaturation, 0f, 6f, 0f, QuasarLimits.ColorSaturationMax);
            q.Glare = Row("glare", q.Glare, 0f, 10f, 0f, QuasarLimits.GlareMax);
            q.GlareSize = Row("glareSize", q.GlareSize, 2f, 500f, 2f, float.MaxValue, true, true);
            q.TorusDensity = Row("torusDensity", q.TorusDensity, 0f, 5f, 0f, QuasarLimits.TorusDensityMax);
            q.TorusInner = Row("torusInner", q.TorusInner, 2f, 300f, 2f, float.MaxValue, true, true);
            q.TorusOuter = Row("torusOuter", q.TorusOuter, 4f, 600f, 4f, float.MaxValue, true, true);
            q.TorusThickness = Row("torusThickness", q.TorusThickness, 0.02f, 1.5f, QuasarLimits.TorusThicknessMin, QuasarLimits.TorusThicknessMax);
            q.TorusTemperature = Row("torusTemperature", q.TorusTemperature, 800f, 6000f, 800f, QuasarLimits.TorusTemperatureMax);
            q.TorusGlow = Row("torusGlow", q.TorusGlow, 0f, 10f, 0f, QuasarLimits.TorusGlowMax);
            q.TorusClumpiness = Row("torusClumpiness", q.TorusClumpiness, 0f, 1f);
            q.TorusTurbulence = Row("torusTurbulence", q.TorusTurbulence, 0f, 10f, 0f, QuasarLimits.TorusTurbulenceMax);
            q.RotationSpeed = Row("rotationSpeed", q.RotationSpeed, -20f, 20f, -QuasarLimits.RotationSpeedMax, QuasarLimits.RotationSpeedMax);
            q.Jets = GUILayout.Toggle(q.Jets, "jets");
            q.JetBrightness = Row("jetBrightness", q.JetBrightness, 0f, 50f, 0f, QuasarLimits.JetBrightnessMax);
            q.JetLength = Row("jetLength", q.JetLength, 5f, 5000f, 5f, float.MaxValue, true, true);
            q.JetWidth = Row("jetWidth", q.JetWidth, 0.2f, 50f, 0.2f, float.MaxValue, true, true);
            q.JetSpread = Row("jetSpread", q.JetSpread, 0f, 1.5f, 0f, QuasarLimits.JetSpreadMax);
            q.JetTwist = Row("jetTwist", q.JetTwist, -0.02f, 0.02f, -QuasarLimits.JetTwistMax, QuasarLimits.JetTwistMax);
            q.JetTemperature = Row("jetTemperature", q.JetTemperature, 3000f, 40000f, 3000f, QuasarLimits.JetTemperatureMax);
            q.JetSpeed = Row("jetSpeed", q.JetSpeed, 0f, 0.999f, 0f, QuasarLimits.JetSpeedMax);
            q.JetPlumeStart = Row("jetPlumeStart", q.JetPlumeStart, 0.1f, 1f, QuasarLimits.JetPlumeStartMin, QuasarLimits.JetPlumeStartMax);
            q.JetPlumeWidth = Row("jetPlumeWidth", q.JetPlumeWidth, 0f, 0.3f, 0f, QuasarLimits.JetPlumeWidthMax);
            q.JetPlumeBoost = Row("jetPlumeBoost", q.JetPlumeBoost, 0f, 10f, 0f, QuasarLimits.JetPlumeBoostMax);
            q.Steps = Mathf.RoundToInt(Row("steps", q.Steps, 32f, 600f, 32f, QuasarLimits.StepsMax));

            Vector3 normal = q.Normal;
            normal.x = Row("normal x", normal.x, -20f, 20f, -QuasarLimits.NormalMax, QuasarLimits.NormalMax);
            normal.y = Row("normal y", normal.y, -20f, 20f, -QuasarLimits.NormalMax, QuasarLimits.NormalMax);
            normal.z = Row("normal z", normal.z, -20f, 20f, -QuasarLimits.NormalMax, QuasarLimits.NormalMax);
            q.Normal = normal;

            q.HideBody = GUILayout.Toggle(q.HideBody, "hideBody");
            q.Spin = GUILayout.Toggle(q.Spin < 0f, "reverse spin") ? -1f : 1f;

            q.Clamp();

            GUILayout.EndScrollView();
        }

        private void DrawNebulaEditor(List<NebulaDefinition> nebulae)
        {
            fieldScroll = GUILayout.BeginScrollView(fieldScroll, GUILayout.Height(400));

            if (selected < 0)
            {
                GUILayout.Label("No nebulae defined. Add a body on the left.");
                GUILayout.EndScrollView();
                return;
            }

            NebulaDefinition n = nebulae[selected];

            n.Draw = GUILayout.Toggle(n.Draw, "draw");
            n.Radius = Row("radius", n.Radius, 0f, 1e9f, 0f, 1e9f, false, true);
            n.Size = Row("size", n.Size, 1f, 1e5f, NebulaLimits.SizeMin, float.MaxValue, false, true);
            n.Brightness = Row("brightness", n.Brightness, 0f, 10f, 0f, NebulaLimits.BrightnessMax);
            n.ColorSaturation = Row("colorSaturation", n.ColorSaturation, 0f, 4f, 0f, NebulaLimits.ColorSaturationMax);
            n.StarTemperature = Row("starTemperature", n.StarTemperature, 2000f, 40000f, 1000f, NebulaLimits.StarTemperatureMax);
            n.StarBrightness = Row("starBrightness", n.StarBrightness, 0f, 20f, 0f, NebulaLimits.StarBrightnessMax);
            n.StarFalloff = Row("starFalloff", n.StarFalloff, 0.02f, 2f, NebulaLimits.StarFalloffMin, NebulaLimits.StarFalloffMax);
            n.RotationSpeed = Row("rotationSpeed", n.RotationSpeed, -20f, 20f, -NebulaLimits.RotationSpeedMax, NebulaLimits.RotationSpeedMax);
            n.Steps = Mathf.RoundToInt(Row("steps", n.Steps, 32f, 600f, 32f, NebulaLimits.StepsMax));

            Vector3 normal = n.Normal;
            normal.x = Row("normal x", normal.x, -20f, 20f, -NebulaLimits.NormalMax, NebulaLimits.NormalMax);
            normal.y = Row("normal y", normal.y, -20f, 20f, -NebulaLimits.NormalMax, NebulaLimits.NormalMax);
            normal.z = Row("normal z", normal.z, -20f, 20f, -NebulaLimits.NormalMax, NebulaLimits.NormalMax);
            n.Normal = normal;

            n.HideBody = GUILayout.Toggle(n.HideBody, "hideBody");
            n.Spin = GUILayout.Toggle(n.Spin < 0f, "reverse spin") ? -1f : 1f;

            GUILayout.Label("shape");
            n.Shape = GUILayout.Toolbar(n.Shape, new[] { "Sphere", "Ring", "Bipolar", "Spiral", "Lumpy" });
            n.ShapeRatio = Row("shapeRatio", n.ShapeRatio, 0.05f, 0.95f, NebulaLimits.ShapeRatioMin, NebulaLimits.ShapeRatioMax);
            n.ShapeArms = Row("shapeArms", n.ShapeArms, 1f, 8f, 1f, NebulaLimits.ShapeArmsMax);
            n.Irregularity = Row("irregularity", n.Irregularity, 0f, 1.5f, 0f, NebulaLimits.IrregularityMax);
            n.Variation = Row("variation", n.Variation, 0f, 1f);
            n.Seed = Row("seed", n.Seed, 0f, 100f, -NebulaLimits.SeedMax, NebulaLimits.SeedMax);

            n.Warp = Row("warp", n.Warp, 0f, 2f, 0f, NebulaLimits.GasWarpMax);
            n.Extent = Row("extent", n.Extent, 0.05f, 1f, NebulaLimits.GasExtentMin, NebulaLimits.GasExtentMax);
            n.Flatten = Row("flatten", n.Flatten, 0.05f, 1f, NebulaLimits.GasFlattenMin, 1f);
            n.Hollow = Row("hollow", n.Hollow, 0f, 0.9f, 0f, NebulaLimits.GasHollowMax);
            n.EdgeBlur = Row("edgeBlur", n.EdgeBlur, 0f, 1f);
            n.Cutoff = Row("cutoff", n.Cutoff, 0f, 2f, 0f, NebulaLimits.CutoffMax);

            n.GasCount = Mathf.RoundToInt(Row("gasTypes", n.GasCount, 1f, NebulaLimits.GasMax, 1f, NebulaLimits.GasMax));

            for (int i = 0; i < n.GasCount; i++)
            {
                NebulaGas gas = n.Gases[i];
                rowPrefix = "gas_" + (i + 1) + ".";

                GUILayout.Label("gas_" + (i + 1));

                gas.ColorR = Row("colorR", gas.ColorR, 0f, 1f, 0f, NebulaLimits.GasColorMax);
                gas.ColorG = Row("colorG", gas.ColorG, 0f, 1f, 0f, NebulaLimits.GasColorMax);
                gas.ColorB = Row("colorB", gas.ColorB, 0f, 1f, 0f, NebulaLimits.GasColorMax);
                gas.Density = Row("density", gas.Density, 0f, 3f, 0f, NebulaLimits.GasDensityMax);
                gas.Emission = Row("emission", gas.Emission, 0f, 10f, 0f, NebulaLimits.GasEmissionMax);
                gas.Absorption = Row("absorption", gas.Absorption, 0f, 5f, 0f, NebulaLimits.GasAbsorptionMax);
                gas.Scale = Row("scale", gas.Scale, 0.5f, 30f, NebulaLimits.GasScaleMin, float.MaxValue, true, true);
                gas.Coverage = Row("coverage", gas.Coverage, 0f, 0.95f);
                gas.Contrast = Row("contrast", gas.Contrast, 0f, 1f);
                gas.Phase = Row("phase", gas.Phase, -0.5f, 0.9f, -NebulaLimits.GasPhaseMax, NebulaLimits.GasPhaseMax);
                gas.Shadow = Row("shadow", gas.Shadow, 0f, 3f, 0f, NebulaLimits.GasShadowMax);
                gas.Seed = Row("seed", gas.Seed, 0f, 100f, -NebulaLimits.GasSeedMax, NebulaLimits.GasSeedMax);

                rowPrefix = "";
            }

            n.Clamp();

            GUILayout.EndScrollView();
        }

        private void DrawCometEditor(List<StellarEffects.Comets.CometDefinition> comets)
        {
            fieldScroll = GUILayout.BeginScrollView(fieldScroll, GUILayout.Height(400));

            if (selected < 0)
            {
                GUILayout.Label("No comets defined. Add a body on the left.");
                GUILayout.EndScrollView();
                return;
            }

            StellarEffects.Comets.CometDefinition c = comets[selected];

            c.Draw = GUILayout.Toggle(c.Draw, "draw");
            c.Radius = Row("radius", c.Radius, 0f, 1e9f, false);
            c.Brightness = Row("brightness", c.Brightness, 0f, 20f, 0f, CometLimits.BrightnessMax);
            c.ComaSize = Row("comaSize", c.ComaSize, 0.2f, 200f, CometLimits.ComaSizeMin, CometLimits.ComaSizeMax);
            c.ComaTemperature = Row("comaTemperature", c.ComaTemperature, 2000f, 100000f, 1000f, CometLimits.ComaTemperatureMax);
            c.ColorSaturation = Row("colorSaturation", c.ColorSaturation, 0f, 10f, 0f, CometLimits.ColorSaturationMax);
            c.Glow = Row("glow", c.Glow, 0f, 50f, 0f, CometLimits.GlowMax);
            c.FlowSpeed = Row("flowSpeed", c.FlowSpeed, 0f, 2000f, 0f, CometLimits.FlowSpeedMax);
            c.Steps = Mathf.RoundToInt(Row("steps", c.Steps, 32f, 4000f, 32f, CometLimits.StepsMax));

            Vector3 normal = c.Normal;
            normal.x = Row("normal x", normal.x, -1000f, 1000f, -CometLimits.NormalMax, CometLimits.NormalMax);
            normal.y = Row("normal y", normal.y, -1000f, 1000f, -CometLimits.NormalMax, CometLimits.NormalMax);
            normal.z = Row("normal z", normal.z, -1000f, 1000f, -CometLimits.NormalMax, CometLimits.NormalMax);
            c.Normal = normal;

            c.HideBody = GUILayout.Toggle(c.HideBody, "hideBody");
            c.AutoDirection = GUILayout.Toggle(c.AutoDirection, "autoDirection");

            DrawCometTail("ion", c.Ion, false);
            DrawCometTail("dust", c.Dust, true);

            c.Clamp();

            GUILayout.EndScrollView();
        }

        private void DrawCometTail(string name, CometTail tail, bool dust)
        {
            rowPrefix = name + ".";

            GUILayout.Label(name);

            tail.Brightness = Row("brightness", tail.Brightness, 0f, 20f, 0f, CometLimits.TailBrightnessMax);
            tail.Length = Row("length", tail.Length, 1f, 20000f, CometLimits.TailLengthMin, CometLimits.TailLengthMax);
            tail.Width = Row("width", tail.Width, 0.05f, 500f, CometLimits.TailWidthMin, CometLimits.TailWidthMax);
            tail.Spread = Row("spread", tail.Spread, 0f, 5f, 0f, CometLimits.TailSpreadMax);
            if (dust) tail.Curve = Row("curve", tail.Curve, 0f, 5f, 0f, CometLimits.TailCurveMax);
            tail.Temperature = Row("temperature", tail.Temperature, 2000f, 100000f, 1000f, CometLimits.TailTemperatureMax);
            tail.ColorSaturation = Row("colorSaturation", tail.ColorSaturation, 0f, 10f, 0f, CometLimits.ColorSaturationMax);
            tail.Density = Row("density", tail.Density, 0f, 20f, 0f, CometLimits.TailDensityMax);
            tail.Striation = Row("striation", tail.Striation, 0f, 2f, 0f, CometLimits.TailStriationMax);
            tail.Turbulence = Row("turbulence", tail.Turbulence, 0f, 10f, 0f, CometLimits.TailTurbulenceMax);
            tail.Decay = Row("decay", tail.Decay, 0.01f, 5f, CometLimits.TailDecayMin, CometLimits.TailDecayMax);
            tail.Seed = Row("seed", tail.Seed, 0f, 1000f, -CometLimits.TailSeedMax, CometLimits.TailSeedMax);

            rowPrefix = "";
        }

        private float Row(string label, float value, float min, float max, bool slider = true)
        {
            return Row(label, value, min, max, min, max, slider);
        }

        private float Row(string label, float value, float min, float max, float hardMin, float hardMax, bool slider = true)
        {
            return Row(label, value, min, max, hardMin, hardMax, slider, false);
        }

        private float Row(string label, float value, float min, float max, float hardMin, float hardMax, bool slider, bool unboundedText)
        {
            string key = rowPrefix + label;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));

            if (slider)
            {
                float shown = Mathf.Clamp(value, min, max);
                float moved = GUILayout.HorizontalSlider(shown, min, max, GUILayout.Width(SliderWidth));
                if (!Mathf.Approximately(moved, shown))
                {
                    value = moved;
                    buffers[key] = Format(value);
                }
            }

            bool focused = GUI.GetNameOfFocusedControl() == key;
            string text;
            if (!focused || !buffers.TryGetValue(key, out text))
                text = Format(value);

            GUI.SetNextControlName(key);
            string typed = GUILayout.TextField(text, GUILayout.Width(BoxWidth));
            if (typed != text)
            {
                buffers[key] = typed;
                float parsed;
                if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    value = Mathf.Clamp(parsed, hardMin, unboundedText ? float.MaxValue : hardMax);
            }

            GUILayout.EndHorizontal();
            return value;
        }

        private void Select(int index)
        {
            selected = index;
            confirmRemove = false;
            buffers.Clear();
            status = "";
        }

        private void AddComet()
        {
            StellarEffects.Comets.CometManager manager = StellarEffects.Comets.CometManager.Instance;
            string name = newBody.Trim();
            if (manager == null || name.Length == 0) return;

            if (manager.Comets.Exists(c => c.Body == name))
            {
                status = name + " already has a comet.";
                return;
            }

            if (FlightGlobals.Bodies == null || !FlightGlobals.Bodies.Exists(b => b.bodyName == name))
            {
                status = "No body named " + name + ".";
                return;
            }

            manager.Add(name);
            Select(manager.Comets.Count - 1);
            newBody = "";
            status = "Added " + name + ". Save to keep it.";
        }

        private void AddBody()
        {
            BlackHoleManager manager = BlackHoleManager.Instance;
            string name = newBody.Trim();
            if (manager == null || name.Length == 0) return;

            if (manager.Holes.Exists(h => h.Body == name))
            {
                status = name + " already has a black hole.";
                return;
            }

            if (FlightGlobals.Bodies == null || !FlightGlobals.Bodies.Exists(b => b.bodyName == name))
            {
                status = "No body named " + name + ".";
                return;
            }

            manager.Holes.Add(BlackHoleDefinition.CreateNew(name));
            Select(manager.Holes.Count - 1);
            newBody = "";
            status = "Added " + name + ". Save to keep it.";
        }

        private void AddQuasar()
        {
            QuasarManager manager = QuasarManager.Instance;
            string name = newBody.Trim();
            if (manager == null || name.Length == 0) return;

            if (manager.Quasars.Exists(h => h.Body == name))
            {
                status = name + " already has a quasar.";
                return;
            }

            if (FlightGlobals.Bodies == null || !FlightGlobals.Bodies.Exists(b => b.bodyName == name))
            {
                status = "No body named " + name + ".";
                return;
            }

            manager.Add(name);
            Select(manager.Quasars.Count - 1);
            newBody = "";
            status = "Added " + name + ". Save to keep it.";
        }

        private void AddNebula()
        {
            NebulaManager manager = NebulaManager.Instance;
            string name = newBody.Trim();
            if (manager == null || name.Length == 0) return;

            if (FlightGlobals.Bodies == null || !FlightGlobals.Bodies.Exists(b => b.bodyName == name))
            {
                status = "No body named " + name + ".";
                return;
            }

            manager.Add(name);
            Select(manager.Definitions.Count - 1);
            newBody = "";
            status = "Added " + name + ". Save to keep it.";
        }

        private void RemoveComet(StellarEffects.Comets.CometDefinition comet)
        {
            if (!confirmRemove)
            {
                confirmRemove = true;
                status = "Click again to remove " + comet.Body + " and delete its config entry.";
                return;
            }

            confirmRemove = false;

            try
            {
                CometConfigWriter.Delete(comet);
                StellarEffects.Comets.CometManager.Instance.Remove(comet);
                buffers.Clear();
                status = "Removed " + comet.Body + ".";
            }
            catch (System.Exception e)
            {
                status = "Remove failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not remove comet " + comet.Body + ": " + e);
            }
        }

        private void Remove(BlackHoleDefinition hole)
        {
            if (!confirmRemove)
            {
                confirmRemove = true;
                status = "Click again to remove " + hole.Body + " and delete its config entry.";
                return;
            }

            confirmRemove = false;

            try
            {
                BlackHoleConfigWriter.Delete(hole);
                BlackHoleManager.Instance.Remove(hole);
                buffers.Clear();
                status = "Removed " + hole.Body + ".";
            }
            catch (System.Exception e)
            {
                status = "Remove failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not remove " + hole.Body + ": " + e);
            }
        }

        private void RemoveQuasar(QuasarDefinition quasar)
        {
            if (!confirmRemove)
            {
                confirmRemove = true;
                status = "Click again to remove " + quasar.Body + " and delete its config entry.";
                return;
            }

            confirmRemove = false;

            try
            {
                QuasarConfigWriter.Delete(quasar);
                QuasarManager.Instance.Remove(quasar);
                buffers.Clear();
                status = "Removed " + quasar.Body + ".";
            }
            catch (System.Exception e)
            {
                status = "Remove failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not remove quasar " + quasar.Body + ": " + e);
            }
        }

        private void RemoveNebula(NebulaDefinition nebula)
        {
            if (!confirmRemove)
            {
                confirmRemove = true;
                status = "Click again to remove " + nebula.Body + " and delete its config entry.";
                return;
            }

            confirmRemove = false;

            try
            {
                NebulaConfigWriter.Delete(nebula);
                NebulaManager.Instance.Remove(nebula);
                buffers.Clear();
                status = "Removed " + nebula.Body + ".";
            }
            catch (System.Exception e)
            {
                status = "Remove failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not remove nebula " + nebula.Body + ": " + e);
            }
        }

        private void SaveComet(StellarEffects.Comets.CometDefinition comet)
        {
            confirmRemove = false;

            try
            {
                string path = CometConfigWriter.Save(comet);
                status = "Saved " + path.Replace(KSPUtil.ApplicationRootPath, "");
            }
            catch (System.Exception e)
            {
                status = "Save failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not save comet " + comet.Body + ": " + e);
            }
        }

        private void Save(BlackHoleDefinition hole)
        {
            confirmRemove = false;

            try
            {
                string path = BlackHoleConfigWriter.Save(hole);
                status = "Saved " + path.Replace(KSPUtil.ApplicationRootPath, "");
            }
            catch (System.Exception e)
            {
                status = "Save failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not save " + hole.Body + ": " + e);
            }
        }

        private void SaveQuasar(QuasarDefinition quasar)
        {
            confirmRemove = false;

            try
            {
                string path = QuasarConfigWriter.Save(quasar);
                status = "Saved " + path.Replace(KSPUtil.ApplicationRootPath, "");
            }
            catch (System.Exception e)
            {
                status = "Save failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not save quasar " + quasar.Body + ": " + e);
            }
        }

        private void SaveNebula(NebulaDefinition nebula)
        {
            confirmRemove = false;

            try
            {
                string path = NebulaConfigWriter.Save(nebula);
                status = "Saved " + path.Replace(KSPUtil.ApplicationRootPath, "");
            }
            catch (System.Exception e)
            {
                status = "Save failed: " + e.Message;
                Debug.LogError("[StellarEffects] Could not save nebula " + nebula.Body + ": " + e);
            }
        }

        private void ResetComet(StellarEffects.Comets.CometDefinition comet)
        {
            confirmRemove = false;
            comet.Revert();
            buffers.Clear();
            status = "Reset " + comet.Body + " to its saved config.";
        }

        private void Reset(BlackHoleDefinition hole)
        {
            confirmRemove = false;
            hole.Revert();
            buffers.Clear();
            status = "Reset " + hole.Body + " to its saved config.";
        }

        private void ResetQuasar(QuasarDefinition quasar)
        {
            confirmRemove = false;
            quasar.Revert();
            buffers.Clear();
            status = "Reset " + quasar.Body + " to its saved config.";
        }

        private void ResetNebula(NebulaDefinition nebula)
        {
            confirmRemove = false;
            nebula.Revert();
            buffers.Clear();
            status = "Reset " + nebula.Body + " to its saved config.";
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static void ReleaseLock()
        {
            InputLockManager.RemoveControlLock(LockId);
        }

        private static string ReadVersion()
        {
            var asm = typeof(SettingsWindow).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string v = info != null ? info.InformationalVersion : asm.GetName().Version.ToString(3);

            int plus = v.IndexOf('+');
            if (plus >= 0) v = v.Substring(0, plus);
            return v;
        }
    }
}