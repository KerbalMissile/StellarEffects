using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Comets
{
    internal class CometRenderer : MonoBehaviour
    {
        private const float MinDistance = 1.5f;
        private const float BendReach = 0.25f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int ComaId = Shader.PropertyToID("_SE_Coma");
        private static readonly int ComaBId = Shader.PropertyToID("_SE_ComaB");
        private static readonly int IonId = Shader.PropertyToID("_SE_Ion");
        private static readonly int IonBId = Shader.PropertyToID("_SE_IonB");
        private static readonly int IonCId = Shader.PropertyToID("_SE_IonC");
        private static readonly int DustId = Shader.PropertyToID("_SE_Dust");
        private static readonly int DustBId = Shader.PropertyToID("_SE_DustB");
        private static readonly int DustCId = Shader.PropertyToID("_SE_DustC");
        private static readonly int BendId = Shader.PropertyToID("_SE_Bend");
        private static readonly int LookId = Shader.PropertyToID("_SE_Look");

        public Shader Shader;
        public List<CometDefinition> Comets;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<CometDefinition, HiddenBody> hiddenBodies = new Dictionary<CometDefinition, HiddenBody>();

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
            if (hasFailed || Comets == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Comet rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Comets" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;
            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            CelestialBody sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;

            foreach (CometDefinition comet in Comets)
            {
                CelestialBody body = comet.Target;
                if (body == null || body.scaledBody == null) continue;

                if (!comet.Draw)
                {
                    SetHidden(comet, false);
                    continue;
                }

                float ionBrightness = comet.Ion.Brightness > 0f && comet.Ion.Length > comet.Ion.Width ? comet.Ion.Brightness : 0f;
                float dustBrightness = comet.Dust.Brightness > 0f && comet.Dust.Length > comet.Dust.Width ? comet.Dust.Brightness : 0f;
                bool comaOn = comet.Brightness > 0f && comet.ComaSize > 0f;

                if (!comaOn && ionBrightness <= 0f && dustBrightness <= 0f)
                {
                    SetHidden(comet, false);
                    continue;
                }

                float scaledRadius = (comet.Radius > 0f ? comet.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                Vector3 sunDirection = Vector3.zero;
                if (sun != null && sun.scaledBody != null)
                    sunDirection = (sun.scaledBody.transform.position - body.scaledBody.transform.position).normalized;

                Vector3 normal;
                if (comet.AutoDirection && sunDirection.sqrMagnitude > 0.0001f)
                    normal = sunDirection;
                else
                    normal = ((Vector3)(Planetarium.Rotation * (Vector3d)comet.Normal)).normalized;

                if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;

                Vector3 bend = body.orbit != null ? Vector3.ProjectOnPlane(-(Vector3)body.orbit.GetVel(), normal) : Vector3.zero;
                if (bend.sqrMagnitude < 0.0001f) bend = Vector3.ProjectOnPlane(Vector3.Cross(normal, sunDirection), normal);
                if (bend.sqrMagnitude < 0.0001f) bend = Vector3.Cross(normal, Vector3.up);
                bend.Normalize();

                float effectBounds = EffectBounds(comet);

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * scaledRadius
                    && ScreenRect(toCenter, effectBounds * scaledRadius, view, tanX, tanY, out screenRect);

                SetHidden(comet, visible && comet.HideBody);
                LogStatus(comet, toCenter, scaledRadius, visible, time);

                if (!visible) continue;

                float surfaceDistance = Mathf.Max(toCenter.magnitude / scaledRadius - 1f, 0f);
                float steps = StellarQuality.Steps(comet.Steps, surfaceDistance, effectBounds);

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, scaledRadius));
                block.SetVector(NormalId, new Vector4(normal.x, normal.y, normal.z, 1f));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(ComaId, new Vector4(comaOn ? 1f : 0f, comet.ComaSize, comet.ComaTemperature, 0f));
                block.SetVector(ComaBId, new Vector4(comet.ColorSaturation, comet.Glow, steps, StellarQuality.NoiseDetail));
                block.SetVector(IonId, new Vector4(ionBrightness, comet.Ion.Length, comet.Ion.Width, comet.Ion.Spread));
                block.SetVector(IonBId, new Vector4(comet.Ion.Temperature, comet.Ion.ColorSaturation, comet.Ion.Density, comet.Ion.Decay));
                block.SetVector(IonCId, new Vector4(comet.Ion.Striation, comet.Ion.Turbulence, comet.Ion.Seed, 0f));
                block.SetVector(DustId, new Vector4(dustBrightness, comet.Dust.Length, comet.Dust.Width, comet.Dust.Spread));
                block.SetVector(DustBId, new Vector4(comet.Dust.Temperature, comet.Dust.ColorSaturation, comet.Dust.Density, comet.Dust.Decay));
                block.SetVector(DustCId, new Vector4(comet.Dust.Curve, comet.Dust.Striation, comet.Dust.Turbulence, comet.Dust.Seed));
                block.SetVector(BendId, bend);
                block.SetVector(LookId, new Vector4(comet.Brightness, comet.FlowSpeed, 0f, (float)(time % 65536.0)));

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

        private static float EffectBounds(CometDefinition comet)
        {
            float bounds = comet.ComaSize * 4f;

            if (comet.Ion.Brightness > 0f)
                bounds = Mathf.Max(bounds, comet.Ion.Length + comet.Ion.Width + comet.Ion.Spread * comet.Ion.Length);

            if (comet.Dust.Brightness > 0f)
            {
                float reach = comet.Dust.Length + comet.Dust.Width + comet.Dust.Spread * comet.Dust.Length;
                reach += comet.Dust.Curve * comet.Dust.Length * BendReach;
                bounds = Mathf.Max(bounds, reach);
            }

            return Mathf.Max(bounds, 4f);
        }

        private void LogStatus(CometDefinition comet, Vector3 toCenter, float scaledRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] Comet " + comet.Body + ": " + (toCenter.magnitude / scaledRadius).ToString("0.0") + " radii away, visible=" + visible);
        }

        public void Release(CometDefinition comet)
        {
            SetHidden(comet, false);
            hiddenBodies.Remove(comet);
        }

        private void SetHidden(CometDefinition comet, bool hide)
        {
            if (!hiddenBodies.TryGetValue(comet, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[comet] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = comet.Target.scaledBody.GetComponentsInChildren<Renderer>(true);
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
