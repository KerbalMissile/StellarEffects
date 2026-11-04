using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Blackholes
{
    internal class BlackHoleRenderer : MonoBehaviour
    {
        private const float SpinRate = 0.75f;
        private const float MinDistance = 1.5f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int DiskId = Shader.PropertyToID("_SE_Disk");
        private static readonly int DiskBId = Shader.PropertyToID("_SE_DiskB");
        private static readonly int LookId = Shader.PropertyToID("_SE_Look");
        private static readonly int ExtraId = Shader.PropertyToID("_SE_Extra");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int JetId = Shader.PropertyToID("_SE_Jet");
        private static readonly int JetBId = Shader.PropertyToID("_SE_JetB");

        public Shader Shader;
        public List<BlackHoleDefinition> Holes;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<BlackHoleDefinition, HiddenBody> hiddenBodies = new Dictionary<BlackHoleDefinition, HiddenBody>();

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
            if (hasFailed || Holes == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Black hole rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Black Holes" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;

            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            foreach (BlackHoleDefinition hole in Holes)
            {
                CelestialBody body = hole.Target;
                if (body == null || body.scaledBody == null) continue;

                float horizonRadius = (hole.HorizonRadius > 0f ? hole.HorizonRadius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                float jetBrightness = hole.Jets ? hole.JetBrightness : 0f;
                float effectBounds = EffectBounds(hole, jetBrightness);

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * horizonRadius
                    && ScreenRect(toCenter, effectBounds * horizonRadius, view, tanX, tanY, out screenRect);

                SetHidden(hole, visible && hole.HideBody);
                LogStatus(hole, toCenter, horizonRadius, visible, time);

                if (!visible) continue;

                float surfaceDistance = Mathf.Max(toCenter.magnitude / horizonRadius - 1f, 0f);
                float steps = StellarQuality.Steps(hole.Steps, surfaceDistance, effectBounds);

                Vector3 normal = ((Vector3)(Planetarium.Rotation * (Vector3d)hole.Normal)).normalized;
                float spinSign = hole.Spin < 0f ? -1f : 1f;
                float rotation = spinSign * (float)(hole.RotationSpeed * SpinRate * time % (2.0 * Mathf.PI));

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, horizonRadius));
                block.SetVector(NormalId, new Vector4(normal.x, normal.y, normal.z, spinSign));
                block.SetVector(DiskId, new Vector4(hole.DiskInner, hole.DiskOuter, hole.DiskThickness, hole.Turbulence));
                block.SetVector(DiskBId, new Vector4(hole.Twirl, StellarQuality.NoiseDetail, StellarQuality.MarchScale, 0f));
                block.SetVector(LookId, new Vector4(hole.Temperature, hole.Brightness, rotation, hole.Debug));
                block.SetVector(ExtraId, new Vector4(hole.ColorSaturation, hole.Glow, hole.PhotonRing, steps));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(JetId, new Vector4(jetBrightness, hole.JetLength, hole.JetWidth, hole.JetTemperature));
                block.SetVector(JetBId, new Vector4(hole.JetSpeed, (float)(time % 65536.0), hole.JetSpread, hole.JetTwist));

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

        private static float EffectBounds(BlackHoleDefinition hole, float jetBrightness)
        {
            float bounds = Mathf.Max(hole.DiskOuter * 1.2f, 4f);
            if (jetBrightness > 0f)
            {
                float jetHalfWidth = hole.JetWidth + hole.JetSpread * hole.JetLength;
                bounds = Mathf.Max(bounds, hole.JetLength * 1.05f + jetHalfWidth);
            }

            return bounds;
        }

        private void LogStatus(BlackHoleDefinition hole, Vector3 toCenter, float horizonRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] " + hole.Body + ": " + (toCenter.magnitude / horizonRadius).ToString("0.0") + " horizon radii away, visible=" + visible);
        }

        public void Release(BlackHoleDefinition hole)
        {
            SetHidden(hole, false);
            hiddenBodies.Remove(hole);
        }

        private void SetHidden(BlackHoleDefinition hole, bool hide)
        {
            if (!hiddenBodies.TryGetValue(hole, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[hole] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = hole.Target.scaledBody.GetComponentsInChildren<Renderer>(true);
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