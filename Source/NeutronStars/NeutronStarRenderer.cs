using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.NeutronStars
{
    internal class NeutronStarRenderer : MonoBehaviour
    {
        private const float MinDistance = 1.5f;
        private const float BeamBase = 1f;
        private const float FieldMinShell = 1.5f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int CoreId = Shader.PropertyToID("_SE_Core");
        private static readonly int CoreColorId = Shader.PropertyToID("_SE_CoreColor");
        private static readonly int MagnetId = Shader.PropertyToID("_SE_Magnet");
        private static readonly int MagnetUId = Shader.PropertyToID("_SE_MagnetU");
        private static readonly int BeamId = Shader.PropertyToID("_SE_Beam");
        private static readonly int BeamBId = Shader.PropertyToID("_SE_BeamB");
        private static readonly int BeamColorId = Shader.PropertyToID("_SE_BeamColor");
        private static readonly int FieldId = Shader.PropertyToID("_SE_Field");
        private static readonly int FieldBId = Shader.PropertyToID("_SE_FieldB");
        private static readonly int FieldColorId = Shader.PropertyToID("_SE_FieldColor");

        public Shader Shader;
        public List<NeutronStarDefinition> Stars;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<NeutronStarDefinition, HiddenBody> hiddenBodies = new Dictionary<NeutronStarDefinition, HiddenBody>();

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
            if (hasFailed || Stars == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Neutron star rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Neutron Stars" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;
            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            foreach (NeutronStarDefinition star in Stars)
            {
                CelestialBody body = star.Target;
                if (body == null || body.scaledBody == null) continue;

                if (!star.Draw)
                {
                    SetHidden(star, false);
                    continue;
                }

                float coreBrightness = star.CoreBrightness > 0f ? star.CoreBrightness : 0f;
                float glare = star.Glare > 0f ? star.Glare : 0f;
                float beamBrightness = star.BeamBrightness > 0f && star.BeamLength > BeamBase ? star.BeamBrightness : 0f;
                float fieldBrightness = star.FieldBrightness > 0f && star.FieldExtent > FieldMinShell ? star.FieldBrightness : 0f;

                if (coreBrightness <= 0f && glare <= 0f && beamBrightness <= 0f && fieldBrightness <= 0f)
                {
                    SetHidden(star, false);
                    continue;
                }

                float scaledRadius = (star.Radius > 0f ? star.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                float effectBounds = EffectBounds(star, glare, beamBrightness, fieldBrightness);

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * scaledRadius
                    && ScreenRect(toCenter, effectBounds * scaledRadius, view, tanX, tanY, out screenRect);

                SetHidden(star, visible && star.HideBody);
                LogStatus(star, toCenter, scaledRadius, visible, time);

                if (!visible) continue;

                float surfaceDistance = Mathf.Max(toCenter.magnitude / scaledRadius - 1f, 0f);
                float steps = StellarQuality.Steps(star.Steps, surfaceDistance, effectBounds);

                Vector3 spinAxis = ((Vector3)(Planetarium.Rotation * (Vector3d)star.Normal)).normalized;
                Vector3 t = Vector3.Cross(spinAxis, Mathf.Abs(spinAxis.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                Vector3 bt = Vector3.Cross(spinAxis, t);

                float spinSign = star.Spin < 0f ? -1f : 1f;
                float phase = spinSign * (float)((star.SpinRate * time) % 1.0 * 2.0 * Mathf.PI);
                float tilt = star.Tilt * Mathf.Deg2Rad;

                Vector3 radial = t * Mathf.Cos(phase) + bt * Mathf.Sin(phase);
                Vector3 magnet = spinAxis * Mathf.Cos(tilt) + radial * Mathf.Sin(tilt);
                Vector3 magnetU = -t * Mathf.Sin(phase) + bt * Mathf.Cos(phase);

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, scaledRadius));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(CoreId, new Vector4(coreBrightness, star.PolarCap, glare, star.GlareSize));
                block.SetVector(CoreColorId, new Vector4(star.CoreColor.x, star.CoreColor.y, star.CoreColor.z, StellarQuality.NoiseDetail));
                block.SetVector(MagnetId, new Vector4(magnet.x, magnet.y, magnet.z, steps));
                block.SetVector(MagnetUId, new Vector4(magnetU.x, magnetU.y, magnetU.z, (float)(time % 65536.0)));
                block.SetVector(BeamId, new Vector4(beamBrightness, star.BeamLength, star.BeamWidth, star.BeamSpread));
                block.SetVector(BeamBId, new Vector4(star.Beaming, star.BeamTurbulence, 0f, 0f));
                block.SetVector(BeamColorId, new Vector4(star.BeamColor.x, star.BeamColor.y, star.BeamColor.z, 0f));
                block.SetVector(FieldId, new Vector4(fieldBrightness, star.FieldExtent, star.FieldShells, star.FieldWidth));
                block.SetVector(FieldBId, new Vector4(star.FieldAzimuth, 0f, 0f, 0f));
                block.SetVector(FieldColorId, new Vector4(star.FieldColor.x, star.FieldColor.y, star.FieldColor.z, 0f));

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

        private static float EffectBounds(NeutronStarDefinition star, float glare, float beamBrightness, float fieldBrightness)
        {
            float bounds = 4f;

            if (glare > 0f)
                bounds = Mathf.Max(bounds, star.GlareSize * 2.5f);

            if (fieldBrightness > 0f)
                bounds = Mathf.Max(bounds, star.FieldExtent * 1.05f);

            if (beamBrightness > 0f)
            {
                float halfWidth = (star.BeamWidth + star.BeamSpread * star.BeamLength) * 3f;
                bounds = Mathf.Max(bounds, star.BeamLength * 1.05f + halfWidth);
            }

            return bounds;
        }

        private void LogStatus(NeutronStarDefinition star, Vector3 toCenter, float scaledRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] Neutron star " + star.Body + ": " + (toCenter.magnitude / scaledRadius).ToString("0.0") + " radii away, visible=" + visible);
        }

        public void Release(NeutronStarDefinition star)
        {
            SetHidden(star, false);
            hiddenBodies.Remove(star);
        }

        private void SetHidden(NeutronStarDefinition star, bool hide)
        {
            if (!hiddenBodies.TryGetValue(star, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[star] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = star.Target.scaledBody.GetComponentsInChildren<Renderer>(true);
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