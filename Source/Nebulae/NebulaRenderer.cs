using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StellarEffects.Nebulae
{
    internal class NebulaRenderer : MonoBehaviour
    {
        private const float MinDistance = 1.5f;
        private const int NoiseSize = 96;
        private const int BakeSize = 96;
        private const int DivisorNormal = 2;
        private const int DivisorLarge = 3;
        private const float LargeCoverageOn = 0.6f;
        private const float LargeCoverageOff = 0.4f;

        private static readonly int RectId = Shader.PropertyToID("_SE_Rect");
        private static readonly int CenterId = Shader.PropertyToID("_SE_Center");
        private static readonly int NormalId = Shader.PropertyToID("_SE_Normal");
        private static readonly int RayId = Shader.PropertyToID("_SE_Ray");
        private static readonly int OccId = Shader.PropertyToID("_SE_Occ");
        private static readonly int RightId = Shader.PropertyToID("_SE_Right");
        private static readonly int UpId = Shader.PropertyToID("_SE_Up");
        private static readonly int ForwardId = Shader.PropertyToID("_SE_Forward");
        private static readonly int LookId = Shader.PropertyToID("_SE_Look");
        private static readonly int StarId = Shader.PropertyToID("_SE_Star");
        private static readonly int QualityId = Shader.PropertyToID("_SE_Quality");
        private static readonly int ShapeId = Shader.PropertyToID("_SE_Shape");
        private static readonly int EdgeId = Shader.PropertyToID("_SE_Edge");
        private static readonly int BakeId = Shader.PropertyToID("_SE_Bake");
        private static readonly int LowId = Shader.PropertyToID("_SE_Low");
        private static readonly int GasAId = Shader.PropertyToID("_SE_GasA");
        private static readonly int GasBId = Shader.PropertyToID("_SE_GasB");
        private static readonly int GasCId = Shader.PropertyToID("_SE_GasC");
        private static readonly int GasDId = Shader.PropertyToID("_SE_GasD");
        private static readonly int NoiseId = Shader.PropertyToID("_SE_NoiseTex");
        private static readonly int BakeAId = Shader.PropertyToID("_SE_BakeA");
        private static readonly int BakeBId = Shader.PropertyToID("_SE_BakeB");
        private static readonly int LowColId = Shader.PropertyToID("_SE_LowCol");
        private static readonly int LowInfoId = Shader.PropertyToID("_SE_LowInfo");

        public Shader Shader;
        public List<NebulaDefinition> Definitions;

        private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
        private readonly Dictionary<CelestialBody, HiddenBody> hiddenBodies = new Dictionary<CelestialBody, HiddenBody>();
        private readonly HashSet<CelestialBody> seenBodies = new HashSet<CelestialBody>();
        private readonly HashSet<CelestialBody> hideBodies = new HashSet<CelestialBody>();
        private readonly Dictionary<NebulaDefinition, BakeState> bakes = new Dictionary<NebulaDefinition, BakeState>();
        private readonly List<Visible> visibleList = new List<Visible>();
        private readonly RenderTargetIdentifier[] lowTargets = new RenderTargetIdentifier[2];
        private readonly Vector4[] gasA = new Vector4[NebulaLimits.GasMax];
        private readonly Vector4[] gasB = new Vector4[NebulaLimits.GasMax];
        private readonly Vector4[] gasC = new Vector4[NebulaLimits.GasMax];
        private readonly Vector4[] gasD = new Vector4[NebulaLimits.GasMax];

        private Camera viewCamera;
        private Material material;
        private CommandBuffer commandBuffer;
        private Texture3D noiseTexture;
        private RenderTexture lowColor;
        private RenderTexture lowInfo;
        private RenderTexture lowDepth;
        private int lowWidth;
        private int lowHeight;
        private int lowDivisor = DivisorNormal;
        private bool isAttached;
        private bool hasFailed;
        private float nextStatusLog;

        private class HiddenBody
        {
            public Renderer[] Renderers;
            public bool[] WasEnabled;
            public bool IsHidden;
        }

        private class BakeState
        {
            public RenderTexture A;
            public RenderTexture B;
            public int Hash;
            public bool HasHash;

            public bool IsReady
            {
                get { return A != null && B != null && A.IsCreated() && B.IsCreated(); }
            }

            public void Free()
            {
                if (A != null) { A.Release(); Destroy(A); A = null; }
                if (B != null) { B.Release(); Destroy(B); B = null; }
                HasHash = false;
            }
        }

        private struct Visible
        {
            public NebulaDefinition Nebula;
            public Vector3 ToCenter;
            public Vector4 Rect;
            public float ScaledRadius;
            public float Bound;
            public float Brightness;
        }

        void Awake()
        {
            viewCamera = GetComponent<Camera>();
        }

        void OnPreRender()
        {
            if (hasFailed || Definitions == null || Shader == null) return;

            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                hasFailed = true;
                Detach();
                Debug.LogError("[StellarEffects] Nebula rendering failed: " + e);
            }
        }

        private void Build()
        {
            if (material == null)
            {
                material = new Material(Shader);
                if (noiseTexture == null) noiseTexture = CreateNoiseTexture();
                material.SetTexture(NoiseId, noiseTexture);
            }

            if (commandBuffer == null) commandBuffer = new CommandBuffer { name = "StellarEffects Nebulae" };
            commandBuffer.Clear();

            Transform view = viewCamera.transform;
            float tanY = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanX = tanY * viewCamera.aspect;
            float inverseScale = (float)ScaledSpace.InverseScaleFactor;
            double time = Time.unscaledTime;
            float maxCoverage = 0f;

            CelestialBody mainBody = FlightGlobals.currentMainBody;
            Vector4 mainOccluder = Vector4.zero;
            if (mainBody != null && mainBody.scaledBody != null)
            {
                Vector3 toMain = mainBody.scaledBody.transform.position - view.position;
                mainOccluder = new Vector4(toMain.x, toMain.y, toMain.z, (float)mainBody.Radius * inverseScale);
            }

            visibleList.Clear();
            seenBodies.Clear();
            hideBodies.Clear();

            foreach (NebulaDefinition nebula in Definitions)
            {
                CelestialBody body = nebula.Target;
                if (body == null || body.scaledBody == null) continue;

                float brightness = nebula.Brightness > 0f && nebula.GasCount > 0 ? nebula.Brightness : 0f;

                if (brightness <= 0f)
                {
                    seenBodies.Add(body);
                    continue;
                }

                float scaledRadius = (nebula.Radius > 0f ? nebula.Radius : (float)body.Radius) * inverseScale;
                Vector3 toCenter = body.scaledBody.transform.position - view.position;

                float bound = EffectExtent(nebula) * nebula.Size * scaledRadius;

                Vector4 screenRect = Vector4.zero;
                bool visible = toCenter.magnitude > MinDistance * scaledRadius
                    && ScreenRect(toCenter, bound, view, tanX, tanY, out screenRect);

                seenBodies.Add(body);
                if (visible && nebula.HideBody) hideBodies.Add(body);
                LogStatus(nebula, toCenter, scaledRadius, visible, time);

                if (!visible) continue;

                float coverage = (screenRect.z - screenRect.x) * (screenRect.w - screenRect.y) * 0.25f;
                if (coverage > maxCoverage) maxCoverage = coverage;

                visibleList.Add(new Visible
                {
                    Nebula = nebula,
                    ToCenter = toCenter,
                    Rect = screenRect,
                    ScaledRadius = scaledRadius,
                    Bound = bound,
                    Brightness = brightness
                });
            }

            foreach (CelestialBody seen in seenBodies)
                SetHidden(seen, hideBodies.Contains(seen));

            if (visibleList.Count > 0)
            {
                UpdateDivisor(maxCoverage);
                EnsureLowResolution();
                viewCamera.depthTextureMode |= DepthTextureMode.Depth;
            }

            for (int n = 0; n < visibleList.Count; n++)
            {
                Visible vis = visibleList[n];
                NebulaDefinition nebula = vis.Nebula;

                float effectBounds = nebula.Size;
                float surfaceDistance = Mathf.Max(vis.ToCenter.magnitude / vis.ScaledRadius - 1f, 0f);
                float steps = StellarQuality.Steps(nebula.Steps, surfaceDistance, effectBounds);

                Vector3 normal = ((Vector3)(Planetarium.Rotation * (Vector3d)nebula.Normal)).normalized;
                float spinSign = nebula.Spin < 0f ? -1f : 1f;
                float rotation = spinSign * (float)(nebula.RotationSpeed * 0.02 * time % (2.0 * Mathf.PI));

                for (int i = 0; i < NebulaLimits.GasMax; i++)
                {
                    NebulaGas gas = nebula.Gases[i];
                    gasA[i] = new Vector4(gas.ColorR, gas.ColorG, gas.ColorB, gas.Density);
                    gasB[i] = new Vector4(gas.Emission, gas.Absorption, gas.Scale, gas.Coverage);
                    gasC[i] = new Vector4(gas.Contrast, nebula.Warp, nebula.Extent, nebula.Flatten);
                    gasD[i] = new Vector4(gas.Seed, gas.Phase, gas.Shadow, nebula.Hollow);
                }

                BakeState bake;
                if (!bakes.TryGetValue(nebula, out bake))
                {
                    bake = new BakeState();
                    bakes[nebula] = bake;
                }

                if (!bake.IsReady) CreateBake(bake);

                if (propertyBlocks.Count <= n) propertyBlocks.Add(new MaterialPropertyBlock());
                MaterialPropertyBlock block = propertyBlocks[n];

                block.SetVector(RectId, vis.Rect);
                block.SetVector(CenterId, new Vector4(vis.ToCenter.x, vis.ToCenter.y, vis.ToCenter.z, vis.ScaledRadius));
                block.SetVector(NormalId, new Vector4(normal.x, normal.y, normal.z, spinSign));
                block.SetVector(RayId, new Vector4(tanX, tanY, effectBounds, 1f));
                block.SetVector(OccId, nebula.Target == mainBody ? Vector4.zero : mainOccluder);
                block.SetVector(RightId, view.right);
                block.SetVector(UpId, view.up);
                block.SetVector(ForwardId, view.forward);
                block.SetVector(LookId, new Vector4(vis.Brightness, nebula.ColorSaturation, steps, (float)(time % 65536.0)));
                block.SetVector(StarId, new Vector4(nebula.StarTemperature, nebula.StarBrightness, nebula.StarFalloff, rotation));
                block.SetVector(QualityId, new Vector4(StellarQuality.NoiseDetail, nebula.GasCount, nebula.Variation, nebula.Seed));
                block.SetVector(ShapeId, new Vector4(nebula.Shape, nebula.ShapeRatio, nebula.ShapeArms, nebula.Irregularity));
                block.SetVector(EdgeId, new Vector4(nebula.EdgeBlur, 0f, 0f, 0f));
                block.SetVector(LowId, new Vector4(lowWidth, lowHeight, 1f / lowWidth, 1f / lowHeight));
                block.SetVectorArray(GasAId, gasA);
                block.SetVectorArray(GasBId, gasB);
                block.SetVectorArray(GasCId, gasC);
                block.SetVectorArray(GasDId, gasD);
                block.SetTexture(BakeAId, bake.A);
                block.SetTexture(BakeBId, bake.B);
                block.SetTexture(LowColId, lowColor);
                block.SetTexture(LowInfoId, lowInfo);

                int hash = BakeHash(nebula);
                if (!bake.HasHash || bake.Hash != hash)
                {
                    RecordBake(bake, block);
                    bake.Hash = hash;
                    bake.HasHash = true;
                }

                commandBuffer.SetRenderTarget(lowTargets, lowDepth);
                commandBuffer.ClearRenderTarget(false, true, Color.clear);
                commandBuffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6, 1, block);

                commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                commandBuffer.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 6, 1, block);
            }

            if (!isAttached)
            {
                viewCamera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, commandBuffer);
                isAttached = true;
            }
        }

        private void RecordBake(BakeState bake, MaterialPropertyBlock block)
        {
            for (int slice = 0; slice < BakeSize; slice++)
            {
                block.SetVector(BakeId, new Vector4(0f, slice, BakeSize, 0f));

                commandBuffer.SetRenderTarget(bake.A, 0, CubemapFace.Unknown, slice);
                commandBuffer.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 6, 1, block);

                commandBuffer.SetRenderTarget(bake.B, 0, CubemapFace.Unknown, slice);
                commandBuffer.DrawProcedural(Matrix4x4.identity, material, 3, MeshTopology.Triangles, 6, 1, block);
            }
        }

        private void CreateBake(BakeState bake)
        {
            bake.Free();

            bake.A = CreateVolume(RenderTextureFormat.ARGBHalf);
            bake.B = CreateVolume(RenderTextureFormat.RGHalf);
        }

        private static RenderTexture CreateVolume(RenderTextureFormat format)
        {
            var texture = new RenderTexture(BakeSize, BakeSize, 0, format);
            texture.dimension = TextureDimension.Tex3D;
            texture.volumeDepth = BakeSize;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Create();
            return texture;
        }

        private static Texture3D CreateNoiseTexture()
        {
            var texture = new Texture3D(NoiseSize, NoiseSize, NoiseSize, TextureFormat.RFloat, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var random = new System.Random(1337);
            var data = new Color[NoiseSize * NoiseSize * NoiseSize];
            for (int i = 0; i < data.Length; i++)
                data[i] = new Color((float)random.NextDouble(), 0f, 0f, 0f);

            texture.SetPixels(data);
            texture.Apply(false, true);
            return texture;
        }

        private int BakeHash(NebulaDefinition nebula)
        {
            int h = 17;

            for (int i = 0; i < NebulaLimits.GasMax; i++)
            {
                h = Mix(h, gasA[i].w);
                h = Mix(h, gasB[i].y);
                h = Mix(h, gasB[i].z);
                h = Mix(h, gasB[i].w);
                h = Mix(h, gasC[i].x);
                h = Mix(h, gasC[i].y);
                h = Mix(h, gasD[i].x);
                h = Mix(h, gasD[i].z);
            }

            h = Mix(h, gasC[0].z);
            h = Mix(h, gasC[0].w);
            h = Mix(h, gasD[0].w);
            h = Mix(h, nebula.Shape);
            h = Mix(h, nebula.ShapeRatio);
            h = Mix(h, nebula.ShapeArms);
            h = Mix(h, nebula.Irregularity);
            h = Mix(h, nebula.EdgeBlur);
            h = Mix(h, nebula.GasCount);
            h = Mix(h, nebula.Variation);
            h = Mix(h, nebula.Seed);
            h = Mix(h, (float)StellarQuality.NoiseDetail);

            return h;
        }

        private static int Mix(int h, float value)
        {
            unchecked
            {
                return h * 31 + value.GetHashCode();
            }
        }

        private void UpdateDivisor(float coverage)
        {
            if (lowDivisor == DivisorNormal && coverage > LargeCoverageOn) lowDivisor = DivisorLarge;
            else if (lowDivisor == DivisorLarge && coverage < LargeCoverageOff) lowDivisor = DivisorNormal;
        }

        private void EnsureLowResolution()
        {
            int width = Mathf.Max(8, Mathf.CeilToInt(viewCamera.pixelWidth / (float)lowDivisor));
            int height = Mathf.Max(8, Mathf.CeilToInt(viewCamera.pixelHeight / (float)lowDivisor));

            if (lowColor != null && lowInfo != null && lowDepth != null
                && lowColor.IsCreated() && lowInfo.IsCreated() && lowDepth.IsCreated()
                && lowWidth == width && lowHeight == height)
                return;

            ReleaseLowResolution();

            lowColor = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            lowColor.filterMode = FilterMode.Point;
            lowColor.wrapMode = TextureWrapMode.Clamp;
            lowColor.Create();

            lowInfo = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat);
            lowInfo.filterMode = FilterMode.Point;
            lowInfo.wrapMode = TextureWrapMode.Clamp;
            lowInfo.Create();

            lowDepth = new RenderTexture(width, height, 24, RenderTextureFormat.Depth);
            lowDepth.Create();

            lowTargets[0] = lowColor;
            lowTargets[1] = lowInfo;
            lowWidth = width;
            lowHeight = height;
        }

        private void ReleaseLowResolution()
        {
            if (lowColor != null) { lowColor.Release(); Destroy(lowColor); lowColor = null; }
            if (lowInfo != null) { lowInfo.Release(); Destroy(lowInfo); lowInfo = null; }
            if (lowDepth != null) { lowDepth.Release(); Destroy(lowDepth); lowDepth = null; }
        }

        private void LogStatus(NebulaDefinition nebula, Vector3 toCenter, float scaledRadius, bool visible, double time)
        {
            if (time <= nextStatusLog) return;

            nextStatusLog = (float)time + 5f;
            Debug.Log("[StellarEffects] Nebula " + nebula.Body + ": " + (toCenter.magnitude / scaledRadius).ToString("0.0") + " radii away, visible=" + visible);
        }

        public void Release(NebulaDefinition nebula)
        {
            if (nebula.Target != null)
            {
                SetHidden(nebula.Target, false);
                hiddenBodies.Remove(nebula.Target);
            }

            BakeState bake;
            if (bakes.TryGetValue(nebula, out bake))
            {
                bake.Free();
                bakes.Remove(nebula);
            }
        }

        private void SetHidden(CelestialBody body, bool hide)
        {
            if (!hiddenBodies.TryGetValue(body, out HiddenBody state))
            {
                state = new HiddenBody();
                hiddenBodies[body] = state;
            }

            if (hide)
            {
                if (state.Renderers == null)
                {
                    state.Renderers = body.scaledBody.GetComponentsInChildren<Renderer>(true);
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

        private static float EffectExtent(NebulaDefinition nebula)
        {
            float extent = nebula.Extent;
            if (nebula.Shape != 1 && nebula.Shape != 2) return extent;

            return nebula.ShapeRatio + extent * Mathf.Max(1f - nebula.ShapeRatio, 0.05f) * 1.4143f;
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

            foreach (var pair in bakes) pair.Value.Free();
            bakes.Clear();
            ReleaseLowResolution();

            if (commandBuffer != null) commandBuffer.Release();
            if (noiseTexture != null) Destroy(noiseTexture);
            if (material != null) Destroy(material);
        }
    }
}