// Lightning bolt ribbon shader (one mesh = whole bolt incl. branches = 1 draw call).
// The mesh is built by LightningBolt.cs; this shader makes every segment face the camera (billboard in the
// vertex shader), so the bolt looks right from any angle without rebuilding the mesh each frame.
//
// Vertex data written by the script:
//   position = centre line point
//   normal   = path tangent (used to expand the ribbon sideways)
//   uv.x     = reveal parameter along the strike (0 start .. 1 end)   -> strike animation
//   uv.y     = 0 / 1 (which side of the ribbon)                         -> glow profile across the width
//   uv.z     = 0..1 along the current branch                            -> branch tips fade out
//   color.r  = width multiplier   color.g = intensity multiplier   color.b = 1 for branches
//
// Look (from the reference): white-hot core, bright cyan-blue inner glow, deep blue outer halo. Additive blend.
Shader "VFX/LightningBolt"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _GlowColor ("Inner Glow Color", Color) = (0.35, 0.75, 1, 1)
        _HaloColor ("Outer Halo Color", Color) = (0.05, 0.25, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.4

        [Header(Shape)]
        _Width ("Width (world units)", Float) = 0.3
        _CoreWidth ("Core Width", Range(0.02, 0.6)) = 0.16
        _InnerPower ("Inner Glow Falloff", Range(0.5, 8)) = 3
        _HaloPower ("Halo Falloff", Range(0.5, 6)) = 1.6
        _HaloStrength ("Halo Strength", Range(0, 2)) = 0.7
        _Crackle ("Crackle Along Length", Range(0, 0.5)) = 0.15

        [Header(Driven by script)]
        _Reveal ("Reveal", Range(0, 1)) = 1
        _WidthScale ("Width Scale", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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

            fixed4 _CoreColor, _GlowColor, _HaloColor;
            float _Intensity, _Width, _CoreWidth, _InnerPower, _HaloPower, _HaloStrength, _Crackle;
            float _Reveal, _WidthScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD0;
                fixed4 col : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;

                float3 posW = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float3 tanW = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));

                // direction toward the camera (works for perspective and orthographic cameras)
                float3 viewDir = lerp(_WorldSpaceCameraPos - posW, UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w);

                // ribbon side = perpendicular to both the bolt direction and the view direction
                float3 side = cross(tanW, viewDir);
                side = side / max(length(side), 1e-4);

                float sgn = v.uv.y * 2.0 - 1.0;
                posW += side * (sgn * 0.5 * _Width * _WidthScale * v.color.r);

                o.pos = mul(UNITY_MATRIX_VP, float4(posW, 1.0));
                o.uv = v.uv;
                o.col = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 0 = centre of the ribbon, 1 = edge
                float t = abs(i.uv.y * 2.0 - 1.0);

                float core = 1.0 - smoothstep(0.0, _CoreWidth, t);
                float inner = pow(saturate(1.0 - t), _InnerPower);
                float halo = pow(saturate(1.0 - t), _HaloPower) * _HaloStrength;

                // strike animation: the bolt is drawn from the start toward the end, with a bright head
                float front = _Reveal * 1.05;
                float vis = saturate((front - i.uv.x) / 0.04);
                float head = (_Reveal < 0.999) ? saturate(1.0 - (front - i.uv.x) / 0.1) : 0.0;

                // branch tips fade out (main bolt has col.b = 0 so it is unaffected)
                float tip = lerp(1.0, 1.0 - smoothstep(0.45, 1.0, i.uv.z), i.col.b);

                // electric shimmer along the length
                float crackle = 1.0 + _Crackle * sin(i.uv.x * 140.0 + _Time.y * 80.0);

                float inten = _Intensity * i.col.g * vis * tip * crackle * (1.0 + head * 1.5);
                float3 c = (_CoreColor.rgb * core + _GlowColor.rgb * inner + _HaloColor.rgb * halo) * inten;
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
