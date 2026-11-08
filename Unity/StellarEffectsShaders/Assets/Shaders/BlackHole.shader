Shader "Hidden/StellarEffects/BlackHole"
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
            float4 _SE_Disk;
            float4 _SE_Look;
            float4 _SE_Extra;
            float4 _SE_Ray;
            float4 _SE_Right;
            float4 _SE_Up;
            float4 _SE_Forward;
            float4 _SE_Jet;
            float4 _SE_JetB;
            float4 _SE_DiskB;

            static const float DiskDensity = 45.0;
            static const float Absorb = 0.35;
            static const float Emission = 6.0;
            static const float ShearPeriod = 6.2831853;
            static const float CriticalB = 2.598076;
            static const float RingOffset = 0.07;
            static const float JetBase = 3.5;
            static const float JetRamp = 1.2;
            static const float TwistScale = 40.0;

            static const float2 Corners[6] = { float2(0, 0), float2(1, 0), float2(0, 1), float2(0, 1), float2(1, 0), float2(1, 1) };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 ndc : TEXCOORD0;
            };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                float2 ndc = lerp(_SE_Rect.xy, _SE_Rect.zw, Corners[id]);
                o.pos = float4(ndc.x, ndc.y * _ProjectionParams.x, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.ndc = ndc;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                               lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                               lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            float Fbm2(float3 p)
            {
                return (Noise(p) + 0.5 * Noise(p * 2.03 + 1.7) + 0.25 * Noise(p * 4.11 + 3.3)) * (1.0 / 1.75);
            }

            float FbmQ(float3 p, float detail)
            {
                float n = Noise(p);
                if (detail < 0.5) return n;
                n += 0.5 * Noise(p * 2.03 + 1.7);
                if (detail < 1.5) return n * (1.0 / 1.5);
                n += 0.25 * Noise(p * 4.11 + 3.3);
                return n * (1.0 / 1.75);
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

            float3 Aces(float3 x)
            {
                return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
            }

            float3 ToSrgb(float3 c)
            {
                c = max(c, 0.0);
                return lerp(1.055 * pow(c, 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308));
            }

            float3 SoftTonemap(float3 c)
            {
                float l = max(dot(c, float3(0.2126, 0.7152, 0.0722)), 1e-5);
                float lt = 1.0 - exp(-l * 0.9);
                float3 rgb = c / l * lt;
                rgb /= max(max(rgb.r, max(rgb.g, rgb.b)), 1.0);
                float white = smoothstep(0.55, 1.0, lt);
                rgb = lerp(rgb, float3(lt, lt, lt) * float3(1.0, 0.95, 0.88), white * 0.18);
                return rgb * 0.97;
            }

            float InterleavedGradientNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            float JetHalfWidth(float az)
            {
                return _SE_Jet.z + _SE_JetB.z * max(az - JetBase, 0.0);
            }

            void AddJet(float3 x, float r, float3 v, float dt, float3 t, float3 bt,
                        float3 coreColor, float3 tailColor, float transmittance, inout float3 jet)
            {
                float3 n = _SE_Normal.xyz;
                float z = dot(x, n);
                float az = abs(z);
                float jetLength = _SE_Jet.y;
                if (az < JetBase || az > jetLength) return;

                float3 q = x - z * n;
                float w = JetHalfWidth(az);
                float2 qn = float2(dot(q, t), dot(q, bt)) / w;
                float q2 = dot(qn, qn);
                if (q2 > 4.0) return;

                float time = _SE_JetB.y;
                float beta = _SE_JetB.x;

                float radial = sqrt(q2);
                float profile = (1.6 * exp(-5.0 * q2) + 0.22 * exp(-1.1 * q2)) * (1.0 - smoothstep(2.6, 4.0, q2));

                float zn = az / jetLength;
                float axial = exp(-2.2 * zn) * (1.0 + 1.3 * exp(-az / (0.08 * jetLength))) * smoothstep(JetBase, JetBase + JetRamp, az) * (1.0 - smoothstep(0.65, 1.0, zn));

                float twistAngle = _SE_JetB.w * TwistScale * (az - time * beta * 1.2);
                float sn, cs;
                sincos(twistAngle, sn, cs);
                float2 qr = float2(qn.x * cs - qn.y * sn, qn.x * sn + qn.y * cs);
                float3 sp = float3(qr.x * 2.6 + 3.1, qr.y * 2.6 + 7.7, az * 0.4 - time * beta * 1.1);
                float streaks = 0.6 + 0.8 * (0.7 * Noise(sp) + 0.3 * Noise(sp * float3(2.3, 2.3, 1.9) + 5.3));

                float knots = 1.0;
                if (_SE_DiskB.y > 0.5)
                {
                    float braid = 0.5 + 0.5 * cos(3.0 * (atan2(qn.y, qn.x) + twistAngle));
                    streaks *= lerp(1.0, 0.55 + 1.3 * braid * braid, smoothstep(0.1, 0.9, radial));

                    knots = 0.55 + 0.9 * Fbm2(float3(zn * 14.0 - time * beta * 0.8, 4.2, 7.9));
                }

                float3 axis = z >= 0.0 ? n : -n;
                float cosT = dot(axis, -v * rsqrt(dot(v, v)));
                float ratio = 1.0 / (1.0 - beta * cosT);
                float boost = min(pow(ratio, 1.6), 12.0);
                float gg = sqrt(saturate(1.0 - 1.0 / r));

                float heat = saturate(1.0 - zn + 0.3 * (ratio - 1.0));
                float3 c = lerp(tailColor, coreColor, heat);
                jet += transmittance * c * (_SE_Jet.x * 0.2 * (_SE_Jet.z / w) * profile * axial * streaks * knots * boost * gg * gg * gg * dt);
            }

            float DiskScale(float r)
            {
                float t = max(r / _SE_Disk.x, 1.0);
                return 0.5 + 1.3 * exp(-(t - 1.0) * 0.8);
            }

            void AddDisk(float3 x, float rSph, float3 v, float dt, float3 t, float3 bt,
                         inout float3 color, inout float transmittance)
            {
                float3 n = _SE_Normal.xyz;
                float height = dot(x, n);
                float3 q = x - height * n;
                float r = length(q);
                float inner = _SE_Disk.x;
                float outer = _SE_Disk.y;
                if (r <= inner || r >= outer) return;

                float zn = height / max(_SE_Disk.z * DiskScale(r) * r, 1e-4);
                if (abs(zn) > 2.7) return;

                float shape = exp(-zn * zn) * smoothstep(inner, inner * 1.06, r) * (1.0 - smoothstep(outer * 0.55, outer, r));
                if (shape < 0.01) return;

                float s = sqrt(r / (inner * (49.0 / 36.0)));
                float kep = rcp(s * s * s);

                float2 qp = float2(dot(q, t), dot(q, bt));

                float spin = (_SE_Normal.w < 0.0 ? -1.0 : 1.0) * _SE_DiskB.x;
                float u = abs(_SE_Look.z) * (1.0 / ShearPeriod);
                float f0 = frac(u);
                float f1 = frac(u + 0.5);
                float w0 = 1.0 - abs(2.0 * f0 - 1.0);
                float w1 = 1.0 - w0;

                float n0 = 0.0;
                float n1 = 0.0;
                if (w0 > 0.01)
                {
                    float sn0, cs0;
                    sincos(f0 * ShearPeriod * kep * spin, sn0, cs0);
                    float3 p0 = float3(qp.x * cs0 + qp.y * sn0, qp.y * cs0 - qp.x * sn0, zn * 0.5) * 0.9 + 9.1;
                    n0 = FbmQ(p0, _SE_DiskB.y);
                }
                if (w1 > 0.01)
                {
                    float sn1, cs1;
                    sincos(f1 * ShearPeriod * kep * spin, sn1, cs1);
                    float3 p1 = float3(qp.x * cs1 + qp.y * sn1, qp.y * cs1 - qp.x * sn1, zn * 0.5) * 0.9 + 9.1;
                    n1 = FbmQ(p1, _SE_DiskB.y);
                }
                float heat = saturate((n0 * w0 + n1 * w1 - 0.5) * rsqrt(w0 * w0 + w1 * w1) * 1.35 + 0.5);
                float ringFine = _SE_DiskB.y > 0.5 ? 0.12 * Noise(float3(r * 23.0, 8.1, 2.9)) : 0.06;
                float rings = 0.78 + 0.22 * Noise(float3(r * 7.0, 3.7, 1.3)) + ringFine;
                heat = saturate(heat * rings);

                float knots = smoothstep(0.45, 0.75, heat) * _SE_Disk.w;
                float density = shape * (0.6 + 0.4 * heat) * (DiskDensity / r);

                float3 orbit = cross(n, q) * (_SE_Normal.w / r);
                float beta = min(sqrt(0.5 / max(r - 1.0, 0.05)), 0.95);
                float doppler = sqrt(1.0 - beta * beta) / (1.0 - beta * dot(orbit, -v * rsqrt(dot(v, v))));
                float g = min(doppler, 1.8) * sqrt(saturate(1.0 - 1.0 / rSph));

                float profile = sqrt(sqrt(max(7.0 * (1.0 - sqrt(inner / r)), 0.0))) * sqrt(kep);
                float edgeGlow = 1.0 + 0.45 * exp(-(r - inner) / (0.28 * inner));
                float tk = profile * (0.9 + 0.3 * knots) * edgeGlow;

                float3 hot = Vivid(Blackbody(_SE_Look.x * tk * g), _SE_Extra.x);
                float tk2 = tk * tk;
                float g2 = g * g;
                float3 thermal = hot * (tk2 * tk2 * Emission * (0.6 + 1.4 * knots) * (0.55 + 0.45 * heat));

                float absorb = Absorb * (1.0 - 0.55 * smoothstep(inner * 1.5, outer, r));

                color += transmittance * thermal * (g2 * g2 * density * dt);
                transmittance *= exp(-density * absorb * dt);
            }

            struct fout
            {
                float4 col : SV_Target;
                float depth : SV_Depth;
            };

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

            fout frag(v2f i)
            {
                float2 ndc = i.ndc;
                float2 uv = ndc * 0.5 + 0.5;
                float3 forward = _SE_Forward.xyz;
                float3 v = normalize(forward + _SE_Right.xyz * ndc.x * _SE_Ray.x + _SE_Up.xyz * ndc.y * _SE_Ray.y);
                float viewCos = dot(v, forward);

                float rs = _SE_Center.w;
                float3 x = -_SE_Center.xyz / rs;
                float bound = _SE_Ray.z;

                float b = dot(x, v);
                float disc = b * b - (dot(x, x) - bound * bound);
                if (disc < 0.0) return Empty();
                float s = sqrt(disc);
                float tEnter = max(0.0, -b - s);
                if (-b + s < 0.0) return Empty();

                float sceneDistance = 1e30;
                if (_SE_Ray.w > 0.5 && _SE_Look.w < 1.5)
                {
                    float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                    #if defined(UNITY_REVERSED_Z)
                        bool sky = rawDepth <= 0.0;
                    #else
                        bool sky = rawDepth >= 1.0;
                    #endif
                    if (!sky)
                        sceneDistance = LinearEyeDepth(rawDepth) / max(dot(v, forward), 1e-4) / rs;
                }
                if (sceneDistance < tEnter) return Empty();

                float camDist = sqrt(dot(x, x));
                float3 toBlackHole = -x / max(camDist, 1e-4);
                bool shadowRay = b < 0.0 && camDist > 1.5;
                float edgeW = 0.7 * camDist * 2.0 * _SE_Ray.y / _ScreenParams.y;

                x += v * tEnter;
                float traveled = tEnter;
                float3 hv = cross(x, v);
                float h2 = dot(hv, hv);
                float k = -1.5 * h2;
                float invB = rsqrt(h2 + 1e-4);
                float r0 = dot(x, x);
                float3 acc = k * x / (r0 * r0 * sqrt(r0));

                float3 n = _SE_Normal.xyz;
                float3 t = normalize(cross(n, abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 bt = cross(n, t);

                float3 glowOffset = 0;
                if (_SE_DiskB.w > 0.5)
                {
                    float3 towardViewer = -toBlackHole;
                    towardViewer -= n * dot(towardViewer, n);
                    if (dot(towardViewer, towardViewer) > 1e-6)
                        glowOffset = _SE_Normal.w * normalize(cross(towardViewer, n)) * max(_SE_Disk.x, 1.5) * 0.12;
                }
                float3 v0 = v;

                float peak = _SE_Look.x;
                float3 glowColor = Vivid(Blackbody(peak * 0.6), _SE_Extra.x);
                float glowGain = _SE_Extra.y * 0.03;
                float thickness = _SE_Disk.z;
                float diskReach = _SE_Disk.y * 1.2;

                bool jetOn = _SE_Jet.x > 0.001;
                float jetReach = _SE_Jet.y;
                float3 jetCore = 0;
                float3 jetTail = 0;
                float3 jet = 0;
                if (jetOn)
                {
                    jetCore = Vivid(Blackbody(_SE_Jet.w), 1.3);
                    jetTail = Vivid(Blackbody(_SE_Jet.w * 0.7), 1.3);
                }

                float3 color = 0;
                float3 glow = 0;
                float transmittance = 1.0;
                bool captured = false;
                bool escaped = true;
                bool hitScene = false;
                float noiseDither = InterleavedGradientNoise(i.pos.xy);
                float jitter = 0.2 + 0.8 * noiseDither;
                int maxSteps = (int)_SE_Extra.w;
                float depthT = -1.0;
                float tExit = min(-b + s, sceneDistance);

                [loop] for (int iter = 0; iter < maxSteps; iter++)
                {
                    float r2 = dot(x, x);
                    float r = sqrt(r2);
                    if (r < 1.0) { captured = true; escaped = false; break; }
                    if (r > bound * 1.001 && dot(x, v) > 0.0) break;
                    if (traveled > sceneDistance) { escaped = false; hitScene = true; break; }

                    float nearPlane = abs(dot(x, n));
                    float hs = thickness * DiskScale(r);
                    float layer = max(nearPlane, 0.3 * hs * r);
                    float vn = abs(dot(v, n));
                    float dt = 0.08 * r * (1.0 + 1.5 * saturate((r - 4.0) * 0.25));
                    dt = min(dt, 0.35 * layer / max(vn, 0.12));
                    if (jetOn)
                    {
                        float jw = JetHalfWidth(nearPlane);
                        float jrad = sqrt(max(r2 - nearPlane * nearPlane, 0.0));
                        float dRad = max(jrad - 2.0 * jw, 0.0);
                        float dAx = max(nearPlane - jetReach, 0.0);
                        dt = min(dt, max(0.6 * sqrt(dRad * dRad + dAx * dAx), 0.22 * jw));
                    }
                    dt = max(min(dt, 0.08 * r2 * invB), 0.004);
                    bool inDiskLayer = nearPlane < 5.0 * hs * r && r < diskReach;
                    if (inDiskLayer) dt = min(dt, 0.25);
                    else dt = max(dt, 0.7 * max(tExit - traveled, 0.0) / max((float)(maxSteps - iter), 1.0));
                    dt *= _SE_DiskB.z;
                    if (iter == 0) dt *= jitter;
                    dt = min(dt, max(sceneDistance - traveled, 1e-5));

                    float rg = _SE_DiskB.w > 0.5 ? length(x - glowOffset) : r;
                    glow += transmittance * glowColor * (glowGain * exp2((1.0 - rg) * 0.6558) * saturate(1.0 - 1.0 / max(rg, 1.0)) * dt);
                    AddDisk(x, r, v, dt, t, bt, color, transmittance);
                    if (jetOn) AddJet(x, r, v, dt, t, bt, jetCore, jetTail, transmittance, jet);
                    if (depthT < 0.0 && (transmittance < 0.75 || max(jet.r, max(jet.g, jet.b)) > 0.12)) depthT = traveled;
                    if (transmittance < 0.02) { escaped = false; break; }

                    float3 xn = x + (v + 0.5 * dt * acc) * dt;
                    float rn2 = max(dot(xn, xn), 1e-6);
                    float3 accn = k * xn / (rn2 * rn2 * sqrt(rn2));
                    v += 0.5 * dt * (acc + accn);
                    x = xn;
                    acc = accn;
                    traveled += dt * length(v);
                }

                float hb = sqrt(h2);
                if (!captured) color += glow;

                if (!hitScene && b < 0.0)
                {
                    float ringB = CriticalB - RingOffset;
                    float d = (hb - ringB) / 0.04;
                    float d2 = (hb - ringB) / 0.011;
                    float3 ringCol = Vivid(Blackbody(peak), _SE_Extra.x);
                    float3 ringHot = lerp(ringCol, float3(1.0, 0.95, 0.88), 0.45);
                    color += ringCol * (_SE_Extra.z * 6.5 * exp(-d * d));
                    color += ringHot * (_SE_Extra.z * 5.0 * exp(-d2 * d2));
                }

                float3 rgb = SoftTonemap(color * _SE_Look.y);
                rgb += 1.0 - exp(-jet);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb = ToSrgb(rgb);
                #endif
                float shadow = captured ? 1.0 : 0.0;
                if (shadowRay && (captured || escaped))
                    shadow = 1.0 - smoothstep(CriticalB - edgeW, CriticalB + edgeW, sqrt(h2));
                float alpha = 1.0 - transmittance * (1.0 - shadow);

                if (!hitScene && b < 0.0)
                {
                    float halo = exp(-max(CriticalB - hb, 0.0) * 1.8) * exp(-max(hb - CriticalB, 0.0) * 1.2);
                    float3 haloColor = Vivid(Blackbody(peak * 0.75), _SE_Extra.x) * 0.9;
                    rgb += haloColor * (halo * min(_SE_Extra.y, 2.0) * 0.25 * transmittance);
                    float hbWide = _SE_DiskB.w > 0.5 ? length(hv - cross(glowOffset, v0)) : hb;
                    float wide = exp(-max(hbWide - CriticalB, 0.0) * 0.35) * saturate(1.0 - 1.0 / max(hbWide, 1.0));
                    rgb += haloColor * (wide * min(_SE_Extra.y, 2.0) * 0.035 * transmittance);
                }

                rgb += (noiseDither - 0.5) * (1.0 / 255.0) * alpha;

                if (depthT < 0.0 && (captured || hitScene)) depthT = traveled;
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
