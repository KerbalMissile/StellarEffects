using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Auroras
{
    internal class AuroraRenderer : MonoBehaviour
    {
        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int LookId = Shader.PropertyToID("_SE_Look");
        private static readonly int ShellId = Shader.PropertyToID("_SE_Shell");
        private static readonly int RingId = Shader.PropertyToID("_SE_Ring");
        private static readonly int BandId = Shader.PropertyToID("_SE_Band");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int LowId = Shader.PropertyToID("_SE_Low");
        private static readonly int HighId = Shader.PropertyToID("_SE_High");
        private static readonly int BaseId = Shader.PropertyToID("_SE_Base");

        public Shader Shader;
        public List<AuroraDefinition> Auroras;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();

        private Camera viewCamera;
        private Material material;
        private CommandBuffer commandBuffer;
        private bool isAttached;
        private bool hasFailed;

        void Awake()
        {
            viewCamera = GetComponent<Camera>();
        }

        void OnPreRender()
        {
            if (hasFailed || Auroras == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Aurora rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null) material = new Material(Shader);
            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Auroras" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;

            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            int drawn = 0;

            foreach (AuroraDefinition aurora in Auroras)
            {
                CelestialBody body = aurora.Target;
                if (body == null || body.scaledBody == null || !aurora.Draw) continue;

                float radius = (aurora.Radius > 0f ? aurora.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;
                if (toCenter.magnitude < radius * 0.99f) continue;

                float bound = 1f + aurora.Altitude + aurora.Height + 0.02f;

                Vector4 screenRect;
                if (!ScreenRect(toCenter, bound * radius, view, tanX, tanY, out screenRect)) continue;

                float colat = (90f - aurora.Latitude) * Mathf.Deg2Rad;
                float sigma = aurora.Width * Mathf.Deg2Rad;
                float wobble = aurora.Wobble * Mathf.Deg2Rad;
                float margin = 4f * sigma + wobble;
                float cosMin = Mathf.Cos(Mathf.Min(colat + margin, Mathf.PI * 0.5f));
                float cosMax = Mathf.Cos(Mathf.Max(colat - margin, 0f));

                Vector3 normal = ((Vector3)(Planetarium.Rotation * (Vector3d)aurora.Normal)).normalized;
                float rotation = (float)(aurora.FlowSpeed * time % (2.0 * Mathf.PI));
                float shimmer = (float)(time * aurora.Pulse * 0.4 % 100000.0);
                float steps = Mathf.Max(24f, aurora.Steps / StellarQuality.MarchScale);

                if (propertyBlocks.Count <= drawn) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[drawn];

                block.SetVector(RectId, screenRect);
                block.SetVector(CenterId, new Vector4(toCenter.x, toCenter.y, toCenter.z, radius));
                block.SetVector(NormalId, new Vector4(normal.x, normal.y, normal.z, 0f));
                block.SetVector(LookId, new Vector4(aurora.Brightness, rotation, shimmer, aurora.RayScale));
                block.SetVector(ShellId, new Vector4(aurora.Altitude, aurora.Height, aurora.RayDrop, steps));
                block.SetVector(RingId, new Vector4(colat, Mathf.Max(sigma, 1e-3f), wobble, aurora.RayContrast));
                block.SetVector(BandId, new Vector4(cosMin, cosMax, aurora.Seed, StellarQuality.NoiseDetail));
                block.SetVector(RayId, new Vector4(tanX, tanY, 0f, 1f));
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(LowId, new Vector4(aurora.LowColor.x, aurora.LowColor.y, aurora.LowColor.z, 0f));
                block.SetVector(HighId, new Vector4(aurora.HighColor.x, aurora.HighColor.y, aurora.HighColor.z, 0f));
                block.SetVector(BaseId, new Vector4(aurora.BaseColor.x, aurora.BaseColor.y, aurora.BaseColor.z, 0f));

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

        private void Detach()
        {
            if (isAttached && viewCamera != null && commandBuffer != null)
                viewCamera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, commandBuffer);

            isAttached = false;
        }

        void OnDestroy()
        {
            Detach();

            if (commandBuffer != null) commandBuffer.Release();
            if (material != null) Destroy(material);
        }
    }
}