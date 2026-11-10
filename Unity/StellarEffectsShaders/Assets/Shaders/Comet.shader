Shader "Hidden/StellarEffects/Comet"
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
            float4 _SE_Coma;
            float4 _SE_ComaB;
            float4 _SE_Ion;
            float4 _SE_IonB;
            float4 _SE_IonC;
            float4 _SE_Dust;
            float4 _SE_DustB;
            float4 _SE_DustC;
            float4 _SE_Bend;
            float4 _SE_Look;

            static const float TailEmission = 0.1;
            static const float ComaEmission = 0.15;
            static const float TailAbsorption = 0.02;
            static const float SupportRadius = 3.0;
            static const float3 ComaTint = float3(0.45, 0.92, 1.0);
            static const float3 IonTint = float3(0.3, 0.5, 1.0);
            static const float3 DustTint = float3(1.0, 0.9, 0.93);
            static const float3 Luma = float3(0.2126, 0.7152, 0.0722);

            static float3 cometAxis;
            static float3 cometT;
            static float3 cometBT;
            static float2 cometBend2;
            static float3 cometComaColor;
            static float3 cometIonColor;
            static float3 cometDustColor;
            static float cometTime;
            static float cometFlow;
            static float qualityLevel;
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
            };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                float2 ndc = lerp(_SE_Rect.xy, _SE_Rect.zw, Corners[id]);
                o.pos = float4(ndc.x, ndc.y * _ProjectionParams.x, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.ndc = ndc;
                return o;
            }

            fout Empty()
            {
                fout o;
                o.col = 0;
                return o;
            }

            float Hash(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
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
                float l = dot(c, Luma);
                return max(l + (c - l) * s, 0.0);
            }

            float3 ToSrgb(float3 c)
            {
                c = max(c, 0.0);
                return lerp(1.055 * pow(c, 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308));
            }

            float3 Tonemap(float3 c)
            {
                c = max(c, 0.0);
                return c / (1.0 + c);
            }

            float InterleavedGradientNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            float IonWidth(float a)
            {
                return max(_SE_Ion.z + _SE_Ion.w * a, 1e-4);
            }

            float DustWidth(float a)
            {
                return max(_SE_Dust.z + _SE_Dust.w * a, 1e-4);
            }

            float DustOffset(float a)
            {
                return _SE_DustC.x * a * a / max(_SE_Dust.y, 1.0) * 0.25;
            }

            float ComaAmount(float3 p)
            {
                float size = max(_SE_Coma.y, 0.05);
                float r2 = dot(p, p) / (size * size);
                if (r2 > 24.0) return 0.0;

                float halo = exp(-2.2 * r2);
                float core = exp(-16.0 * r2);
                return halo * 0.45 + core * (1.0 + _SE_ComaB.y);
            }

            float IonAmount(float a, float3 q)
            {
                float len = max(_SE_Ion.y, 1.0);
                if (a <= 0.0 || a >= len) return 0.0;

                float w = IonWidth(a);
                float2 qn = float2(dot(q, cometT), dot(q, cometBT)) / w;
                float r2 = dot(qn, qn);
                if (r2 > 6.25) return 0.0;

                float zn = a / len;
                float edge = 1.0 - smoothstep(1.2, 2.5, r2);
                float profile = (0.65 * exp(-2.0 * r2) + 0.35 * exp(-0.5 * r2)) * edge;
                float axial = exp(-_SE_IonB.w * zn) * smoothstep(0.0, 0.05, zn) * (1.0 - smoothstep(0.8, 1.0, zn));
                float dens = profile * axial / (1.0 + 1.5 * zn) * _SE_IonB.z;

                float stri = saturate(_SE_IonC.x);
                if (stri > 0.001)
                {
                    float3 fp = float3(qn * 3.2 + _SE_IonC.z, a * 0.02 - cometTime * cometFlow * 0.001);
                    float fine = Noise(fp);
                    float broad = qualityLevel > 0.5 ? Noise(fp * 0.4 + 5.7) : 0.5;
                    dens *= lerp(1.0, 0.55 + 0.65 * fine + 0.3 * broad, stri);
                }

                float turb = saturate(_SE_IonC.y);
                if (turb > 0.001)
                {
                    float knots = Noise(float3(a * 0.07 - cometTime * cometFlow * 0.002, _SE_IonC.z + 3.1, 7.7));
                    dens *= lerp(1.0, 0.5 + 1.0 * knots, turb);
                }

                return max(dens, 0.0);
            }

            float DustAmount(float a, float3 q)
            {
                float len = max(_SE_Dust.y, 1.0);
                if (a <= 0.0 || a >= len) return 0.0;

                float w = DustWidth(a);
                float2 q2 = float2(dot(q, cometT), dot(q, cometBT)) - cometBend2 * DustOffset(a);
                float2 qn = q2 / w;
                float r2 = dot(qn, qn);
                if (r2 > 4.0) return 0.0;

                float r = sqrt(r2);
                float zn = a / len;
                float edge = 1.0 - smoothstep(0.35, 1.6, r);
                float profile = edge * (0.55 + 0.45 * exp(-2.0 * r2));
                float axial = exp(-_SE_DustB.w * zn) * smoothstep(0.0, 0.02, zn) * (1.0 - smoothstep(0.72, 1.0, zn));
                float dens = profile * axial / (1.0 + 2.5 * zn) * _SE_DustB.z;

                float stri = saturate(_SE_DustC.y);
                if (stri > 0.001)
                {
                    float3 fp = float3(qn * 2.4 + _SE_DustC.w, a * 0.012 - cometTime * cometFlow * 0.001);
                    float fine = Noise(fp);
                    float broad = qualityLevel > 0.5 ? Noise(fp * 0.35 + 11.3) : 0.5;
                    dens *= lerp(1.0, 0.6 + 0.55 * fine + 0.3 * broad, stri);
                }

                float turb = saturate(_SE_DustC.z);
                if (turb > 0.001)
                {
                    float clump = qualityLevel > 1.5
                        ? 0.6 * Noise(float3(a * 0.03 - cometTime * cometFlow * 0.002, _SE_DustC.w + 1.7, 4.2))
                          + 0.4 * Noise(float3(a * 0.09, _SE_DustC.w + 8.9, 2.1))
                        : Noise(float3(a * 0.05, _SE_DustC.w + 1.7, 4.2));
                    dens *= lerp(1.0, 0.5 + 1.0 * clump, turb);
                }

                return max(dens, 0.0);
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
                tEnd = min(tEnd, sceneDistance);

                cometAxis = normalize(_SE_Normal.xyz);
                cometT = normalize(cross(cometAxis, abs(cometAxis.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                cometBT = cross(cometAxis, cometT);
                cometBend2 = float2(dot(_SE_Bend.xyz, cometT), dot(_SE_Bend.xyz, cometBT));
                cometTime = _SE_Look.w;
                cometFlow = _SE_Look.y;
                qualityLevel = _SE_ComaB.w;
                marchScale = qualityLevel > 1.5 ? 1.0 : (qualityLevel > 0.5 ? 1.5 : 2.2);

                bool ionOn = _SE_Ion.x > 0.001 && _SE_Ion.y > _SE_Ion.z;
                bool dustOn = _SE_Dust.x > 0.001 && _SE_Dust.y > _SE_Dust.z;
                bool comaOn = _SE_Coma.x > 0.0 && _SE_Coma.y > 0.0;

                cometComaColor = lerp(Vivid(Blackbody(_SE_Coma.z), _SE_ComaB.x), ComaTint, 0.4);
                cometIonColor = Vivid(Blackbody(_SE_IonB.x), _SE_IonB.y) * IonTint;
                cometDustColor = Vivid(Blackbody(_SE_DustB.x), _SE_DustB.y) * DustTint;

                float3 color = 0;
                float transmittance = 1.0;
                float dither = InterleavedGradientNoise(i.pos.xy);
                int maxSteps = max((int)_SE_ComaB.z, 1);
                float rayT = tEnter;

                [loop] for (int iter = 0; iter < maxSteps; iter++)
                {
                    if (rayT >= tEnd) break;

                    float3 p0 = x0 + v * rayT;
                    float r = length(p0);
                    float a = dot(p0, cometAxis);
                    float3 q = p0 - a * cometAxis;
                    float rc = length(q);

                    float gap = 1e9;
                    float fine = 1e9;

                    if (comaOn)
                    {
                        float cr = max(_SE_Coma.y, 1e-3) * SupportRadius;
                        gap = min(gap, max(r - cr, 0.0));
                        if (r < cr) fine = min(fine, 0.2 * _SE_Coma.y);
                    }

                    if (ionOn)
                    {
                        float len = _SE_Ion.y;
                        float ac = clamp(a, 0.0, len);
                        float dr = max(rc - IonWidth(ac) * SupportRadius, 0.0);
                        float da = a < 0.0 ? -a : max(a - len, 0.0);
                        gap = min(gap, sqrt(dr * dr + da * da));
                        if (dr <= 0.0 && da <= 0.0) fine = min(fine, 0.3 * IonWidth(a));
                    }

                    if (dustOn)
                    {
                        float len = _SE_Dust.y;
                        float ac = clamp(a, 0.0, len);
                        float2 q2 = float2(dot(q, cometT), dot(q, cometBT)) - cometBend2 * DustOffset(ac);
                        float dr = max(length(q2) - DustWidth(ac) * 2.4, 0.0);
                        float da = a < 0.0 ? -a : max(a - len, 0.0);
                        gap = min(gap, sqrt(dr * dr + da * da));
                        if (dr <= 0.0 && da <= 0.0) fine = min(fine, 0.25 * DustWidth(a));
                    }

                    float dt = fine < 1e8 ? fine : gap * 0.55;
                    dt *= marchScale;

                    float rem = max((float)(maxSteps - iter), 1.0);
                    dt = max(dt, (tEnd - rayT) / rem * 0.25);
                    dt = clamp(dt, 1e-5, max(tEnd - rayT, 1e-5));

                    float jit = frac(dither + iter * 0.618034);
                    float3 p = x0 + v * (rayT + dt * jit);
                    float sa = dot(p, cometAxis);
                    float3 sq = p - sa * cometAxis;

                    if (comaOn)
                    {
                        float dens = ComaAmount(p);
                        if (dens > 0.0)
                            color += transmittance * cometComaColor * (dens * ComaEmission * dt);
                    }

                    if (ionOn)
                    {
                        float dens = IonAmount(sa, sq);
                        if (dens > 0.0)
                            color += transmittance * cometIonColor * (dens * _SE_Ion.x * TailEmission * dt);
                    }

                    if (dustOn)
                    {
                        float dens = DustAmount(sa, sq);
                        if (dens > 0.0)
                        {
                            color += transmittance * cometDustColor * (dens * _SE_Dust.x * TailEmission * dt);
                            transmittance *= exp(-dens * TailAbsorption * dt);
                        }
                    }

                    if (transmittance < 0.02) break;

                    rayT += dt;
                }

                float3 rgb = Tonemap(color * _SE_Look.x);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb = ToSrgb(rgb);
                #endif

                float alpha = saturate(1.0 - transmittance);
                rgb += (dither - 0.5) * (1.0 / 255.0) * alpha;

                fout o;
                o.col = float4(rgb, alpha);
                return o;
            }
            ENDCG
        }
    }
}
