using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Evaporation
{
    internal class EvaporationRenderer : MonoBehaviour
    {
        private const float MinDistance = 1.5f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int ComaId = Shader.PropertyToID("_SE_Coma");
        private static readonly int ComaColorId = Shader.PropertyToID("_SE_ComaColor");
        private static readonly int TailId = Shader.PropertyToID("_SE_Tail");
        private static readonly int TailBId = Shader.PropertyToID("_SE_TailB");
        private static readonly int TailCId = Shader.PropertyToID("_SE_TailC");
        private static readonly int TailColorId = Shader.PropertyToID("_SE_TailColor");
        private static readonly int BendId = Shader.PropertyToID("_SE_Bend");
        private static readonly int LookId = Shader.PropertyToID("_SE_Look");

        public Shader Shader;
        public List<EvaporationDefinition> Planets;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<EvaporationDefinition, HiddenBody> hiddenBodies = new Dictionary<EvaporationDefinition, HiddenBody>();

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
            if (hasFailed || Planets == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Evaporation rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Evaporation" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;
            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            foreach (EvaporationDefinition planet in Planets)
            {
                CelestialBody body = planet.Target;
                if (body == null || body.scaledBody == null) continue;

                if (!planet.Draw)
                {
                    SetHidden(planet, false);
                    continue;
                }

                float tailBrightness = planet.TailBrightness > 0f && planet.TailLength > planet.TailWidth ? planet.TailBrightness : 0f;
                bool comaOn = planet.ComaSize > 0f;

                if (planet.Brightness <= 0f || (!comaOn && tailBrightness <= 0f))
                {
                    SetHidden(planet, false);
                    continue;
                }

                float scaledRadius = (planet.Radius > 0f ? planet.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                Vector3 toParent = Vector3.zero;
                CelestialBody parent = body.referenceBody;
                if (parent != null && parent != body && parent.scaledBody != null)
                    toParent = parent.scaledBody.transform.position - body.scaledBody.transform.position;

                Vector3 axis = Vector3.zero;
                if (body.orbit != null)
                    axis = -(Vector3)(Planetarium.Rotation * body.orbit.GetVel());

                if (axis.sqrMagnitude < 1e-10f)
                    axis = toParent.sqrMagnitude > 1e-10f ? Vector3.Cross(toParent, Vector3.up) : Vector3.up;

                if (axis.sqrMagnitude < 1e-10f) axis = Vector3.right;
                axis.Normalize();

                if (planet.TailYaw != 0f || planet.TailPitch != 0f)
                {
                    Vector3 orbitNormal = Vector3.Cross(toParent, axis);
                    if (orbitNormal.sqrMagnitude < 1e-10f) orbitNormal = Vector3.up;
                    orbitNormal.Normalize();

                    axis = Quaternion.AngleAxis(planet.TailYaw, orbitNormal) * axis;
                    Vector3 side = Vector3.Cross(orbitNormal, axis);
                    axis = (Quaternion.AngleAxis(planet.TailPitch, side) * axis).normalized;
                }

                Vector3 bend = Vector3.zero;
                float curvature = 0f;
                Vector3 lateral = Vector3.ProjectOnPlane(toParent, axis);
                if (planet.TailCurve > 0f && lateral.sqrMagnitude > 1e-10f)
                {
                    bend = lateral.normalized;
                    float orbitRadius = toParent.magnitude / scaledRadius;
                    curvature = planet.TailCurve / (2f * orbitRadius);
                }

                float effectBounds = EffectBounds(planet, tailBrightness, curvature);

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * scaledRadius
                    && ScreenRect(toCenter, effectBounds * scaledRadius, view, tanX, tanY, out screenRect);

                SetHidden(planet, visible && planet.HideBody);
                LogStatus(planet, toCenter, scaledRadius, visible, time);

                if (!visible) continue;

                float surfaceDistance = Mathf.Max(toCenter.magnitude / scaledRadius - 1f, 0f);
                float steps = StellarQuality.Steps(planet.Steps, surfaceDistance, effectBounds);

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, scaledRadius));
                block.SetVector(NormalId, new Vector4(axis.x, axis.y, axis.z, 1f));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(ComaId, new Vector4(comaOn ? 1f : 0f, planet.ComaSize, planet.Glow, steps));
                block.SetVector(ComaColorId, new Vector4(planet.ComaColor.x, planet.ComaColor.y, planet.ComaColor.z, StellarQuality.NoiseDetail));
                block.SetVector(TailId, new Vector4(tailBrightness, planet.TailLength, planet.TailWidth, planet.TailSpread));
                block.SetVector(TailBId, new Vector4(planet.TailDensity, planet.TailDecay, planet.TailStriation, planet.TailTurbulence));
                block.SetVector(TailCId, new Vector4(planet.TailSeed, 0f, 0f, 0f));
                block.SetVector(TailColorId, new Vector4(planet.TailColor.x, planet.TailColor.y, planet.TailColor.z, 0f));
                block.SetVector(BendId, new Vector4(bend.x, bend.y, bend.z, curvature));
                block.SetVector(LookId, new Vector4(planet.Brightness, planet.FlowSpeed, 0f, (float)(time % 65536.0)));

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

        private static float EffectBounds(EvaporationDefinition planet, float tailBrightness, float curvature)
        {
            float bounds = planet.ComaSize * 4f;

            if (tailBrightness > 0f)
            {
                float reach = planet.TailLength + planet.TailWidth + planet.TailSpread * planet.TailLength;
                reach += curvature * planet.TailLength * planet.TailLength;
                bounds = Mathf.Max(bounds, reach);
            }

            return Mathf.Max(bounds, 4f);
        }

        private void LogStatus(EvaporationDefinition planet, Vector3 toCenter, float scaledRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] Evaporation " + planet.Body + ": " + (toCenter.magnitude / scaledRadius).ToString("0.0") + " radii away, visible=" + visible);
        }

        public void Release(EvaporationDefinition planet)
        {
            SetHidden(planet, false);
            hiddenBodies.Remove(planet);
        }

        private void SetHidden(EvaporationDefinition planet, bool hide)
        {
            if (!hiddenBodies.TryGetValue(planet, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[planet] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = planet.Target.scaledBody.GetComponentsInChildren<Renderer>(true);
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