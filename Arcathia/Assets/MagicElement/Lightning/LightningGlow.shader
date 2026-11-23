// Procedural additive glow (no texture needed).
//  - Billboard ON  : use on a Quad -> always faces the camera (impact flash at the base of the bolt)
//  - Billboard OFF : use on a ParticleSystem renderer (sparks); particle colour / alpha tint and fade it
Shader "VFX/LightningGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.25, 0.6, 1, 1)
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.2
        _Power ("Halo Falloff", Range(0.5, 6)) = 2
        _CorePower ("Core Falloff", Range(1, 16)) = 8
        [Toggle] _Billboard ("Billboard (for quads)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+11" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            fixed4 _Color, _CoreColor;
            float _Intensity, _Power, _CorePower, _Billboard;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 col : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                if (_Billboard > 0.5)
                {
                    float3 center = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                    float sx = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                    float sy = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float3 wp = center + right * (v.vertex.x * sx) + up * (v.vertex.y * sy);
                    o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                }
                else
                {
                    o.pos = UnityObjectToClipPos(v.vertex);
                }
                o.uv = v.uv;
                o.col = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                float halo = pow(a, _Power);
                float core = pow(a, _CorePower);

                float3 c = (_Color.rgb * halo + _CoreColor.rgb * core) * _Intensity;
                c *= i.col.rgb * i.col.a;     // particle colour + fade-out via alpha (additive)
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
