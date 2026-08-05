Shader "Custom/ShoulderTint"
{
    Properties
    {
        _MainTex ("Shoulder Tile", 2D) = "white" {}
        [Toggle] _RoadEdgeAtHighU ("Road Edge At High U", Float) = 1
        _EdgeInset ("Road-edge inset (UV width)", Range(0, 0.25)) = 0
        _EdgeWaveAmp ("Road-edge wave amplitude (UV)", Range(0, 0.2)) = 0.04
        _EdgeWaveFreq ("Road-edge waves per strip length", Float) = 5
        _EdgeAmpVar ("Per-wave height variation", Range(0, 1)) = 1
        _EdgeSoftness ("Road-edge alpha feather (UV)", Range(0, 0.08)) = 0.032
        _EdgeBlurRadius ("Alpha blur radius at zone center (UV)", Range(0, 0.05)) = 0.014
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _RoadEdgeAtHighU;
            float _EdgeInset;
            float _EdgeWaveAmp;
            float _EdgeWaveFreq;
            float _EdgeAmpVar;
            float _EdgeSoftness;
            float _EdgeBlurRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float WaveAmpScale(float2 uv, float seed)
            {
                float cell = uv.y * _EdgeWaveFreq;
                float i = floor(cell);
                float f = frac(cell);
                f = f * f * (3.0 - 2.0 * f);
                float r0 = Hash11(i * 12.9898 + 78.233 + seed);
                float r1 = Hash11((i + 1.0) * 12.9898 + 78.233 + seed);
                return lerp(r0, r1, f);
            }

            // x = signed distance past wavy edge (positive = visible strip)
            // y = half-width of the fluctuation band for blur falloff
            float2 StripEdgeMaskAndZone(float2 uv, float edgeAtHighU, float seed)
            {
                float edgeDist = edgeAtHighU > 0.5 ? (1.0 - uv.x) : uv.x;
                float cell = uv.y * _EdgeWaveFreq;
                float waveIndex = floor(cell);
                float phase = (Hash11(waveIndex * 41.17 + 9.3 + seed) - 0.5) * 0.45;
                float t = cell + phase;
                float s = sin(t * 6.2831853);
                float wave = sign(s) * pow(abs(s), 0.82);
                float rnd = WaveAmpScale(uv, seed);
                float rnd2 = Hash11(waveIndex * 23.45 + 5.1 + seed);
                float slowRnd = Hash11(floor(uv.y * 1.3) * 31.1 + 2.7 + seed);
                float ampScale = lerp(0.12, 2.15, rnd);
                ampScale *= lerp(0.55, 1.55, slowRnd);
                float insetJitter = lerp(-0.012, 0.02, rnd2);
                float ampMix = lerp(1.0, ampScale, _EdgeAmpVar);
                float boundary = _EdgeInset + insetJitter * _EdgeAmpVar + _EdgeWaveAmp * ampMix * wave;
                float mask = edgeDist - boundary;
                float zoneHalf = _EdgeWaveAmp * ampMix + _EdgeSoftness + abs(insetJitter) * _EdgeAmpVar;
                return float2(mask, zoneHalf);
            }

            float AlphaFromMask(float mask)
            {
                return smoothstep(0.0, _EdgeSoftness, mask);
            }

            float AlphaAt(float2 uv)
            {
                float2 road = StripEdgeMaskAndZone(uv, _RoadEdgeAtHighU, 0.0);
                float2 grass = StripEdgeMaskAndZone(uv, 1.0 - _RoadEdgeAtHighU, 31.7);
                float mask = min(road.x, grass.x);
                return AlphaFromMask(mask);
            }

            float FluctuationBlurWeight(float2 edgeInfo)
            {
                return 1.0 - smoothstep(0.0, max(edgeInfo.y, 1e-4), abs(edgeInfo.x));
            }

            float CombinedBlurWeight(float2 uv)
            {
                float2 road = StripEdgeMaskAndZone(uv, _RoadEdgeAtHighU, 0.0);
                float2 grass = StripEdgeMaskAndZone(uv, 1.0 - _RoadEdgeAtHighU, 31.7);
                return max(FluctuationBlurWeight(road), FluctuationBlurWeight(grass));
            }

            float BlurAlphaAlongStrip(float2 uv, float blurW)
            {
                float r = _EdgeBlurRadius * blurW;
                float a = AlphaAt(uv) * 0.26;
                a += AlphaAt(uv + float2(-3.0 * r, 0.0)) * 0.07;
                a += AlphaAt(uv + float2(-2.0 * r, 0.0)) * 0.11;
                a += AlphaAt(uv + float2(-r, 0.0)) * 0.18;
                a += AlphaAt(uv + float2(r, 0.0)) * 0.18;
                a += AlphaAt(uv + float2(2.0 * r, 0.0)) * 0.11;
                a += AlphaAt(uv + float2(3.0 * r, 0.0)) * 0.07;
                return a;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float sharpAlpha = AlphaAt(i.uv);
                float blurW = CombinedBlurWeight(i.uv);
                col.a = sharpAlpha;
                if (blurW > 0.001)
                {
                    float blurredAlpha = BlurAlphaAlongStrip(i.uv, blurW);
                    col.a = lerp(sharpAlpha, blurredAlpha, blurW);
                }
                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
