using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Quasars
{
    internal class QuasarRenderer : MonoBehaviour
    {
        private const float MinDistance = 1.5f;
        private const float JetBase = 1.5f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int CoreId = Shader.PropertyToID("_SE_Core");
        private static readonly int CoreBId = Shader.PropertyToID("_SE_CoreB");
        private static readonly int TorusId = Shader.PropertyToID("_SE_Torus");
        private static readonly int TorusBId = Shader.PropertyToID("_SE_TorusB");
        private static readonly int TorusCId = Shader.PropertyToID("_SE_TorusC");
        private static readonly int JetId = Shader.PropertyToID("_SE_Jet");
        private static readonly int JetBId = Shader.PropertyToID("_SE_JetB");
        private static readonly int JetCId = Shader.PropertyToID("_SE_JetC");

        public Shader Shader;
        public List<QuasarDefinition> Quasars;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<QuasarDefinition, HiddenBody> hiddenBodies = new Dictionary<QuasarDefinition, HiddenBody>();

        private Camera viewCamera;
        private Material material;
        private CommandBuffer commandBuffer;
        private bool isAttached;
        private bool hasFailed;
        private float nextStatusLog;

        private class HiddenBody
        {
            public Renderer[] Renderers;
            public bool[] WasEnabled;
            public bool IsHidden;
        }

        void Awake()
        {
            viewCamera = GetComponent<Camera>();
        }

        void OnPreRender()
        {
            if (hasFailed || Quasars == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Quasar rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Quasars" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;
            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            foreach (QuasarDefinition quasar in Quasars)
            {
                CelestialBody body = quasar.Target;
                if (body == null || body.scaledBody == null) continue;

                if (!quasar.Draw)
                {
                    SetHidden(quasar, false);
                    continue;
                }

                float coreBrightness = quasar.CoreSize > 0f && quasar.CoreBrightness > 0f ? quasar.CoreBrightness : 0f;
                float glare = quasar.Glare > 0f ? quasar.Glare : 0f;
                float torusDensity = quasar.TorusDensity > 0f && quasar.TorusOuter > quasar.TorusInner * 0.9f ? quasar.TorusDensity : 0f;
                float torusGlow = torusDensity > 0f && quasar.TorusGlow > 0f ? quasar.TorusGlow : 0f;
                float jetBrightness = quasar.Jets && quasar.JetBrightness > 0f && quasar.JetLength > JetBase && quasar.JetWidth > 0f ? quasar.JetBrightness : 0f;

                if (coreBrightness <= 0f && glare <= 0f && torusDensity <= 0f && jetBrightness <= 0f)
                {
                    SetHidden(quasar, false);
                    continue;
                }

                float scaledRadius = (quasar.Radius > 0f ? quasar.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                float effectBounds = EffectBounds(quasar, coreBrightness, glare, torusDensity, jetBrightness);

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * scaledRadius
                    && ScreenRect(toCenter, effectBounds * scaledRadius, view, tanX, tanY, out screenRect);

                SetHidden(quasar, visible && quasar.HideBody);
                LogStatus(quasar, toCenter, scaledRadius, visible, time);

                if (!visible) continue;

                float surfaceDistance = Mathf.Max(toCenter.magnitude / scaledRadius - 1f, 0f);
                float steps = StellarQuality.Steps(quasar.Steps, surfaceDistance, effectBounds);

                Vector3 normal = ((Vector3)(Planetarium.Rotation * (Vector3d)quasar.Normal)).normalized;
                float spinSign = quasar.Spin < 0f ? -1f : 1f;
                float rotation = torusDensity > 0f
                    ? spinSign * (float)(quasar.RotationSpeed * 0.1 * time % (2.0 * Mathf.PI))
                    : 0f;

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, scaledRadius));
                block.SetVector(NormalId, new Vector4(normal.x, normal.y, normal.z, spinSign));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(CoreId, new Vector4(coreBrightness, quasar.CoreSize, quasar.CoreTemperature, glare));
                block.SetVector(CoreBId, new Vector4(quasar.GlareSize, quasar.ColorSaturation, steps, (float)(time % 65536.0)));
                block.SetVector(TorusId, new Vector4(quasar.TorusInner, quasar.TorusOuter, quasar.TorusThickness, torusDensity));
                block.SetVector(TorusBId, new Vector4(quasar.TorusTemperature, torusGlow, quasar.TorusClumpiness, rotation));
                block.SetVector(TorusCId, new Vector4(quasar.TorusTurbulence, 0f, 0f, 0f));
                block.SetVector(JetId, new Vector4(jetBrightness, quasar.JetLength, quasar.JetWidth, quasar.JetTemperature));
                block.SetVector(JetBId, new Vector4(quasar.JetSpeed, 0f, quasar.JetSpread, quasar.JetTwist));
                block.SetVector(JetCId, new Vector4(quasar.JetPlumeStart, quasar.JetPlumeWidth, quasar.JetPlumeBoost, StellarQuality.NoiseDetail));

                commandBuffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6, 1, block);
                drawn++;
            }

            if (drawn > 0) viewCamera.depthTextureMode |= DepthTextureMode.Depth;

            if (!isAttached)
            {
                viewCamera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, commandBuffer);
                isAttached = true;
            }
        }

        private static float EffectBounds(QuasarDefinition quasar, float coreBrightness, float glare, float torusDensity, float jetBrightness)
        {
            float bounds = 0f;

            if (coreBrightness > 0f)
                bounds = Mathf.Max(quasar.CoreSize * 4f, 4f);

            if (torusDensity > 0f)
            {
                float torusReach = quasar.TorusOuter * Mathf.Sqrt(1f + 6.8f * quasar.TorusThickness * quasar.TorusThickness);
                bounds = Mathf.Max(bounds, torusReach * 1.05f);
            }

            if (glare > 0f)
                bounds = Mathf.Max(bounds, quasar.GlareSize * 2.5f);

            if (jetBrightness > 0f)
            {
                float jetHalfWidth = ((quasar.JetWidth + quasar.JetSpread * quasar.JetLength) * 3f + quasar.JetLength * quasar.JetPlumeWidth) * 2.2f;
                bounds = Mathf.Max(bounds, quasar.JetLength * 1.05f + jetHalfWidth);
            }

            return bounds;
        }

        private void LogStatus(QuasarDefinition quasar, Vector3 toCenter, float scaledRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] Quasar " + quasar.Body + ": " + (toCenter.magnitude / scaledRadius).ToString("0.0") + " radii away, visible=" + visible);
        }

        public void Release(QuasarDefinition quasar)
        {
            SetHidden(quasar, false);
            hiddenBodies.Remove(quasar);
        }

        private void SetHidden(QuasarDefinition quasar, bool hide)
        {
            if (!hiddenBodies.TryGetValue(quasar, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[quasar] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = quasar.Target.scaledBody.GetComponentsInChildren<Renderer>(true);
                    state.WasEnabled = new bool[state.Renderers.Length];
                }

                if (!state.IsHidden)
                {
                    for (int i = 0; i < state.Renderers.Length; i++)
                        if (state.Renderers[i] != null) state.WasEnabled[i] = state.Renderers[i].enabled;

                    state.IsHidden = true;
                }

                for (int i = 0; i < state.Renderers.Length; i++)
                    if (state.Renderers[i] != null) state.Renderers[i].enabled = false;
            }
            else if (state.IsHidden)
            {
                for (int i = 0; i < state.Renderers.Length; i++)
                    if (state.Renderers[i] != null) state.Renderers[i].enabled = state.WasEnabled[i];

                state.IsHidden = false;
                state.Renderers = null;
            }
        }

        private static bool ScreenRect(Vector3 toCenter, float radius, Transform view, float tanX, float tanY, out Vector4 screenRect)
        {
            const float margin = 0.02f;

            screenRect = new Vector4(-1f, -1f, 1f, 1f);
            if (toCenter.sqrMagnitude <= radius * radius * 1.0201f) return true;

            float depth = Vector3.Dot(toCenter, view.forward);
            if (!AxisRange(Vector3.Dot(toCenter, view.right), depth, radius, tanX, out float x0, out float x1)) return false;
            if (!AxisRange(Vector3.Dot(toCenter, view.up), depth, radius, tanY, out float y0, out float y1)) return false;

            screenRect = new Vector4(Mathf.Max(-1f, x0 - margin), Mathf.Max(-1f, y0 - margin), Mathf.Min(1f, x1 + margin), Mathf.Min(1f, y1 + margin));
            return true;
        }

        private static bool AxisRange(float offset, float depth, float radius, float tanHalfFov, out float start, out float end)
        {
            start = -1f;
            end = 1f;

            float distance = Mathf.Sqrt(offset * offset + depth * depth);
            if (distance <= radius) return true;

            float centerAngle = Mathf.Atan2(offset, depth);
            float halfAngle = Mathf.Asin(radius / distance);
            float angleStart = centerAngle - halfAngle;
            float angleEnd = centerAngle + halfAngle;
            const float halfPi = Mathf.PI * 0.5f;

            if (angleStart >= halfPi || angleEnd <= -halfPi) return false;
            if (angleStart > -halfPi) start = Mathf.Max(-1f, Mathf.Tan(angleStart) / tanHalfFov);
            if (angleEnd < halfPi) end = Mathf.Min(1f, Mathf.Tan(angleEnd) / tanHalfFov);
            return start < end;
        }

        private void RestoreBodies()
        {
            foreach (var pair in hiddenBodies)
            {
                HiddenBody state = pair.Value;
                if (!state.IsHidden || state.Renderers == null) continue;

                for (int i = 0; i < state.Renderers.Length; i++)
                    if (state.Renderers[i] != null) state.Renderers[i].enabled = state.WasEnabled[i];

                state.IsHidden = false;
                state.Renderers = null;
            }
        }

        private void Detach()
        {
            if (isAttached && viewCamera != null && commandBuffer != null)
                viewCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, commandBuffer);

            isAttached = false;
            RestoreBodies();
        }

        void OnDisable()
        {
            RestoreBodies();
        }

        void OnDestroy()
        {
            Detach();

            if (commandBuffer != null) commandBuffer.Release();
            if (material != null) Destroy(material);
        }
    }
}