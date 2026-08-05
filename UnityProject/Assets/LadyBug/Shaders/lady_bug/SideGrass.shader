Shader "lady_bug/SideGrass"
{
    Properties
    {
        _Color ("Tint", Color) = (0.55, 0.72, 0.35, 1)
        _MainTex ("Grass", 2D) = "white" {}
        _StrokeRotation ("Upright rotation (deg)", Float) = -44
        _PerspectiveSkew ("Along-road perspective skew", Float) = 0.38
        [HideInInspector] _SideSign ("Side mirror (+1 left, -1 right)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _StrokeRotation;
            float _PerspectiveSkew;
            float _SideSign;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float objZ : TEXCOORD1;
            };

            float2 UprightGrassUV(float2 uv, float objZ)
            {
                // Rotate inside each tile — global pivot (0.5) is wrong when UV spans dozens of repeats.
                float2 tile = floor(uv);
                float2 local = frac(uv) - 0.5;
                float side = _SideSign >= 0.0 ? 1.0 : -1.0;
                local.x += _PerspectiveSkew * side * objZ * local.y;
                float rad = (_StrokeRotation * side) * 0.01745329251;
                float s = sin(rad);
                float c = cos(rad);
                local = float2(c * local.x - s * local.y, s * local.x + c * local.y);
                return tile + local + 0.5;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.objZ = v.vertex.z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, UprightGrassUV(i.uv, i.objZ)) * _Color;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
