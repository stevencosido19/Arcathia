// Water wave shader: vertex-animated flow in the model + layered water look.
// LAYERED LOOK (matches the reference video): blue height gradient, light streaks scrolling down the slope,
// white bubbles, foam lip on top, foam bands running down the slope, ragged bottom fade, dissolve over time.
// - The stylized texture is completely static (painted look stays crisp).
// - Motion = vertex displacement: rolling swell (Gerstner-style crest roll), a secondary cross swell,
//   and a snaking sway along the flow direction. Normals are re-derived so shading moves with it.
// - 2 texture fetches (texture + dissolve noise). No grab pass, no depth write. Built-in + URP.
//
// Mesh needs enough vertices to show the motion (quads about 0.15-0.25 m for a ~3 m wave).
// Optional vertex colors (white if absent):
//   R = tone  (paint dark at the underside / base)
//   G = flow mask (only used when "Use Vertex Color G as Flow Mask" > 0; 0 = pinned, 1 = full motion)
//   A = fade  (0 at tips so they fade out)
Shader "VFX/WaterWaveFlow"
{
    Properties
    {
        _MainTex ("Stylized Texture (RGBA, static)", 2D) = "white" {}
        _NoiseTex ("Dissolve Noise", 2D) = "gray" {}
        _UseMainTex ("Use Painted Texture (0 = layered water look)", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _FoamColor ("Rim Foam Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0.5, 3)) = 1

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Back for closed meshes)", Float) = 2
        [Toggle] _ZWrite ("Depth Write", Float) = 1

        [Header(Mapping)]
        _UsePlanar ("Planar Top-Down Mapping (0 = mesh UV)", Range(0, 1)) = 1
        _PlanarRect ("Planar Rect (minSide, minFlow, 1/sizeSide, 1/sizeFlow)", Vector) = (0, 0, 1, 1)

        [Header(Layered Water   Colors)]
        _ColorLow ("Water Low (bottom of wave)", Color) = (0.10, 0.45, 0.85, 1)
        _ColorHigh ("Water High (top of wave)", Color) = (0.36, 0.76, 1.0, 1)
        _StreakColor ("Streak Color", Color) = (0.78, 0.94, 1.0, 1)
        _HeightRange ("Height Range (min, 1/size)", Vector) = (0, 1, 0, 0)

        [Header(Layered Water   Scrolling Streaks)]
        _ScrollSpeed ("Streak Scroll Speed (down the slope)", Range(-3, 3)) = 0.35
        _StreakScale ("Streak Scale (across, along)", Vector) = (6, 1.2, 0, 0)
        _StreakCut ("Streak Threshold", Range(0, 1)) = 0.55
        _StreakStrength ("Streak Strength", Range(0, 1)) = 0.6

        [Header(Layered Water   Foam and Bubbles)]
        _LipStart ("Foam Lip Start Height", Range(0, 1)) = 0.78
        _LipSoft ("Foam Lip Softness", Range(0.01, 0.5)) = 0.08
        _LipNoise ("Foam Lip Raggedness", Range(0, 1)) = 0.35
        _BandCut ("Foam Band Threshold", Range(0, 1)) = 0.7
        _BandStrength ("Foam Band Strength", Range(0, 1)) = 1
        _BubbleCut ("Bubble Threshold (higher = fewer)", Range(0.5, 1)) = 0.82
        _BubbleStrength ("Bubble Strength", Range(0, 1)) = 1

        [Header(Layered Water   Bottom Fade)]
        _BottomFade ("Bottom Fade (start, width)", Vector) = (0.05, 0.25, 0, 0)
        _BottomNoise ("Bottom Raggedness", Range(0, 1)) = 0.3

        [Header(Alpha)]
        _AlphaCut ("Alpha Threshold", Range(0, 1)) = 0.3
        _Soft ("Alpha Softness", Range(0.002, 0.5)) = 0.2

        [Header(Vertex Flow   Main Swell)]
        _FlowDir ("Flow Direction (object space)", Vector) = (0, 0, 1, 0)
        _UpAxis ("Up Axis (object space)", Vector) = (0, 1, 0, 0)
        _Amp1 ("Swell Height", Range(0, 1)) = 0.12
        _Freq1 ("Swell Frequency", Range(0, 12)) = 2.5
        _Speed1 ("Swell Speed", Range(-12, 12)) = 3
        _Steep ("Crest Roll Steepness", Range(0, 1.5)) = 0.6
        _Cross1 ("Wave Front Slant", Range(-4, 4)) = 0.8

        [Header(Vertex Flow   Secondary Swell)]
        _Amp2 ("Height", Range(0, 1)) = 0.06
        _Freq2 ("Frequency", Range(0, 12)) = 4.5
        _Speed2 ("Speed", Range(-12, 12)) = -2

        [Header(Vertex Flow   Sway)]
        _SwayAmp ("Sway Amount", Range(0, 0.6)) = 0.08
        _SwayFreq ("Sway Frequency", Range(0, 8)) = 1.5
        _SwaySpeed ("Sway Speed", Range(-8, 8)) = 2
        _UseVertexMask ("Use Vertex Color G as Flow Mask", Range(0, 1)) = 0

        [Header(Depth Shading)]
        _LightDir ("Fake Light Direction (world)", Vector) = (0.3, 0.8, -0.5, 0)
        _ShadeAmount ("Form Shading", Range(0, 0.8)) = 0.25
        _FresnelStrength ("Rim / Fresnel Foam", Range(0, 1)) = 0.3
        _VertexTone ("Vertex Color R Darkening", Range(0, 1)) = 0.5

        [Header(Runtime Driven)]
        _NoiseScale ("Static Noise Scale (bubbles, dissolve)", Vector) = (4, 4, 0, 0)
        _Progress ("Dissolve Progress", Range(0, 1)) = 0
        _Alpha ("Global Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull [_Cull]
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float _UsePlanar;
            float4 _PlanarRect;
            half _UseMainTex;
            half4 _ColorLow, _ColorHigh, _StreakColor;
            float4 _HeightRange;
            float _ScrollSpeed;
            float4 _StreakScale;
            half _StreakCut, _StreakStrength;
            half _LipStart, _LipSoft, _LipNoise, _BandCut, _BandStrength, _BubbleCut, _BubbleStrength;
            float4 _BottomFade;
            half _BottomNoise;
            half4 _Tint;
            half4 _FoamColor;
            half _Intensity;
            half _AlphaCut, _Soft;
            float4 _FlowDir, _UpAxis;
            float _Amp1, _Freq1, _Speed1, _Steep, _Cross1;
            float _Amp2, _Freq2, _Speed2;
            float _SwayAmp, _SwayFreq, _SwaySpeed, _UseVertexMask;
            float4 _LightDir;
            half _ShadeAmount, _FresnelStrength, _VertexTone;
            float4 _NoiseScale;
            half _Progress, _Alpha;

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
                half h : TEXCOORD3;
                float2 puv : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
                v2f o;

                float3 flowDir = normalize(_FlowDir.xyz);
                float3 up = normalize(_UpAxis.xyz);
                float3 side = normalize(cross(up, flowDir));
                float wgt = lerp(1.0, v.color.g, _UseVertexMask);
                float t = _Time.y;

                float3 p = v.vertex.xyz;
                float s = dot(p, flowDir);
                float c = dot(p, side);

                // main rolling swell: up/down + back/forth = crest roll
                float ph1 = s * _Freq1 - t * _Speed1 + c * _Cross1;
                float sn1 = sin(ph1);
                float cs1 = cos(ph1);

                // secondary swell travelling diagonally
                float3 dir2 = normalize(flowDir * 0.6 + side * 0.8);
                float ph2 = dot(p, dir2) * _Freq2 - t * _Speed2 + 1.7;
                float sn2 = sin(ph2);
                float cs2 = cos(ph2);

                // snaking sway
                float ph3 = s * _SwayFreq - t * _SwaySpeed;

                float3 d = up * (sn1 * _Amp1) + flowDir * (cs1 * _Amp1 * _Steep)
                         + up * (sn2 * _Amp2) + dir2 * (cs2 * _Amp2 * _Steep * 0.5)
                         + side * (sin(ph3) * _SwayAmp);
                p += d * wgt;

                // re-derive normal from the wave slopes so shading follows the motion
                float3 slope = flowDir * (_Amp1 * _Freq1 * cs1)
                             + side * (_Amp1 * _Cross1 * cs1)
                             + dir2 * (_Amp2 * _Freq2 * cs2);
                float3 n = normalize(v.normal - slope * wgt * dot(v.normal, up));

                o.pos = UnityObjectToClipPos(float4(p, 1.0));
                // planar top-down mapping uses the UNDISPLACED position, so the texture sticks to the water
                float2 uvPlanar = float2((dot(v.vertex.xyz, side) - _PlanarRect.x) * _PlanarRect.z,
                                         (dot(v.vertex.xyz, flowDir) - _PlanarRect.y) * _PlanarRect.w);
                o.uv = lerp(TRANSFORM_TEX(v.uv, _MainTex), uvPlanar, _UsePlanar);
                o.puv = uvPlanar;
                o.h = (dot(v.vertex.xyz, up) - _HeightRange.x) * _HeightRange.y;
                o.col = v.color;
                o.nrm = UnityObjectToWorldNormal(n);
                float3 wp = mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
                o.view = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;

                // streak noise: stretched along the slope and scrolling down it
                float2 sUV = float2(i.puv.x * _StreakScale.x, i.puv.y * _StreakScale.y - t * _ScrollSpeed);
                half ns = tex2D(_NoiseTex, sUV).r;
                // static noise: bubbles, bottom breakup, dissolve
                half nb = tex2D(_NoiseTex, i.puv * _NoiseScale.xy).r;

                half4 m = tex2D(_MainTex, i.uv);
                half h = saturate(i.h);

                // dissolve (driven by WaterWaveController)
                half cut = lerp(-0.15, 1.1, _Progress);
                half diss = saturate((nb - cut) / 0.08);

                // bottom of the sheet fades out with a ragged edge
                half bottom = saturate((h + (nb - 0.5) * _BottomNoise - _BottomFade.x) / max(0.001, _BottomFade.y));

                half breakup = saturate((m.a - _AlphaCut) / _Soft);
                half alpha = breakup * diss * bottom * i.col.a * _Alpha;
                clip(alpha - 0.003);

                // depth cues
                half3 nrm = normalize(i.nrm);
                half3 vdir = normalize(i.view);
                half ndv = dot(nrm, vdir);
                nrm *= (ndv < 0.0) ? -1.0 : 1.0;
                ndv = abs(ndv);
                half ndl = dot(nrm, normalize(_LightDir.xyz));
                half fres = 1.0 - ndv;
                fres *= fres;

                // body: height gradient (or the painted texture)
                half3 body = lerp(lerp(_ColorLow.rgb, _ColorHigh.rgb, h), m.rgb, _UseMainTex) * _Tint.rgb;
                body *= lerp(1.0, i.col.r, _VertexTone);
                body *= 1.0 + ndl * _ShadeAmount;

                // light streaks flowing down the slope
                half streak = smoothstep(_StreakCut, _StreakCut + 0.15, ns) * _StreakStrength;
                half3 col = lerp(body, _StreakColor.rgb, streak);

                // bubbles
                half bub = smoothstep(_BubbleCut, _BubbleCut + 0.03, nb) * _BubbleStrength;
                col = lerp(col, _FoamColor.rgb, bub);

                // foam lip on top (ragged by the streak noise = foam fingers running down) + foam bands
                half lip = smoothstep(_LipStart, _LipStart + _LipSoft, h + (ns - 0.5) * _LipNoise);
                half bands = smoothstep(_BandCut, _BandCut + 0.05, ns) * smoothstep(0.25, 0.75, h) * _BandStrength;
                col = lerp(col, _FoamColor.rgb, saturate(max(lip, bands)));

                col = lerp(col, _FoamColor.rgb, fres * _FresnelStrength);
                col *= _Intensity;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
