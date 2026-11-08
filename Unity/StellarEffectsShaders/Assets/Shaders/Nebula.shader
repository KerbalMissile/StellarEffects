Shader "Hidden/StellarEffects/Nebula"
{
    SubShader
    {
        Cull Off ZWrite Off

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D_float _CameraDepthTexture;
        sampler3D _SE_NoiseTex;
        sampler3D _SE_BakeA;
        sampler3D _SE_BakeB;
        Texture2D<float4> _SE_LowCol;
        Texture2D<float2> _SE_LowInfo;

        float4 _SE_Rect;
        float4 _SE_Center;
        float4 _SE_Normal;
        float4 _SE_Ray;
        float4 _SE_Right;
        float4 _SE_Up;
        float4 _SE_Forward;
        float4 _SE_Look;
        float4 _SE_Star;
        float4 _SE_Quality;
        float4 _SE_Shape;
        float4 _SE_Bake;
        float4 _SE_Low;
        float4 _SE_GasA[5];
        float4 _SE_GasB[5];
        float4 _SE_GasC[5];
        float4 _SE_GasD[5];

        static const float OpticalScale = 8.0;
        static const float EmissionScale = 0.05;
        static const float ScatterScale = 0.08;
        static const float AmbientScatter = 0.004;
        static const float NoiseSize = 96.0;
        static const float BakeSize = 96.0;
        static const int LightBakeTaps = 16;
        static const float3 DustExt = float3(0.75, 1.0, 1.7);
        static const float3x3 NoiseRot = float3x3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64);

        static float3 nebN;
        static float3 nebT;
        static float3 nebB;
        static float nebBound;
        static float2 nebSpinCS;
        static float nebExtent;
        static float nebInvFlatten;
        static float nebHollow;
        static float3 nebStarColor;
        static float3 nebStarBase;
        static float octaves;
        static int nebCount;
        static float nebShape;
        static float nebRatio;
        static float nebArms;
        static float nebIrr;
        static float nebVar;
        static float nebSeed;
        static bool nebLight;
        static float nebStep;
        static float nebSkip;
        static float nebPixAngle;
        static float nebBakeB;
        static float nebTexel;
        static float qualityLevel;
        static float marchScale;

        static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(0, 1), float2(1, 0), float2(1, 1) };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 ndc : TEXCOORD0;
        };

        struct v2fBake
        {
            float4 pos : SV_POSITION;
        };

        struct fout
        {
            float4 col : SV_Target;
            float depth : SV_Depth;
        };

        struct lowout
        {
            float4 col : SV_Target0;
            float2 info : SV_Target1;
        };

        v2f vert(uint id : SV_VertexID)
        {
            v2f o;
            float2 ndc = lerp(_SE_Rect.xy, _SE_Rect.zw, Corners[id]);
            o.pos = float4(ndc.x, ndc.y * _ProjectionParams.x, UNITY_NEAR_CLIP_VALUE, 1.0);
            o.ndc = ndc;
            return o;
        }

        v2f vertLow(uint id : SV_VertexID)
        {
            v2f o;
            float2 ndc = lerp(_SE_Rect.xy, _SE_Rect.zw, Corners[id]);
            o.pos = float4(ndc.x, ndc.y, UNITY_NEAR_CLIP_VALUE, 1.0);
            o.ndc = ndc;
            return o;
        }

        v2fBake vertBake(uint id : SV_VertexID)
        {
            v2fBake o;
            o.pos = float4(Corners[id] * 2.0 - 1.0, UNITY_NEAR_CLIP_VALUE, 1.0);
            return o;
        }

        float RawDepth(float eye)
        {
            eye = clamp(eye, _ProjectionParams.y * 1.01, _ProjectionParams.z * 0.98);
            return (1.0 / eye - _ZBufferParams.w) / _ZBufferParams.z;
        }

        lowout EmptyLow(float sceneEye)
        {
            lowout o;
            o.col = 0;
            o.info = float2(RawDepth(_ProjectionParams.z), sceneEye);
            return o;
        }

        float SceneEye(float2 uv)
        {
            if (_SE_Ray.w < 0.5) return _ProjectionParams.z;

            float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
            #if defined(UNITY_REVERSED_Z)
                bool sky = rawDepth <= 0.0;
            #else
                bool sky = rawDepth >= 1.0;
            #endif
            if (sky) return _ProjectionParams.z;

            float eye = LinearEyeDepth(rawDepth);
            return eye >= _ProjectionParams.z * 0.98 ? _ProjectionParams.z : eye;
        }

        float Noise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
            i = i - NoiseSize * floor(i * (1.0 / NoiseSize));
            return tex3Dlod(_SE_NoiseTex, float4((i + 0.5 + f) * (1.0 / NoiseSize), 0.0)).r;
        }

        float SoftRidge(float n)
        {
            float x = 2.0 * n - 1.0;
            return 1.0 - saturate((sqrt(x * x + 0.05) - 0.2236) / 0.7764);
        }

        float Fbm(float3 p, float count, float ridge, float footprint)
        {
            float sum = 0.0;
            float amp = 0.5;
            float norm = 0.0;
            float mean = lerp(0.5, 0.82, ridge);
            p = mul(NoiseRot, p);
            [loop] for (int k = 0; k < 5; k++)
            {
                if (k >= count) break;

                float w = k == 0 ? 1.0 : 1.0 - smoothstep(0.7, 1.6, footprint);
                float n = mean;
                if (w > 0.0)
                {
                    n = Noise(p);
                    n = lerp(n, SoftRidge(n), ridge);
                    n = lerp(mean, n, w);
                }

                sum += n * amp;
                norm += amp;
                p = mul(NoiseRot, p) * 2.03 + float3(1.7, 3.3, 5.1);
                amp *= 0.5;
                footprint *= 2.03;
            }
            return sum / norm;
        }

        float3 Blackbody(float kelvin)
        {
            float t = clamp(kelvin, 1667.0, 25000.0);
            float t2 = t * t;
            float t3 = t2 * t;
            float x = t < 4000.0
                ? -0.2661239e9 / t3 - 0.2343589e6 / t2 + 0.8776956e3 / t + 0.179910
                : -3.0258469e9 / t3 + 2.1070379e6 / t2 + 0.2226347e3 / t + 0.240390;
            float x2 = x * x;
            float x3 = x2 * x;
            float y = t < 2222.0 ? -1.1063814 * x3 - 1.34811020 * x2 + 2.18555832 * x - 0.20219683
                    : (t < 4000.0 ? -0.9549476 * x3 - 1.37418593 * x2 + 2.09137015 * x - 0.16748867
                                  : 3.0817580 * x3 - 5.87338670 * x2 + 3.75112997 * x - 0.37001483);
            float3 xyz = float3(x / y, 1.0, (1.0 - x - y) / y);
            float3 rgb = float3(
                3.2404542 * xyz.x - 1.5371385 * xyz.y - 0.4985314 * xyz.z,
               -0.9692660 * xyz.x + 1.8760108 * xyz.y + 0.0415560 * xyz.z,
                0.0556434 * xyz.x - 0.2040259 * xyz.y + 1.0572252 * xyz.z);
            rgb = max(rgb, 0.0);
            rgb /= max(max(rgb.r, max(rgb.g, rgb.b)), 1e-5);
            return rgb * saturate(kelvin / 1667.0);
        }

        float3 Vivid(float3 c, float s)
        {
            float l = dot(c, float3(0.2126, 0.7152, 0.0722));
            return max(l + (c - l) * s, 0.0);
        }

        float3 ToSrgb(float3 c)
        {
            c = max(c, 0.0);
            return lerp(1.055 * pow(c, 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308));
        }

        float3 Tonemap(float3 c)
        {
            float l = max(dot(c, float3(0.2126, 0.7152, 0.0722)), 1e-5);
            float lt = 1.0 - exp(-l * 0.9);
            float3 rgb = c / l * lt;
            rgb /= max(max(rgb.r, max(rgb.g, rgb.b)), 1.0);
            float white = smoothstep(0.6, 1.0, lt);
            rgb = lerp(rgb, float3(lt, lt, lt), white * 0.3);
            return rgb;
        }

        float InterleavedGradientNoise(float2 p)
        {
            return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
        }

        float HG(float c, float g)
        {
            float g2 = g * g;
            float d = max(1.0 + g2 - 2.0 * g * c, 1e-3);
            return min((1.0 - g2) / (d * sqrt(d)), 4.0);
        }

        float ShapeBaseDist(float3 u)
        {
            float rr = length(u);
            float d = rr;

            if (nebShape > 0.5 && nebShape < 1.5)
            {
                float rc = length(u.xy);
                float tube = max(1.0 - nebRatio, 0.05);
                d = length(float2(rc - nebRatio, u.z)) / tube;
            }
            else if (nebShape > 1.5 && nebShape < 2.5)
            {
                float3 c = float3(0.0, 0.0, nebRatio);
                d = min(length(u - c), length(u + c)) / max(1.0 - nebRatio, 0.05);
            }
            else if (nebShape > 2.5 && nebShape < 3.5)
            {
                float rc = length(u.xy);
                float ang = atan2(u.y, u.x);
                float arm = cos(nebArms * (ang - nebRatio * 8.0 * rc));
                d = rr * (1.0 + 1.2 * (0.5 - 0.5 * arm));
            }

            return d;
        }

        float ShapeIrregularity(float3 u)
        {
            float irr = nebIrr + (nebShape > 3.5 ? 0.8 : 0.0);
            if (irr <= 0.001) return 1.0;

            float n = Fbm(u * 1.6 + nebSeed * 3.7 + 21.0, 2.0, 0.0, nebStep * 1.6);
            return 1.0 + irr * max(0.65 - n, 0.0) * 2.5;
        }

        float EnvelopeValue(float d)
        {
            if (d >= nebExtent) return 0.0;

            float env = 1.0 - smoothstep(nebExtent * 0.3, nebExtent, d);
            if (nebHollow > 0.001) env *= smoothstep(nebHollow * nebExtent * 0.4, nebHollow * nebExtent, d);
            return env;
        }

        void ShapeSpace(float3 q, out float3 u)
        {
            float h = dot(q, nebN);
            u = float3(dot(q, nebT), dot(q, nebB), h * nebInvFlatten);
            u.xy = float2(u.x * nebSpinCS.y - u.y * nebSpinCS.x,
                          u.x * nebSpinCS.x + u.y * nebSpinCS.y);
        }

        float Envelope(float3 q, out float3 u, out float irrMul, out float dist)
        {
            ShapeSpace(q, u);

            float d = ShapeBaseDist(u);
            irrMul = 1.0;
            if (d < nebExtent) irrMul = ShapeIrregularity(u);

            dist = d * irrMul;
            return EnvelopeValue(dist);
        }

        float GasVariation(int g, float3 u)
        {
            float seed = _SE_GasD[g].x + nebSeed;
            float m = Fbm(u * 1.5 + seed * 5.3 + 7.0, 2.0, 0.0, nebStep * 1.5);
            return lerp(1.0, smoothstep(0.3, 0.7, m) * 1.6, nebVar);
        }

        float GasDensity(int g, float3 u, float env, float count, float varMul)
        {
            float4 A = _SE_GasA[g];
            if (A.w <= 0.0) return 0.0;

            float4 B = _SE_GasB[g];
            float4 C = _SE_GasC[g];
            float4 D = _SE_GasD[g];

            float seed = D.x + nebSeed;
            float3 p = u * B.z + seed * 17.31;
            if (count > 2.5 && C.y > 0.0)
            {
                float3 pw = mul(NoiseRot, p * 0.5);
                float3 w = float3(Noise(pw + 4.1), Noise(pw + 9.2), Noise(pw + 2.6)) - 0.5;
                p += w * C.y * 4.0;
            }

            float n = Fbm(p, count, C.x, nebStep * B.z);
            float d = saturate((n - B.w) / max(1.0 - B.w, 0.05));
            d = d * d * (3.0 - 2.0 * d);

            return d * env * A.w * varMul;
        }

        float GasDensityTap(int g, float3 u, float env, float varMul)
        {
            float4 A = _SE_GasA[g];
            if (A.w <= 0.0) return 0.0;

            float4 B = _SE_GasB[g];
            float4 D = _SE_GasD[g];

            float3 p = u * B.z + (D.x + nebSeed) * 17.31;
            float n = Noise(mul(NoiseRot, p));
            n = lerp(n, SoftRidge(n), _SE_GasC[g].x);

            float d = saturate((n - B.w) / max(1.0 - B.w, 0.05));
            d = d * d * (3.0 - 2.0 * d);

            return d * env * A.w * varMul;
        }

        bool LightEnabled()
        {
            bool on = false;
            if (_SE_Star.y > 0.0)
            {
                [loop] for (int g = 0; g < nebCount; g++)
                {
                    if (_SE_GasA[g].w > 0.0 && _SE_GasB[g].y > 0.0 && _SE_GasD[g].z > 0.0) on = true;
                }
            }
            return on;
        }

        void InitShape()
        {
            nebCount = clamp((int)_SE_Quality.y, 1, 5);
            nebExtent = _SE_GasC[0].z;
            nebInvFlatten = 1.0 / max(_SE_GasC[0].w, 0.02);
            nebHollow = _SE_GasD[0].w;
            nebShape = _SE_Shape.x;
            nebRatio = max(_SE_Shape.y, 0.0);
            nebArms = floor(_SE_Shape.z + 0.5);
            nebIrr = _SE_Shape.w;
            nebVar = _SE_Quality.z;
            nebSeed = _SE_Quality.w;

            qualityLevel = _SE_Quality.x;
            octaves = qualityLevel > 1.5 ? 4.0 : (qualityLevel > 0.5 ? 3.0 : 2.0);
            marchScale = qualityLevel > 1.5 ? 1.0 : (qualityLevel > 0.5 ? 1.5 : 2.2);

            bool ringlike = nebShape > 0.5 && nebShape < 2.5;
            nebBakeB = ringlike ? nebRatio + nebExtent * max(1.0 - nebRatio, 0.05) * 1.4143 : nebExtent;
            nebTexel = 2.0 * nebBakeB / BakeSize;

            nebStep = 0.0;
            nebSkip = 1.0;
            nebPixAngle = 0.0;
        }

        bool ClipRay(float3 o, float3 d, float rc, float hh, inout float t0, inout float t1)
        {
            if (abs(d.z) > 1e-6)
            {
                float ta = (-hh - o.z) / d.z;
                float tb = (hh - o.z) / d.z;
                t0 = max(t0, min(ta, tb));
                t1 = min(t1, max(ta, tb));
            }
            else if (abs(o.z) > hh)
            {
                return false;
            }

            float a = dot(d.xy, d.xy);
            if (a > 1e-8)
            {
                float tm = -dot(o.xy, d.xy) / a;
                float2 mm = o.xy + d.xy * tm;
                float h2 = rc * rc - dot(mm, mm);
                if (h2 < 0.0) return false;
                float hs = sqrt(h2 / a);
                t0 = max(t0, tm - hs);
                t1 = min(t1, tm + hs);
            }
            else if (dot(o.xy, o.xy) > rc * rc)
            {
                return false;
            }

            return t0 < t1;
        }

        float BakeDensity(int g, float3 u)
        {
            if (_SE_GasA[g].w <= 0.0) return 0.0;

            float variation = 1.0;
            if (nebVar > 0.001) variation = GasVariation(g, u);

            return GasDensity(g, u, 1.0, octaves, variation);
        }

        float BakeLight(float3 u)
        {
            float d0 = ShapeBaseDist(u);
            float irrMul = 1.0;
            if (d0 < nebExtent) irrMul = ShapeIrregularity(u);
            if (d0 * irrMul >= nebExtent) return 0.0;

            float vars[5];
            [loop] for (int g = 0; g < nebCount; g++)
            {
                float variation = 1.0;
                if (nebVar > 0.001 && _SE_GasA[g].w > 0.0) variation = GasVariation(g, u);
                vars[g] = variation;
            }

            float sum = 0.0;
            [loop] for (int k = 0; k < LightBakeTaps; k++)
            {
                float s = (k + 0.5) / LightBakeTaps;
                float3 uk = u * (1.0 - s);
                float env = EnvelopeValue(ShapeBaseDist(uk) * irrMul);
                if (env <= 0.0) continue;

                [loop] for (int h = 0; h < nebCount; h++)
                {
                    float sigma = _SE_GasB[h].y * _SE_GasD[h].z;
                    if (sigma <= 0.0 || _SE_GasA[h].w <= 0.0) continue;
                    sum += GasDensityTap(h, uk, env, vars[h]) * sigma;
                }
            }

            return sum / LightBakeTaps;
        }

        float3 BakeU(float4 pos)
        {
            float3 tc = float3(pos.xy / BakeSize, (_SE_Bake.y + 0.5) / BakeSize);
            return (tc * 2.0 - 1.0) * nebBakeB;
        }

        void AddNebula(float3 p, float3 v, float rayT, float dt, inout float3 color, inout float transmittance)
        {
            nebStep = dt / nebBound * max(1.0, nebInvFlatten);
            nebSkip = 1.0;

            float3 q = p / nebBound;
            float3 u;
            float irrMul;
            float dist;
            float env = Envelope(q, u, irrMul, dist);
            if (env <= 0.0005)
            {
                float inner = nebHollow * nebExtent * 0.4;
                float gap = dist >= nebExtent ? dist - nebExtent : inner - dist;
                nebSkip = clamp(0.25 * gap / max(nebStep, 1e-6), 1.0, 3.0);
                return;
            }

            float pixFoot = rayT * nebPixAngle / nebBound * max(1.0, nebInvFlatten);
            float lod = saturate((max(nebStep, pixFoot) / nebTexel - 0.5) * 2.0);
            float3 bu = u * (0.5 / nebBakeB) + 0.5;

            float2 bakeB = 0.0;
            float4 bakeA = 0.0;
            if (nebLight || lod > 0.0) bakeB = tex3Dlod(_SE_BakeB, float4(bu, 0.0)).rg;
            if (lod > 0.0) bakeA = tex3Dlod(_SE_BakeA, float4(bu, 0.0));
            float bk[5] = { bakeA.x, bakeA.y, bakeA.z, bakeA.w, bakeB.x };

            float dens[5];
            float sigma = 0.0;
            float total = 0.0;

            [loop] for (int g = 0; g < nebCount; g++)
            {
                float dp = 0.0;
                if (lod < 1.0)
                {
                    float variation = 1.0;
                    if (nebVar > 0.001 && _SE_GasA[g].w > 0.0)
                        variation = GasVariation(g, u);

                    dp = GasDensity(g, u, 1.0, octaves, variation);
                }

                float d = lerp(dp, bk[g], lod) * env;
                dens[g] = d;
                sigma += d * _SE_GasB[g].y;
                total += d;
            }

            if (total < 0.002) return;

            float rq = length(q);
            float3 dir = q / max(rq, 1e-4);
            float cosT = dot(dir, -v);

            float3 e0 = float3(1, 1, 1);
            if (nebLight) e0 = exp(-(rq * OpticalScale * bakeB.y) * DustExt);

            float falloff = rq / max(_SE_Star.z, 1e-3);
            float3 starCol = nebStarBase / (1.0 + falloff * falloff);

            float3 emit = 0;
            [loop] for (int k = 0; k < nebCount; k++)
            {
                float d = dens[k];
                if (d <= 0.0) continue;

                float3 gc = _SE_GasA[k].rgb;
                float phase = HG(cosT, _SE_GasD[k].y);
                float3 glow = gc * (_SE_GasB[k].x * EmissionScale);
                float3 scatter = gc * (starCol * e0 * (phase * ScatterScale) + AmbientScatter);
                emit += d * (glow + scatter);
            }

            float dtn = dt / nebBound * OpticalScale;
            float ext = exp(-sigma * dtn);
            float3 gain = sigma > 1e-5 ? emit * ((1.0 - ext) / sigma) : emit * dtn;
            color += transmittance * gain;
            transmittance *= ext;
        }

        lowout fragMarch(v2f i)
        {
            InitShape();
            nebPixAngle = 2.0 * _SE_Ray.y / _ScreenParams.y;

            float2 ndc = i.ndc;
            float2 uv = ndc * 0.5 + 0.5;
            float sceneEye = SceneEye(uv);

            float3 forward = _SE_Forward.xyz;
            float3 v = normalize(forward + _SE_Right.xyz * ndc.x * _SE_Ray.x + _SE_Up.xyz * ndc.y * _SE_Ray.y);
            float viewCos = dot(v, forward);

            float rs = _SE_Center.w;
            float3 x0 = -_SE_Center.xyz / rs;
            nebBound = _SE_Ray.z;
            nebLight = LightEnabled();

            nebN = normalize(_SE_Normal.xyz);
            nebT = normalize(cross(nebN, abs(nebN.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
            nebB = cross(nebN, nebT);
            float spinSin, spinCos;
            sincos(_SE_Star.w, spinSin, spinCos);
            nebSpinCS = float2(spinSin, spinCos);
            nebStarColor = lerp(Vivid(Blackbody(_SE_Star.x), 1.2), float3(1, 1, 1), 0.3);
            nebStarBase = nebStarColor * _SE_Star.y;

            float reach = nebBound * nebBakeB;
            float b = dot(x0, v);
            float3 m = x0 - v * b;
            float h2 = reach * reach - dot(m, m);
            if (h2 < 0.0) return EmptyLow(sceneEye);
            float s = sqrt(h2);
            float tEnter = max(0.0, -b - s);
            float tEnd = -b + s;
            if (tEnd < 0.0) return EmptyLow(sceneEye);

            float flatten = 1.0 / nebInvFlatten;
            float tube = max(1.0 - nebRatio, 0.05);
            float rc = nebExtent;
            float hh = nebExtent * flatten;
            if (nebShape > 0.5 && nebShape < 1.5)
            {
                rc = nebRatio + nebExtent * tube;
                hh = nebExtent * tube * flatten;
            }
            else if (nebShape > 1.5 && nebShape < 2.5)
            {
                rc = nebExtent * tube;
                hh = (nebRatio + nebExtent * tube) * flatten;
            }

            float3 ro = float3(dot(x0, nebT), dot(x0, nebB), dot(x0, nebN));
            float3 rd = float3(dot(v, nebT), dot(v, nebB), dot(v, nebN));
            if (!ClipRay(ro, rd, rc * nebBound, hh * nebBound, tEnter, tEnd)) return EmptyLow(sceneEye);

            bool occluder = sceneEye < _ProjectionParams.z * 0.98;
            float sceneDistance = occluder ? sceneEye / max(viewCos, 1e-4) / rs : 1e30;
            if (sceneDistance < tEnter) return EmptyLow(sceneEye);

            bool hitScene = sceneDistance < tEnd;
            tEnd = min(tEnd, sceneDistance);

            float3 color = 0;
            float transmittance = 1.0;
            float depthT = -1.0;
            float dither = InterleavedGradientNoise(i.pos.xy);
            int maxSteps = max((int)_SE_Look.z, 1);
            float minDt = 0.004 * reach;
            float rayT = tEnter;

            [loop] for (int iter = 0; iter < maxSteps; iter++)
            {
                if (rayT >= tEnd) break;

                float rem = max((float)(maxSteps - iter), 1.0);
                float dt = max(minDt, (tEnd - rayT) / max(rem * 0.8, 1.0));
                dt *= marchScale * nebSkip;
                dt = min(dt, max(tEnd - rayT, 1e-5 * reach));

                float jit = frac(dither + iter * 0.618034);
                float tSample = rayT + dt * jit;
                float3 p = m + v * (tSample + b);
                AddNebula(p, v, tSample, dt, color, transmittance);

                if (depthT < 0.0 && (transmittance < 0.75 || dot(color, float3(0.2126, 0.7152, 0.0722)) > 0.12)) depthT = rayT;
                if (transmittance < 0.05) break;

                rayT += dt;
            }

            if (depthT < 0.0 && hitScene) depthT = tEnd;
            float eye = depthT < 0.0 ? _ProjectionParams.z : depthT * rs * viewCos;

            lowout o;
            o.col = float4(color, 1.0 - transmittance);
            o.info = float2(RawDepth(eye), sceneEye);
            return o;
        }

        fout fragUp(v2f i)
        {
            float2 ndc = i.ndc;
            float sceneEye = SceneEye(ndc * 0.5 + 0.5);

            float2 tc = float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5);
            float2 pf = tc * _SE_Low.xy - 0.5;
            float2 fl = floor(pf);
            float2 fr = pf - fl;
            int2 hi = int2((int)_SE_Low.x, (int)_SE_Low.y) - 1;

            float4 acc = 0.0;
            float wsum = 0.0;
            float bestW = -1.0;
            float bestDepth = RawDepth(_ProjectionParams.z);

            [unroll] for (int j = -1; j <= 2; j++)
            {
                [unroll] for (int k = -1; k <= 2; k++)
                {
                    int2 pc = clamp(int2(fl) + int2(k, j), int2(0, 0), hi);
                    float4 c = _SE_LowCol.Load(int3(pc, 0));
                    float2 inf = _SE_LowInfo.Load(int3(pc, 0));

                    float dx = k - fr.x;
                    float dy = j - fr.y;
                    float wb = exp(-(dx * dx + dy * dy) * 0.9);
                    float rel = abs(inf.y - sceneEye) / max(sceneEye, 1e-3);
                    float w = wb / (1.0 + rel * 64.0) + 1e-6;

                    acc += c * w;
                    wsum += w;

                    if (w > bestW)
                    {
                        bestW = w;
                        bestDepth = inf.x;
                    }
                }
            }

            float4 col = acc / wsum;
            if (col.a <= 1e-5 && dot(col.rgb, float3(1, 1, 1)) <= 1e-5) discard;
            if (sceneEye < _ProjectionParams.z * 0.98 && LinearEyeDepth(bestDepth) > sceneEye * 1.002) discard;

            float3 rgb = Tonemap(Vivid(col.rgb * _SE_Look.x, _SE_Look.y));

            #if defined(UNITY_COLORSPACE_GAMMA)
                rgb = ToSrgb(rgb);
            #endif

            float alpha = saturate(col.a);

            float2 fp = i.pos.xy;
            float nc = InterleavedGradientNoise(fp + float2(37.0, 17.0)) + InterleavedGradientNoise(fp + float2(59.0, 83.0)) - 1.0;
            float na = InterleavedGradientNoise(fp + float2(11.0, 71.0)) + InterleavedGradientNoise(fp + float2(97.0, 29.0)) - 1.0;
            float mask = saturate(max(alpha, max(rgb.r, max(rgb.g, rgb.b))) * 255.0);
            rgb = max(rgb + nc * (1.0 / 255.0) * mask, 0.0);
            alpha = saturate(alpha + na * (1.0 / 255.0) * saturate(alpha * 255.0));

            fout o;
            o.col = float4(rgb, alpha);
            o.depth = bestDepth;
            return o;
        }

        float4 fragBakeA(v2fBake i) : SV_Target
        {
            InitShape();
            nebStep = nebTexel;
            float3 u = BakeU(i.pos);

            float4 r = 0.0;
            if (nebCount > 0) r.x = BakeDensity(0, u);
            if (nebCount > 1) r.y = BakeDensity(1, u);
            if (nebCount > 2) r.z = BakeDensity(2, u);
            if (nebCount > 3) r.w = BakeDensity(3, u);
            return r;
        }

        float2 fragBakeB(v2fBake i) : SV_Target
        {
            InitShape();
            nebStep = nebTexel;
            float3 u = BakeU(i.pos);

            float g4 = 0.0;
            if (nebCount > 4) g4 = BakeDensity(4, u);

            return float2(g4, BakeLight(u));
        }
        ENDCG

        Pass
        {
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vertLow
            #pragma fragment fragMarch
            ENDCG
        }

        Pass
        {
            ZTest Always
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG
        }

        Pass
        {
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vertBake
            #pragma fragment fragBakeA
            ENDCG
        }

        Pass
        {
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vertBake
            #pragma fragment fragBakeB
            ENDCG
        }
    }
}