Shader "Custom/ShoulderTint"
{
    Properties
    {
        _MainTex ("Shoulder Tile", 2D) = "white" {}
        _ScrollWorld ("Scroll World", Float) = 0
        _SegOrigin ("Segment Origin", Float) = 500
        _Seed ("Seed", Float) = 0
        _MinSolid ("Min Solid Run", Float) = 40
        _MaxSolid ("Max Solid Run", Float) = 96
        _MinTrans ("Min Transition", Float) = 0.5
        _MaxTrans ("Max Transition", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ScrollWorld;
            float _SegOrigin;
            float _Seed;
            float _MinSolid;
            float _MaxSolid;
            float _MinTrans;
            float _MaxTrans;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float along : TEXCOORD1;
            };

            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + _Seed) * 43758.5453);
            }

            float PickColor(float seg)
            {
                return floor(Hash(seg * 3.17) * 3.0);
            }

            float NextColor(float cur, float seg)
            {
                float a = cur + 1.0;
                if (a >= 3.0)
                    a -= 3.0;
                float b = cur + 2.0;
                if (b >= 3.0)
                    b -= 3.0;
                return Hash(seg * 7.91 + 2.3) > 0.5 ? a : b;
            }

            float3 ApplyTint(float3 rgb, float tintIdx)
            {
                float brightness = 1.0;
                float warmth = 0.0;
                if (tintIdx >= 1.5)
                {
                    brightness = 0.88;
                    warmth = -0.06;
                }
                else if (tintIdx >= 0.5)
                {
                    brightness = 1.14;
                    warmth = 0.10;
                }

                float r = rgb.r * brightness * (1.0 + warmth * 0.5);
                float g = rgb.g * brightness;
                float b = rgb.b * brightness * (1.0 - warmth * 0.5);
                return saturate(float3(r, g, b));
            }

            float3 GetTint(float along)
            {
                float pos = 0.0;
                float cur = PickColor(0.0);

                [loop]
                for (int i = 0; i < 48; i++)
                {
                    float seg = (float)i;
                    float runLen = lerp(_MinSolid, _MaxSolid, Hash(seg + 0.1));
                    float transLen = lerp(_MinTrans, _MaxTrans, Hash(seg + 5.7));

                    if (along < pos + runLen)
                        return ApplyTint(float3(1.0, 1.0, 1.0), cur);

                    if (along < pos + runLen + transLen)
                    {
                        float nxt = NextColor(cur, seg);
                        float t = (along - pos - runLen) / max(transLen, 0.001);
                        t = smoothstep(0.0, 1.0, t);
                        float3 a = ApplyTint(float3(1.0, 1.0, 1.0), cur);
                        float3 b = ApplyTint(float3(1.0, 1.0, 1.0), nxt);
                        return lerp(a, b, t);
                    }

                    pos += runLen + transLen;
                    cur = NextColor(cur, seg);
                }

                return ApplyTint(float3(1.0, 1.0, 1.0), cur);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.along = world.z + _ScrollWorld + _SegOrigin;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);
                float3 tint = GetTint(i.along);
                return fixed4(baseCol.rgb * tint, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
