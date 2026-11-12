Shader "Hidden/StellarEffects/NeutronStar"
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
            float4 _SE_Ray;
            float4 _SE_Right;
            float4 _SE_Up;
            float4 _SE_Forward;
            float4 _SE_Core;
            float4 _SE_CoreColor;
            float4 _SE_Magnet;
            float4 _SE_MagnetU;
            float4 _SE_Beam;
            float4 _SE_BeamB;
            float4 _SE_BeamColor;
            float4 _SE_Field;
            float4 _SE_FieldB;
            float4 _SE_FieldColor;

            static const float BeamBase = 1.0;
            static const float BeamRamp = 1.5;
            static const float SurfaceRadius = 1.0;
            static const float FieldMinShell = 1.5;
            static const float3 Luma = float3(0.2126, 0.7152, 0.0722);

            static float3 starMagnet;
            static float3 starMagU;
            static float3 starMagW;
            static float3 starCoreColor;
            static float3 starBeamColor;
            static float3 starFieldColor;
            static float starTime;
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
                float l = max(dot(c, Luma), 1e-5);
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

            float BeamWidth(float az)
            {
                return max(_SE_Beam.z + _SE_Beam.w * az, 1e-3);
            }

            void AddBeams(float3 p, float3 v, float dt, inout float3 color)
            {
                float s = dot(p, starMagnet);
                float az = abs(s);
                float len = _SE_Beam.y;
                if (az < BeamBase || az > len) return;

                float3 q = p - s * starMagnet;
                float w = BeamWidth(az);
                float q2 = dot(q, q) / (w * w);
                if (q2 > 9.0) return;

                float zn = az / len;
                float w0 = max(_SE_Beam.z, 1e-3);
                float profile = exp(-3.0 * q2) + 0.25 * exp(-0.6 * q2);
                float axial = exp(-1.6 * zn) * (1.0 + 1.2 * exp(-az / (0.05 * len)))
                            * smoothstep(BeamBase, BeamBase + BeamRamp, az)
                            * (1.0 - smoothstep(0.6, 1.0, zn));

                float turb = saturate(_SE_BeamB.y);
                float knots = 1.0;
                if (turb > 0.001)
                {
                    float2 qd = float2(dot(q, starMagU), dot(q, starMagW)) / w;
                    float3 kp = float3(qd * 1.6, az * 0.3 - starTime * 2.5);
                    float n = 0.65 * Noise(kp) + 0.35 * (qualityLevel > 0.5 ? Noise(kp * 2.3 + 5.1) : 0.5);
                    knots = lerp(1.0, 0.45 + 1.1 * n, turb);
                }

                float c = saturate(dot(s >= 0.0 ? starMagnet : -starMagnet, -v));
                float boost = 1.0 + _SE_BeamB.x * c * c * c;

                float widthDim = pow(w0 / w, 1.5);
                float heat = saturate(1.0 - 0.8 * zn);
                float3 col = lerp(starBeamColor, float3(1, 1, 1), 0.35 * heat * heat);

                color += col * (_SE_Beam.x * 0.25 * widthDim * profile * axial * knots * boost * dt);
            }

            void AddField(float3 p, float dt, inout float3 color)
            {
                float r = length(p);
                float ext = _SE_Field.y;
                if (r < SurfaceRadius || r > ext) return;

                float c = dot(p, starMagnet) / r;
                float s2 = max(1.0 - c * c, 0.003);
                float shellL = r / s2;
                if (shellL < FieldMinShell || shellL > ext) return;

                float span = log(ext / FieldMinShell);
                float shells = max(_SE_Field.z, 1.0);
                float t = log(shellL / FieldMinShell) / span * shells;
                float d = abs(frac(t + 0.5) - 0.5);
                float lw = max(_SE_Field.w, 0.01);
                float lineMask = exp(-0.5 * d * d / (lw * lw));

                float azMask = 1.0;
                float azCount = _SE_FieldB.x;
                if (azCount > 0.5)
                {
                    float az = atan2(dot(p, starMagW), dot(p, starMagU));
                    float a = 0.5 + 0.5 * cos(azCount * az);
                    azMask = a * a;
                }

                float fade = pow(1.0 / r, 1.3)
                           * (1.0 - smoothstep(0.6 * ext, ext, r))
                           * smoothstep(SurfaceRadius, SurfaceRadius + 0.3, r);

                color += starFieldColor * (_SE_Field.x * 0.5 * lineMask * azMask * fade * dt);
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

                bool hitSurface = false;
                float tSurface = 0.0;
                float dsc = b * b - (dot(x0, x0) - SurfaceRadius * SurfaceRadius);
                if (dsc > 0.0)
                {
                    float ts = -b - sqrt(dsc);
                    if (ts > 0.0 && ts < tEnd)
                    {
                        hitSurface = true;
                        tSurface = ts;
                        tEnd = ts;
                    }
                }

                starMagnet = normalize(_SE_Magnet.xyz);
                starMagU = normalize(_SE_MagnetU.xyz);
                starMagW = cross(starMagnet, starMagU);
                starTime = _SE_MagnetU.w;
                qualityLevel = _SE_CoreColor.w;
                marchScale = qualityLevel > 1.5 ? 1.0 : (qualityLevel > 0.5 ? 1.5 : 2.2);

                starCoreColor = pow(max(_SE_CoreColor.rgb, 0.0), 2.2);
                starBeamColor = pow(max(_SE_BeamColor.rgb, 0.0), 2.2);
                starFieldColor = pow(max(_SE_FieldColor.rgb, 0.0), 2.2);

                bool coreOn = _SE_Core.x > 0.001;
                bool glareOn = _SE_Core.z > 0.001;
                bool beamOn = _SE_Beam.x > 0.001 && _SE_Beam.y > BeamBase;
                bool fieldOn = _SE_Field.x > 0.001 && _SE_Field.y > FieldMinShell;

                float beamLen = _SE_Beam.y;
                float fieldExt = _SE_Field.y;
                float fieldStep = clamp(0.5 * max(_SE_Field.w, 0.01) * log(max(fieldExt / FieldMinShell, 1.01)) / max(_SE_Field.z, 1.0), 0.015, 0.15);

                float3 color = 0;
                float dither = InterleavedGradientNoise(i.pos.xy);
                int maxSteps = max((int)_SE_Magnet.w, 1);
                float rayT = tEnter;

                if (beamOn || fieldOn)
                {
                    [loop] for (int iter = 0; iter < maxSteps; iter++)
                    {
                        if (rayT >= tEnd) break;

                        float3 p0 = x0 + v * rayT;
                        float r0 = length(p0);
                        float gap = 1e9;
                        float fine = 1e9;

                        if (fieldOn)
                        {
                            gap = min(gap, max(r0 - fieldExt, 0.0));
                            if (r0 < fieldExt) fine = min(fine, fieldStep * r0);
                        }

                        if (beamOn)
                        {
                            float s0 = dot(p0, starMagnet);
                            float az0 = abs(s0);
                            float3 q0 = p0 - s0 * starMagnet;
                            float ac = clamp(az0, BeamBase, beamLen);
                            float w = BeamWidth(ac);
                            float dr = max(length(q0) - 3.0 * w, 0.0);
                            float da = az0 < BeamBase ? BeamBase - az0 : max(az0 - beamLen, 0.0);
                            gap = min(gap, sqrt(dr * dr + da * da));
                            if (dr <= 0.0 && da <= 0.0) fine = min(fine, 0.35 * w);
                        }

                        float dt = fine < 1e8 ? fine : gap * 0.55;
                        dt *= marchScale;

                        float rem = max((float)(maxSteps - iter), 1.0);
                        dt = max(dt, (tEnd - rayT) / rem * 0.25);
                        dt = clamp(dt, 1e-5, max(tEnd - rayT, 1e-5));

                        float jit = frac(dither + iter * 0.618034);
                        float3 p = x0 + v * (rayT + dt * jit);

                        if (fieldOn) AddField(p, dt, color);
                        if (beamOn) AddBeams(p, v, dt, color);

                        rayT += dt;
                    }
                }

                float cover = 0.0;
                if (coreOn && hitSurface)
                {
                    float3 pS = x0 + v * tSurface;
                    float mu = saturate(dot(pS, -v));
                    float limb = 0.45 + 0.55 * pow(mu, 0.6);
                    float cap = pow(abs(dot(pS, starMagnet)), 12.0);
                    float mottle = 0.9 + 0.2 * Noise(pS * 6.0 + 3.7);
                    cover = smoothstep(0.0, 0.08, mu);
                    color += starCoreColor * (_SE_Core.x * limb * mottle * (1.0 + _SE_Core.y * cap) * cover);
                }

                float3 rgb = Tonemap(color);

                if (glareOn && b < 0.0 && !hitScene)
                {
                    float3 m = x0 - v * dot(x0, v);
                    float dg = length(m);
                    float gs = max(_SE_Core.w, 1.0);
                    float bq = dg / (0.4 * gs);
                    float bloom = pow(1.0 + bq * bq, -1.5);
                    float3 glareAdd = starCoreColor * (_SE_Core.z * (1.1 * exp(-dg / (0.15 * gs)) + 0.3 * bloom));
                    rgb += 1.0 - exp(-glareAdd);
                }

                #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb = ToSrgb(rgb);
                #endif

                float alpha = cover;
                rgb += (dither - 0.5) * (1.0 / 255.0) * alpha;

                fout o;
                o.col = float4(rgb, alpha);
                return o;
            }
            ENDCG
        }
    }
}
