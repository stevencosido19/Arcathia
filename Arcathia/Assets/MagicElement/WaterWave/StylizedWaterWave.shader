// Stylized cel-shaded water wave for projectiles.
// - 3 texture fetches (ramp + 2 noise lookups), no grab pass, no depth write, no lighting.
// - Works in Built-in and URP (untagged unlit pass renders in both).
// Mesh UV convention: u = along the crescent, v = 0 (inner edge) -> 1 (outer foam edge)
// Vertex color: R = layer brightness shift, G = foam strength
Shader "VFX/StylizedWaterWave"
{
    Properties
    {
        _RampTex ("Color Ramp (cel bands)", 2D) = "white" {}
        _NoiseTex ("Noise (R=fingers, G=streaks)", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _Intensity ("Intensity (HDR boost)", Range(0.5, 3)) = 1.15

        [Header(Shape)]
        _Edge ("Outer Edge", Range(0.3, 1)) = 0.72
        _Soft ("Edge Softness", Range(0.002, 0.2)) = 0.03
        _Distort ("Finger Length", Range(0, 1.5)) = 0.6
        _Contrast ("Noise Contrast", Range(0.5, 4)) = 1.5
        _InnerRag ("Inner Edge Raggedness", Range(0, 0.5)) = 0.15
        _NoiseScale ("Finger Noise Scale (u,v)", Vector) = (6, 1.2, 0, 0)
        _StreakScale ("Streak Noise Scale (u,v)", Vector) = (2.5, 0.8, 0, 0)
        _Scroll ("Scroll Speed (u,v)", Vector) = (0.12, -0.6, 0, 0)

        [Header(Color)]
        _LayerShift ("Layer Brightness Shift", Range(0, 1)) = 0.55
        _Band ("Band Noise", Range(0, 0.6)) = 0.18
        _StreakCut ("Streak Threshold", Range(0, 1)) = 0.62
        _StreakStrength ("Streak Strength", Range(0, 1)) = 0.8

        [Header(Depth Shading)]
        _LightDir ("Fake Light Direction (world)", Vector) = (0.3, 0.8, -0.5, 0)
        _ShadeAmount ("Form Shading", Range(0, 0.8)) = 0.3
        _FresnelStrength ("Rim / Fresnel Brightness", Range(0, 1)) = 0.35

        [Header(Animation)]
        _WobbleAmp ("Wobble Amount", Range(0, 0.3)) = 0.05
        _WobbleSpeed ("Wobble Speed", Range(0, 20)) = 6

        [Header(Runtime Driven)]
        _Progress ("Dissolve Progress", Range(0, 1)) = 0
        _Alpha ("Global Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _RampTex;
            sampler2D _NoiseTex;
            half4 _Tint;
            half4 _FoamColor;
            half _Intensity;
            half _Edge, _Soft, _Distort, _Contrast, _InnerRag;
            float4 _NoiseScale, _StreakScale, _Scroll;
            half _LayerShift, _Band, _StreakCut, _StreakStrength;
            half _WobbleAmp, _WobbleSpeed;
            half _Progress, _Alpha;
            float4 _LightDir;
            half _ShadeAmount, _FresnelStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 col : COLOR;
                half3 nrm : TEXCOORD1;
                half3 view : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 p = v.vertex.xyz;
                // cheap vertex ripple, strongest on the foam edge
                float w = sin(_Time.y * _WobbleSpeed + v.uv.x * 12.566 + v.color.r * 3.0) * _WobbleAmp * v.uv.y;
                p.z += w;
                p.y += w * 0.5;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                o.col = v.color;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                float3 wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.view = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 scroll = _Scroll.xy * _Time.y;
                float2 uvA = i.uv * _NoiseScale.xy + scroll;
                float2 uvB = i.uv * _StreakScale.xy + scroll * 0.6 + float2(i.col.r * 0.37, i.col.r * 0.21);

                half n1 = tex2D(_NoiseTex, uvA).r;
                half n2 = tex2D(_NoiseTex, uvB).g;
                n1 = saturate((n1 - 0.5) * _Contrast + 0.5);

                half v = i.uv.y;

                // ragged outer edge -> splash "fingers"
                half d = v + (n1 - 0.5) * _Distort * v;
                half outer = saturate((_Edge - d) / _Soft);
                half inner = saturate((v + (n2 - 0.5) * _InnerRag) / 0.06);

                // dissolve (driven by controller)
                half cut = lerp(-0.15, 1.1, _Progress);
                half diss = saturate((n1 - cut) / 0.08);

                half alpha = outer * inner * diss * _Alpha;
                clip(alpha - 0.003);

                // cel colour from ramp
                // depth cues: two-sided cel light + rim, both just shift the ramp lookup
                half3 nrm = normalize(i.nrm);
                half3 vdir = normalize(i.view);
                half ndv = dot(nrm, vdir);
                nrm *= (ndv < 0.0) ? -1.0 : 1.0;           // Cull Off -> flip back faces toward camera
                ndv = abs(ndv);
                half ndl = dot(nrm, normalize(_LightDir.xyz));
                half fres = 1.0 - ndv;
                fres *= fres;

                half r = saturate(d / _Edge + (i.col.r - 0.5) * _LayerShift + (n2 - 0.5) * _Band
                                  + ndl * _ShadeAmount + fres * _FresnelStrength);
                half3 col = tex2D(_RampTex, float2(r, 0.5)).rgb * _Tint.rgb;

                // white foam streaks
                half streak = smoothstep(_StreakCut, _StreakCut + 0.06, n2) * i.col.g * _StreakStrength * saturate(r * 1.3);
                col = lerp(col, _FoamColor.rgb, streak);

                col *= _Intensity;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
