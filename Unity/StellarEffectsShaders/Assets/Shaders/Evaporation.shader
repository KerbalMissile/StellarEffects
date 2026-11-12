Shader "Hidden/StellarEffects/Evaporation"
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
            float4 _SE_ComaColor;
            float4 _SE_Tail;
            float4 _SE_TailB;
            float4 _SE_TailC;
            float4 _SE_TailColor;
            float4 _SE_Bend;
            float4 _SE_Look;

            static const float TailEmission = 0.1;
            static const float ComaEmission = 0.15;
            static const float TailAbsorption = 0.02;
            static const float SupportRadius = 3.0;

            static float3 planetAxis;
            static float3 planetT;
            static float3 planetBT;
            static float2 planetBend2;
            static float3 planetComaColor;
            static float3 planetTailColor;
            static float planetTime;
            static float planetFlow;
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

            float TailWidth(float a)
            {
                return max(_SE_Tail.z + _SE_Tail.w * a, 1e-4);
            }

            float TailOffset(float a)
            {
                return _SE_Bend.w * a * a;
            }

            float ComaAmount(float3 p)
            {
                float size = max(_SE_Coma.y, 0.05);
                float r2 = dot(p, p) / (size * size);
                if (r2 > 24.0) return 0.0;

                float halo = exp(-2.2 * r2);
                float core = exp(-16.0 * r2);
                return halo * 0.45 + core * (1.0 + _SE_Coma.z);
            }

            float TailAmount(float a, float3 q)
            {
                float len = max(_SE_Tail.y, 1.0);
                if (a <= 0.0 || a >= len) return 0.0;

                float w = TailWidth(a);
                float2 q2 = float2(dot(q, planetT), dot(q, planetBT)) - planetBend2 * TailOffset(a);
                float2 qn = q2 / w;
                float r2 = dot(qn, qn);
                if (r2 > 4.0) return 0.0;

                float r = sqrt(r2);
                float zn = a / len;
                float edge = 1.0 - smoothstep(0.35, 1.6, r);
                float profile = edge * (0.55 + 0.45 * exp(-2.0 * r2));
                float axial = exp(-_SE_TailB.y * zn) * smoothstep(0.0, 0.02, zn) * (1.0 - smoothstep(0.72, 1.0, zn));
                float dens = profile * axial / (1.0 + 2.5 * zn) * _SE_TailB.x;

                float stri = saturate(_SE_TailB.z);
                if (stri > 0.001)
                {
                    float3 fp = float3(qn * 2.4 + _SE_TailC.x, a * 0.012 - planetTime * planetFlow * 0.001);
                    float fine = Noise(fp);
                    float broad = qualityLevel > 0.5 ? Noise(fp * 0.35 + 11.3) : 0.5;
                    dens *= lerp(1.0, 0.6 + 0.55 * fine + 0.3 * broad, stri);
                }

                float turb = saturate(_SE_TailB.w);
                if (turb > 0.001)
                {
                    float clump = qualityLevel > 1.5
                        ? 0.6 * Noise(float3(a * 0.03 - planetTime * planetFlow * 0.002, _SE_TailC.x + 1.7, 4.2))
                          + 0.4 * Noise(float3(a * 0.09, _SE_TailC.x + 8.9, 2.1))
                        : Noise(float3(a * 0.05, _SE_TailC.x + 1.7, 4.2));
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

                planetAxis = normalize(_SE_Normal.xyz);
                planetT = normalize(cross(planetAxis, abs(planetAxis.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                planetBT = cross(planetAxis, planetT);
                planetBend2 = float2(dot(_SE_Bend.xyz, planetT), dot(_SE_Bend.xyz, planetBT));
                planetTime = _SE_Look.w;
                planetFlow = _SE_Look.y;
                qualityLevel = _SE_ComaColor.w;
                marchScale = qualityLevel > 1.5 ? 1.0 : (qualityLevel > 0.5 ? 1.5 : 2.2);

                bool tailOn = _SE_Tail.x > 0.001 && _SE_Tail.y > _SE_Tail.z;
                bool comaOn = _SE_Coma.x > 0.0 && _SE_Coma.y > 0.0;

                planetComaColor = pow(max(_SE_ComaColor.rgb, 0.0), 2.2);
                planetTailColor = pow(max(_SE_TailColor.rgb, 0.0), 2.2);

                float3 color = 0;
                float transmittance = 1.0;
                float dither = InterleavedGradientNoise(i.pos.xy);
                int maxSteps = max((int)_SE_Coma.w, 1);
                float rayT = tEnter;

                [loop] for (int iter = 0; iter < maxSteps; iter++)
                {
                    if (rayT >= tEnd) break;

                    float3 p0 = x0 + v * rayT;
                    float r = length(p0);
                    float a = dot(p0, planetAxis);
                    float3 q = p0 - a * planetAxis;
                    float rc = length(q);

                    float gap = 1e9;
                    float fine = 1e9;

                    if (comaOn)
                    {
                        float cr = max(_SE_Coma.y, 1e-3) * SupportRadius;
                        gap = min(gap, max(r - cr, 0.0));
                        if (r < cr) fine = min(fine, 0.2 * _SE_Coma.y);
                    }

                    if (tailOn)
                    {
                        float len = _SE_Tail.y;
                        float ac = clamp(a, 0.0, len);
                        float2 q2 = float2(dot(q, planetT), dot(q, planetBT)) - planetBend2 * TailOffset(ac);
                        float dr = max(length(q2) - TailWidth(ac) * 2.4, 0.0);
                        float da = a < 0.0 ? -a : max(a - len, 0.0);
                        gap = min(gap, sqrt(dr * dr + da * da));
                        if (dr <= 0.0 && da <= 0.0) fine = min(fine, 0.25 * TailWidth(a));
                    }

                    float dt = fine < 1e8 ? fine : gap * 0.55;
                    dt *= marchScale;

                    float rem = max((float)(maxSteps - iter), 1.0);
                    dt = max(dt, (tEnd - rayT) / rem * 0.25);
                    dt = clamp(dt, 1e-5, max(tEnd - rayT, 1e-5));

                    float jit = frac(dither + iter * 0.618034);
                    float3 p = x0 + v * (rayT + dt * jit);
                    float sa = dot(p, planetAxis);
                    float3 sq = p - sa * planetAxis;

                    if (comaOn)
                    {
                        float dens = ComaAmount(p);
                        if (dens > 0.0)
                            color += transmittance * planetComaColor * (dens * ComaEmission * dt);
                    }

                    if (tailOn)
                    {
                        float dens = TailAmount(sa, sq);
                        if (dens > 0.0)
                        {
                            color += transmittance * planetTailColor * (dens * _SE_Tail.x * TailEmission * dt);
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
