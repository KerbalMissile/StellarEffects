Shader "Hidden/StellarEffects/Quasar"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D_float _CameraDepthTexture;

            float4 _SE_Rect;
            float4 _SE_Center;
            float4 _SE_Normal;
            float4 _SE_Ray;
            float4 _SE_Right;
            float4 _SE_Up;
            float4 _SE_Forward;
            float4 _SE_Core;
            float4 _SE_CoreB;
            float4 _SE_Torus;
            float4 _SE_TorusB;
            float4 _SE_Jet;
            float4 _SE_JetB;
            float4 _SE_JetC;

            static const float JetBase = 1.5;
            static const float JetRamp = 2.0;
            static const float TwistScale = 40.0;
            static const float DustAbsorb = 0.22;
            static const float CoreOcclude = 10.0;

            static float tipWidth;
            static float ratioUp;
            static float ratioDown;
            static bool torusGlowOn;
            static bool clumpOn;
            static bool trackOcclusion;
            static bool twistOn;
            static float meanderScale;
            static float torusSin;
            static float torusCos;
            static float torusNoiseScale;

            static float qualityLevel;
            static int lightTaps;
            static float marchScale;

            static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(0, 1), float2(1, 0), float2(1, 1) };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 ndc : TEXCOORD0;
            };

            struct fout
            {
                float4 col : SV_Target;
                float depth : SV_Depth;
            };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                float2 ndc = lerp(_SE_Rect.xy, _SE_Rect.zw, Corners[id]);
                o.pos = float4(ndc.x, ndc.y * _ProjectionParams.x, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.ndc = ndc;
                return o;
            }

            float RawDepth(float eye)
            {
                eye = clamp(eye, _ProjectionParams.y * 1.01, _ProjectionParams.z * 0.98);
                return (1.0 / eye - _ZBufferParams.w) / _ZBufferParams.z;
            }

            fout Empty()
            {
                fout o;
                o.col = 0;
                o.depth = RawDepth(_ProjectionParams.z);
                return o;
            }

            float Hash(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            static const float3x3 NoiseRot = float3x3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64);

            float SoftTurb(float n)
            {
                float x = 2.0 * n - 1.0;
                return saturate((sqrt(x * x + 0.03) - 0.1732) / 0.8268);
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                               lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                               lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            float Fbm2(float3 p)
            {
                return (Noise(p) + 0.5 * Noise(p * 2.03 + 1.7)) * (1.0 / 1.5);
            }

            float Fbm3(float3 p)
            {
                return (Noise(p) + 0.5 * Noise(p * 2.03 + 1.7) + 0.25 * Noise(p * 4.11 + 3.3)) * (1.0 / 1.75);
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
                rgb = lerp(rgb, float3(lt, lt, lt), white * 0.7);
                return rgb;
            }

            float InterleavedGradientNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            float PlumeAmount(float zn)
            {
                return smoothstep(_SE_JetC.x - 0.08, _SE_JetC.x + 0.18, zn);
            }

            float JetBaseWidth(float az)
            {
                return _SE_Jet.z + _SE_JetB.z * max(az - JetBase, 0.0);
            }

            float JetHalfWidthP(float az, float P)
            {
                float L = _SE_Jet.y;
                float zn = az / L;
                float grow = pow(saturate((zn - _SE_JetC.x) / max(1.0 - _SE_JetC.x, 0.05)), 0.7);
                return JetBaseWidth(az) * (1.0 + 2.0 * P) + P * L * _SE_JetC.y * (0.35 + 0.65 * grow);
            }

            float JetHalfWidth(float az)
            {
                return JetHalfWidthP(az, PlumeAmount(az / _SE_Jet.y));
            }

            void AddJet(float3 p, float dt, float3 t, float3 bt,
                        float3 coreColor, float3 tailColor, float transmittance,
                        float w, float P, inout float3 jet)
            {
                float3 n = _SE_Normal.xyz;
                float z = dot(p, n);
                float az = abs(z);
                float jetLength = _SE_Jet.y;
                if (az < JetBase || az > jetLength) return;

                float3 q = p - z * n;
                float2 q2d = float2(dot(q, t), dot(q, bt));

                if (dot(q2d, q2d) > 7.29 * w * w) return;

                float time = _SE_CoreB.w;
                float beta = _SE_JetB.x;

                float zn = az / jetLength;
                float w0 = JetBaseWidth(az);

                float drift = time * beta * 0.25;
                float wTip = tipWidth;
                float burst = 0.4 * (0.5 * w + 0.5 * wTip);
                float mScale = meanderScale;
                float2 meander = float2(Noise(float3(az / mScale - drift * 0.1, 1.7, 0.0)),
                                        Noise(float3(az / mScale - drift * 0.1, 8.3, 0.0))) - 0.5;
                float2 off = P * w * 0.5 * meander;
                float lump = lerp(1.0, 0.9 + 0.25 * Noise(float3(az / (2.0 * wTip), 9.1, 3.3)), P);

                float2 qn = (q2d - off) / (w * lump);
                float q2 = dot(qn, qn);
                if (q2 > 4.0) return;

                float radial = sqrt(q2);
                float edgeCut = 1.0 - smoothstep(2.6, 4.0, q2);

                float profile0 = (1.6 * exp(-5.0 * q2) + 0.3 * exp(-1.1 * q2)) * edgeCut;

                float tipAmt = smoothstep(0.55, 1.0, zn);
                float3 cp = float3(qn * 1.3, az / burst - drift * 0.5);
                float3 warp = float3(Noise(cp * 0.8 + 4.1), Noise(cp * 0.8 + 9.2), Noise(cp * 0.8 + 2.6)) - 0.5;
                cp += warp * 1.2;
                float big = Fbm3(cp);
                float detail = Fbm2(cp * 2.6 + 5.5);
                float lit = qualityLevel > 0.5 ? Fbm3(cp + float3(-0.3, 0.4, 0.25)) : big;
                float shade = saturate(0.65 + 2.4 * (big - lit));
                float edgeR = radial + (big - 0.5) * 1.5 + (detail - 0.5) * 0.6;
                float outerR = 1.0 - 0.3 * tipAmt;
                float body = 1.0 - smoothstep(0.35, outerR, edgeR);
                float plumeP = 2.0 * body * lerp(0.3, 1.5, shade) * (0.6 + 0.8 * detail);
                float thr = lerp(0.15, 0.6, tipAmt);
                float fleck = smoothstep(thr - 0.15, thr + 0.15, big * 0.7 + detail * 0.3);
                plumeP *= lerp(1.0, fleck, 0.15 + 0.85 * tipAmt);
                float cloud = big;

                float profile = lerp(profile0, plumeP, P);

                float axial = exp(-1.4 * zn) * (1.0 + 1.3 * exp(-az / (0.06 * jetLength)))
                            * smoothstep(JetBase, JetBase + JetRamp, az)
                            * (1.0 - smoothstep(0.7, 1.0, zn) * (1.0 - 0.9 * P))
                            * (1.0 - smoothstep(0.86, 1.0, zn));
                axial *= lerp(1.0, _SE_JetC.z, P);

                float twistAngle = _SE_JetB.w * TwistScale * (az - time * beta * 1.2);
                float2 qr = qn;
                if (twistOn)
                {
                    float sn, cs;
                    sincos(twistAngle, sn, cs);
                    qr = float2(qn.x * cs - qn.y * sn, qn.x * sn + qn.y * cs);
                }
                float3 sp = float3(qr.x * 2.6 + 3.1, qr.y * 2.6 + 7.7, az * 0.4 - time * beta * 1.1);
                float streaks = 0.6 + 0.8 * (0.7 * Noise(sp) + 0.3 * Noise(sp * float3(2.3, 2.3, 1.9) + 5.3));

                float braid = 0.5 + 0.5 * cos(3.0 * (atan2(qn.y, qn.x) + twistAngle));
                streaks *= lerp(1.0, 0.55 + 1.3 * braid * braid, smoothstep(0.1, 0.9, radial) * (1.0 - P));
                streaks = lerp(streaks, 0.85 + 0.3 * cloud, P);

                float knots = 0.6 + 0.8 * Fbm2(float3(zn * 14.0 - time * beta * 0.8, 4.2, 7.9));
                knots = lerp(knots, 1.0, P);

                float ratio = z >= 0.0 ? ratioUp : ratioDown;
                float boost = min(ratio * ratio, 14.0);

                float heat = saturate(1.0 - 0.8 * zn + 0.3 * (ratio - 1.0));
                float3 c = lerp(tailColor, coreColor, heat);

                float widthDim = (_SE_Jet.z / w0) * pow(w0 / w, 1.0 - 0.85 * P);
                float plumeGain = lerp(1.0, 1.6 * 4.0 / (4.0 + w), P);
                jet += transmittance * c * (_SE_Jet.x * 0.2 * widthDim * plumeGain * profile * axial * streaks * knots * boost * dt);
            }

            void AddCore(float3 p, float dt, float3 coreColor, float transmittance, inout float3 color)
            {
                float3 n = _SE_Normal.xyz;
                float z = dot(p, n);
                float3 q = p - z * n;
                float cs = _SE_Core.y;
                float e = dot(q, q) / (cs * cs) + (z * z) / (0.2 * cs * cs);
                float haze = dot(p, p) / (6.0 * cs * cs);
                if (e > 16.0 && haze > 16.0) return;

                float turb = 0.6 + 0.8 * Fbm2(p / cs * 1.4 + 11.0);
                float dens = exp(-0.5 * e) * turb + 0.18 * exp(-0.5 * haze);
                color += transmittance * coreColor * (_SE_Core.x * 0.8 / cs * dens * dt);
            }

            static const float3 DustExt = float3(0.75, 1.0, 1.7);

            float HG(float c, float g)
            {
                float g2 = g * g;
                float d = max(1.0 + g2 - 2.0 * g * c, 1e-3);
                return (1.0 - g2) / (d * sqrt(d));
            }

            float DustPhase(float c, float ecc)
            {
                return lerp(HG(c, -0.15 * ecc), HG(c, 0.5 * ecc), 0.45);
            }

            float3 TorusPc(float3 p, float3 t, float3 bt)
            {
                float3 n = _SE_Normal.xyz;
                float height = dot(p, n);
                float3 q = p - height * n;
                float2 qp = float2(dot(q, t), dot(q, bt));
                return float3(qp.x * torusCos + qp.y * torusSin, qp.y * torusCos - qp.x * torusSin, height) * torusNoiseScale;
            }

            float TorusLite(float3 p, float3 t, float3 bt)
            {
                float3 n = _SE_Normal.xyz;
                float height = dot(p, n);
                float rc = length(p - height * n);
                float inner = _SE_Torus.x;
                float outer = _SE_Torus.y;
                if (rc <= inner * 0.9 || rc >= outer) return 0.0;
                float zn = height / max(_SE_Torus.z * rc, 1e-3);
                if (abs(zn) > 2.6) return 0.0;
                float radial = smoothstep(inner * 0.9, inner * 1.25, rc) * (1.0 - smoothstep(outer * 0.6, outer, rc));
                float clumpMix = 1.0;
                if (clumpOn)
                {
                    float c = SoftTurb(Noise(TorusPc(p, t, bt) * 0.07 + 31.0));
                    float clump = smoothstep(0.2, 0.8, c);
                    clumpMix = lerp(1.0, 0.5 + 1.1 * clump, saturate(_SE_TorusB.z));
                }
                return radial * exp(-0.5 * zn * zn) * clumpMix * _SE_Torus.w * 1.8;
            }

            void AddTorus(float3 p, float3 v, float dt, float3 t, float3 bt, float3 coreColor, float jit, float occDt,
                          inout float3 color, inout float transmittance, inout float occl)
            {
                float3 n = _SE_Normal.xyz;
                float height = dot(p, n);
                float3 q = p - height * n;
                float rc = length(q);
                float inner = _SE_Torus.x;
                float outer = _SE_Torus.y;
                if (rc <= inner * 0.9 || rc >= outer) return;

                float sigma = max(_SE_Torus.z * rc, 1e-3);
                float zn = height / sigma;
                if (abs(zn) > 2.6) return;

                float radial = smoothstep(inner * 0.9, inner * 1.25, rc) * (1.0 - smoothstep(outer * 0.6, outer, rc));
                float dens = radial * exp(-0.5 * zn * zn);
                if (dens < 0.01) return;

                float3 pc = TorusPc(p, t, bt);
                float2 warp = float2(Noise(pc * 0.05 + 3.1), Noise(pc * 0.05 + 9.7)) - 0.5;
                pc.xy += warp * 6.0;
                float macro = Noise(pc * 0.025 + 5.0);
                float clumpMix = 1.0;
                if (clumpOn)
                {
                    float c;
                    if (qualityLevel > 1.5)
                    {
                        float3 pc2 = mul(NoiseRot, pc);
                        float3 pc3 = mul(NoiseRot, pc2);
                        c = 0.55 * SoftTurb(Noise(pc * 0.07 + 31.0))
                          + 0.30 * SoftTurb(Noise(pc2 * 0.17 + 7.0))
                          + 0.15 * Noise(pc3 * 0.33 + 13.0);
                    }
                    else if (qualityLevel > 0.5)
                    {
                        float3 pc2 = mul(NoiseRot, pc);
                        c = 0.65 * SoftTurb(Noise(pc * 0.07 + 31.0))
                          + 0.35 * SoftTurb(Noise(pc2 * 0.17 + 7.0));
                    }
                    else
                    {
                        c = SoftTurb(Noise(pc * 0.07 + 31.0));
                    }
                    float clump = smoothstep(0.2, 0.8, c);
                    clumpMix = lerp(1.0, 0.5 + 1.1 * clump, saturate(_SE_TorusB.z));
                }
                float density = dens * clumpMix * (0.7 + 0.6 * macro) * _SE_Torus.w * 1.8;

                if (torusGlowOn)
                {
                    float r = length(p);
                    float3 d = p / max(r, 1e-3);
                    float travel = max((rc - inner * 0.9) * r / max(rc, 1e-3), 0.0);
                    float stepL = travel / lightTaps;
                    float tau = 0.0;
                    [loop] for (int k = 0; k < lightTaps; k++)
                        tau += TorusLite(p - d * (stepL * (k + jit)), t, bt);
                    tau *= stepL * DustAbsorb;

                    float cosT = dot(d, -v);
                    float3 e0 = exp(-tau * DustExt);
                    float3 e1 = sqrt(e0);
                    float3 e2 = sqrt(e1);
                    float3 L = e0 * DustPhase(cosT, 1.0)
                             + 0.5 * e1 * DustPhase(cosT, 0.5)
                             + 0.25 * e2 * DustPhase(cosT, 0.25);
                    L *= (1.0 / 1.75);
                    L += 0.05;

                    float rn = saturate(inner / r);
                    float irr = rn * rn;
                    float3 thermal = Vivid(Blackbody(_SE_TorusB.x * pow(rn, 0.45)), 1.4);
                    float3 emitRGB = coreColor * L * (_SE_TorusB.y * irr * 0.14)
                                   + thermal * (_SE_TorusB.y * pow(rn, 1.2) * 0.04);

                    color += transmittance * emitRGB * (density * dt);
                }

                transmittance *= exp(-density * DustAbsorb * dt);
                if (trackOcclusion) occl *= exp(-density * CoreOcclude * occDt);
            }

            fout frag(v2f i)
            {
                float2 ndc = i.ndc;
                float2 uv = ndc * 0.5 + 0.5;
                float3 forward = _SE_Forward.xyz;
                float3 v = normalize(forward + _SE_Right.xyz * ndc.x * _SE_Ray.x + _SE_Up.xyz * ndc.y * _SE_Ray.y);
                float viewCos = dot(v, forward);

                float rs = _SE_Center.w;
                float3 x0 = -_SE_Center.xyz / rs;
                float bound = _SE_Ray.z;

                float b = dot(x0, v);
                float disc = b * b - (dot(x0, x0) - bound * bound);
                if (disc < 0.0) return Empty();
                float s = sqrt(disc);
                float tEnter = max(0.0, -b - s);
                float tEnd = -b + s;
                if (tEnd < 0.0) return Empty();

                float sceneDistance = 1e30;
                if (_SE_Ray.w > 0.5)
                {
                    float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                    #if defined(UNITY_REVERSED_Z)
                        bool sky = rawDepth <= 0.0;
                    #else
                        bool sky = rawDepth >= 1.0;
                    #endif
                    if (!sky)
                        sceneDistance = LinearEyeDepth(rawDepth) / max(viewCos, 1e-4) / rs;
                }
                if (sceneDistance < tEnter) return Empty();

                bool hitScene = sceneDistance < tEnd;
                tEnd = min(tEnd, sceneDistance);

                float3 n = _SE_Normal.xyz;
                float3 t = normalize(cross(n, abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 bt = cross(n, t);

                qualityLevel = _SE_JetC.w;
                lightTaps = qualityLevel > 1.5 ? 5 : (qualityLevel > 0.5 ? 3 : 2);
                marchScale = qualityLevel > 1.5 ? 1.0 : (qualityLevel > 0.5 ? 1.35 : 1.9);

                bool jetOn = _SE_Jet.x > 0.001;
                bool torusOn = _SE_Torus.w > 0.001;
                bool coreOn = _SE_Core.x != 0.0;
                bool glareOn = _SE_Core.w > 0.001;
                torusGlowOn = torusOn && _SE_TorusB.y != 0.0;
                clumpOn = saturate(_SE_TorusB.z) > 0.0;
                trackOcclusion = torusOn && (coreOn || glareOn);
                twistOn = _SE_JetB.w != 0.0;

                float3 coreColor = 0;
                if (coreOn || glareOn || torusGlowOn)
                    coreColor = lerp(Vivid(Blackbody(_SE_Core.z), _SE_CoreB.y), float3(1, 1, 1), 0.35);
                float3 jetCore = 0;
                float3 jetTail = 0;
                if (jetOn)
                {
                    float3 tint = float3(0.9, 0.82, 1.0);
                    jetCore = Vivid(Blackbody(_SE_Jet.w), 1.3) * tint;
                    jetTail = Vivid(Blackbody(_SE_Jet.w * 0.7), 1.3) * tint;
                }

                float coreSize = max(_SE_Core.y, 0.1);
                float torusReach = _SE_Torus.y * 1.1;
                float jetLength = _SE_Jet.y;

                float nv = dot(n, v);
                ratioUp = 1.0 / (1.0 + _SE_JetB.x * nv);
                ratioDown = 1.0 / (1.0 - _SE_JetB.x * nv);
                tipWidth = JetHalfWidth(jetLength);
                meanderScale = max(6.0 * tipWidth, 1.0);
                sincos(_SE_TorusB.w, torusSin, torusCos);
                torusNoiseScale = 10.0 / max(_SE_Torus.x, 1e-3);

                float3 color = 0;
                float3 jet = 0;
                float transmittance = 1.0;
                float occl = 1.0;
                float depthT = -1.0;
                float dither = InterleavedGradientNoise(i.pos.xy);
                int maxSteps = (int)_SE_CoreB.z;
                float rayT = tEnter + 0.01;

                float torusEnter = tEnd;
                float torusExit = tEnd;
                if (torusOn)
                {
                    float H = 2.8 * _SE_Torus.z * _SE_Torus.y;
                    float hv = dot(v, n);
                    float hx = dot(x0, n);
                    float ta = tEnter;
                    float tb = tEnd;
                    if (abs(hv) < 1e-6)
                    {
                        if (abs(hx) > H) { ta = 1.0; tb = 0.0; }
                    }
                    else
                    {
                        float t0 = (-H - hx) / hv;
                        float t1 = (H - hx) / hv;
                        ta = max(ta, min(t0, t1));
                        tb = min(tb, max(t0, t1));
                    }
                    float dsc = b * b - (dot(x0, x0) - torusReach * torusReach);
                    if (dsc < 0.0) { ta = 1.0; tb = 0.0; }
                    else
                    {
                        float sq = sqrt(dsc);
                        ta = max(ta, -b - sq);
                        tb = min(tb, -b + sq);
                    }
                    if (ta < tb) { torusEnter = ta; torusExit = tb; }
                }
                bool inTorusRange = torusEnter < torusExit;
                bool torusJitterDone = false;

                [loop] for (int iter = 0; iter < maxSteps; iter++)
                {
                    if (rayT >= tEnd) break;

                    float3 p = x0 + v * rayT;
                    float r = length(p);
                    float height = dot(p, n);
                    float az = abs(height);
                    float rc = sqrt(max(r * r - height * height, 0.0));

                    float dt = clamp(0.07 * r, 0.12, 4.0);
                    if (r < 3.0 * coreSize) dt = min(dt, 0.3 * coreSize);
                    if (torusOn && inTorusRange && rayT >= torusEnter && rayT < torusExit)
                    {
                        float sg = _SE_Torus.z * max(rc, 0.9 * _SE_Torus.x);
                        dt = min(dt, clamp(min(0.04 * rc, 0.4 * sg), 0.03, 1.0));
                    }
                    float jetWidth = 0.0;
                    float jetPlume = 0.0;
                    if (jetOn)
                    {
                        jetPlume = PlumeAmount(az / jetLength);
                        jetWidth = JetHalfWidthP(az, jetPlume);
                        float radialGap = max(rc - 3.0 * jetWidth, 0.0);
                        float axialGap = max(az - jetLength, 0.0);
                        dt = min(dt, max(0.6 * sqrt(radialGap * radialGap + axialGap * axialGap), min(0.25 * jetWidth, 0.12 * jetWidth + 0.3)));
                    }
                    dt *= marchScale;
                    float segmentEnd = tEnd;
                    float segmentShare = 1.0;
                    if (inTorusRange)
                    {
                        if (rayT < torusEnter) { segmentEnd = torusEnter; segmentShare = 0.35; }
                        else if (rayT < torusExit) { segmentEnd = torusExit; segmentShare = 0.7; }
                    }
                    float rem = max((float)(maxSteps - iter), 1.0);
                    dt = max(dt, (segmentEnd - rayT) / max(rem * segmentShare, 1.0));
                    if (iter == 0) dt *= 0.2 + 0.8 * dither;
                    if (inTorusRange && !torusJitterDone && rayT >= torusEnter - 1e-5)
                    {
                        torusJitterDone = true;
                        if (iter != 0) dt *= 0.2 + 0.8 * dither;
                    }
                    dt = min(dt, max(segmentEnd - rayT, 1e-4));
                    dt = min(dt, max(tEnd - rayT, 1e-4));

                    if (coreOn) AddCore(p, dt, coreColor, occl, color);
                    if (torusOn) AddTorus(p, v, dt, t, bt, coreColor, dither, rayT < -b ? dt : 0.0, color, transmittance, occl);
                    if (jetOn)
                    {
                        float3 pj = x0 + v * (rayT + dt * frac(dither + 0.61803399 * iter));
                        float azj = abs(dot(pj, n));
                        float plumeJ = PlumeAmount(azj / jetLength);
                        AddJet(pj, dt, t, bt, jetCore, jetTail, transmittance, JetHalfWidthP(azj, plumeJ), plumeJ, jet);
                    }

                    if (depthT < 0.0 && (transmittance < 0.75 || dot(color, float3(0.2126, 0.7152, 0.0722)) > 0.12 || max(jet.r, max(jet.g, jet.b)) > 0.12)) depthT = rayT;
                    if (transmittance < 0.02) break;

                    rayT += dt;
                }

                float3 rgb = Tonemap(color);
                rgb += 1.0 - exp(-jet);

                float3 glareAdd = 0;
                if (glareOn && b < 0.0 && !hitScene)
                {
                    float3 m = x0 - v * dot(x0, v);
                    float dg = length(m);
                    float gs = max(_SE_CoreB.x, 1.0);
                    float bq = dg / (0.4 * gs);
                    float bloom = pow(1.0 + bq * bq, -1.5);
                    float edge = _SE_Torus.z * 1.6;
                    edge = edge * rsqrt(1.0 + edge * edge);
                    float sinE = abs(dot(x0, n)) * rsqrt(dot(x0, x0));
                    float vis = lerp(1.0, 0.05 + 0.95 * smoothstep(edge * 0.6, edge * 1.1 + 0.02, sinE), saturate(_SE_Torus.w));
                    glareAdd = coreColor * (_SE_Core.w * vis * occl * (1.1 * exp(-dg / (0.15 * gs)) + 0.3 * bloom));
                }
                rgb += 1.0 - exp(-glareAdd);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb = ToSrgb(rgb);
                #endif

                float alpha = 1.0 - transmittance;
                rgb += (dither - 0.5) * (1.0 / 255.0) * alpha;

                if (depthT < 0.0 && hitScene) depthT = tEnd;
                float eye = depthT < 0.0 ? _ProjectionParams.z : depthT * rs * viewCos;

                fout o;
                o.col = float4(rgb, alpha);
                o.depth = RawDepth(eye);
                return o;
            }
            ENDCG
        }
    }
}
