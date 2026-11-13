Shader "Hidden/StellarEffects/Aurora"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
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
            float4 _SE_Center;   // xyz: toCenter, w: planet radius (scaled)
            float4 _SE_Normal;   // xyz: pole axis
            float4 _SE_Look;     // x: brightness, y: ring rotation, z: shimmer time, w: ray scale
            float4 _SE_Shell;    // x: altitude, y: height, z: ray drop, w: steps
            float4 _SE_Ring;     // x: colatitude, y: sigma, z: wobble, w: contrast
            float4 _SE_Band;     // x: cosMin, y: cosMax, z: seed, w: noise detail
            float4 _SE_Ray;      // x: tanX, y: tanY
            float4 _SE_Right;
            float4 _SE_Up;
            float4 _SE_Forward;
            float4 _SE_Low;
            float4 _SE_High;
            float4 _SE_Base;

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

            float FbmQ(float3 p, float detail)
            {
                float n = Noise(p);
                if (detail < 0.5) return n;
                n += 0.5 * Noise(p * 2.03 + 1.7);
                if (detail < 1.5) return n * (1.0 / 1.5);
                n += 0.25 * Noise(p * 4.11 + 3.3);
                return n * (1.0 / 1.75);
            }

            float3 ToSrgb(float3 c)
            {
                c = max(c, 0.0);
                return lerp(1.055 * pow(c, 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308));
            }

            float InterleavedGradientNoise(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            bool Sphere(float3 x, float3 v, float radius, out float t0, out float t1)
            {
                float b = dot(x, v);
                float d = b * b - (dot(x, x) - radius * radius);
                t0 = 0.0;
                t1 = 0.0;
                if (d < 0.0) return false;
                float s = sqrt(d);
                t0 = -b - s;
                t1 = -b + s;
                return true;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 ndc = i.ndc;
                float2 uv = ndc * 0.5 + 0.5;
                float3 forward = _SE_Forward.xyz;
                float3 v = normalize(forward + _SE_Right.xyz * ndc.x * _SE_Ray.x + _SE_Up.xyz * ndc.y * _SE_Ray.y);
                float viewDot = max(dot(v, forward), 1e-4);

                float rs = _SE_Center.w;
                float3 x = -_SE_Center.xyz / rs;

                float alt0 = _SE_Shell.x;
                float height = _SE_Shell.y;
                float drop = _SE_Shell.z;
                float rOuter = 1.0 + alt0 + height;
                float rInner = 1.0 + max(alt0 - drop, 0.0);

                float tO0, tO1;
                if (!Sphere(x, v, rOuter, tO0, tO1) || tO1 < 0.0) return 0;

                float tI0, tI1;
                bool hitInner = Sphere(x, v, rInner, tI0, tI1) && tI1 > 0.0;

                float tP0, tP1;
                bool hitPlanet = Sphere(x, v, 1.0, tP0, tP1) && tP0 > 0.0;

                float sceneDistance = 1e30;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                #if defined(UNITY_REVERSED_Z)
                    bool sky = rawDepth <= 0.0;
                #else
                    bool sky = rawDepth >= 1.0;
                #endif
                if (!sky) sceneDistance = LinearEyeDepth(rawDepth) / viewDot / rs;

                float tMax = sceneDistance;
                if (hitPlanet) tMax = min(tMax, tP0);

                float a0 = max(tO0, 0.0);
                float a1 = min(hitInner ? tI0 : tO1, tMax);
                float lenA = max(a1 - a0, 0.0);
                float b0 = hitInner ? tI1 : 0.0;
                float b1 = min(tO1, tMax);
                float lenB = hitInner ? max(b1 - b0, 0.0) : 0.0;

                float total = lenA + lenB;
                if (total <= 1e-5) return 0;

                float3 n = normalize(_SE_Normal.xyz);
                float3 t = normalize(cross(n, abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 bt = cross(n, t);

                float sa, ca;
                sincos(_SE_Look.y, sa, ca);

                float seed = _SE_Band.z;
                float detail = _SE_Band.w;
                float shimmer = _SE_Look.z;
                float rayScale = _SE_Look.w;

                int steps = (int)_SE_Shell.w;
                float ds = total / steps;
                float jit = InterleavedGradientNoise(i.pos.xy);

                float3 color = 0;

                [loop] for (int k = 0; k < steps; k++)
                {
                    float s = (k + jit) * ds;
                    float tt = s < lenA ? a0 + s : b0 + (s - lenA);
                    float3 p = x + v * tt;
                    float r = length(p);
                    float3 d = p / r;
                    float ct = dot(d, n);
                    float act = abs(ct);
                    if (act < _SE_Band.x || act > _SE_Band.y) continue;

                    float h = r - 1.0;
                    float2 rc = float2(dot(d, t), dot(d, bt));
                    float phi = atan2(rc.y, rc.x) + _SE_Look.y;
                    float2 cp = float2(cos(phi), sin(phi));
                    float side = ct > 0.0 ? 1.0 : 9.3;

                    // smooth low-frequency ring shape: always one continuous oval
                    float lobes = FbmQ(float3(cp * 1.4 + seed, side + shimmer * 0.02), 1.0);
                    float colat = acos(min(act, 1.0));
                    float c0 = _SE_Ring.x + _SE_Ring.z * clamp((lobes - 0.5) * 3.0, -1.0, 1.0);
                    float dc = (colat - c0) / _SE_Ring.y;
                    float ringW = exp(-dc * dc);
                    if (ringW < 0.01) continue;

                    // gentle brightness variation along the oval, never fully dark
                    float patch = 0.7 + 0.3 * Noise(float3(cp * 2.2 + seed * 1.3 + 11.0, side + shimmer * 0.05));

                    // vertical rays: noise depends on longitude (plus a slight height lean), so each ray is a column
                    float rr = rayScale * 0.159;
                    float3 np = float3(cp * rr + seed, shimmer + h * 6.0);
                    float rayN = FbmQ(np, detail);
                    float lo = lerp(0.2, 0.4, _SE_Ring.w);
                    float hi = lerp(0.8, 0.6, _SE_Ring.w);
                    float rays = 0.25 + 0.75 * smoothstep(lo, hi, rayN);
                    rays *= 0.8 + 0.4 * Noise(np * 2.7 + 4.1);
                    rays = saturate(rays);

                    float hb = max(alt0 - drop * rays * rays, 0.0);
                    float w = (h - hb) / height;
                    if (w <= 0.0) continue;

                    float topFade = 1.0 - smoothstep(alt0 + height * 0.7, alt0 + height, h);
                    float eg = smoothstep(0.0, 0.06, w) * exp(-w * 3.2);
                    float er = smoothstep(0.12, 0.55, w) * exp(-w * 1.4);
                    float eb = exp(-w * 40.0) * smoothstep(0.0, 0.01, w);

                    float3 em = _SE_Low.rgb * (eg * 1.6) + _SE_High.rgb * (er * 0.55) + _SE_Base.rgb * (eb * 0.25);
                    float inten = ringW * patch * rays * (0.25 + 0.75 * rays) * topFade;
                    color += em * (inten * ds / height);
                }

                float3 rgb = 1.0 - exp(-color * _SE_Look.x);
                rgb += (jit - 0.5) * (1.0 / 255.0) * saturate(max(rgb.r, max(rgb.g, rgb.b)) * 50.0);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    rgb = ToSrgb(rgb);
                #endif

                return float4(rgb, 0.0);
            }
            ENDCG
        }
    }
}
