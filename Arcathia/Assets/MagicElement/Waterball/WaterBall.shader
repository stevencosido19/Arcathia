Shader "Stylized/Water Core"
{
    // Toon water blob: 3 color bands (deep -> mid -> light), thin white "vein" highlights,
    // a ragged white foam rim, and a fixed fake specular blob. No lights, no grab pass.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, tileable)", 2D) = "gray" {}

        [Header(Colors)]
        _ColorDeep  ("Deep",  Color) = (0.03, 0.34, 0.78, 1)
        _ColorMid   ("Mid",   Color) = (0.00, 0.70, 0.95, 1)
        _ColorLight ("Light", Color) = (0.30, 1.00, 0.88, 1)
        _ColorFoam  ("Foam / highlights", Color) = (1, 1, 1, 1)

        [Header(Banding)]
        _Bands ("Band thresholds (mid, light)", Vector) = (0.38, 0.68, 0, 0)
        _Softness ("Band softness", Range(0.001, 0.2)) = 0.03

        [Header(Foam and highlights)]
        _FoamStart ("Foam rim start", Range(0.2, 1)) = 0.62
        _FoamNoise ("Foam raggedness", Range(0, 1)) = 0.5
        _VeinScale ("Vein scale", Float) = 1.3
        _VeinWidth ("Vein width", Range(0.005, 0.15)) = 0.045
        _VeinStrength ("Vein strength", Range(0, 1)) = 0.7
        _HighlightSize ("Spec blob threshold", Range(0.7, 1)) = 0.93

        [Header(Shape and motion)]
        _NoiseScale ("Noise scale", Float) = 1.4
        _ScrollSpeed ("Scroll speed", Float) = 0.9
        _Displace ("Vertex wobble", Range(0, 0.5)) = 0.14
        _Ripple ("Ripple", Range(0, 0.1)) = 0.025
        _Tail ("Tail stretch", Range(0, 2)) = 0.6
        _Alpha ("Opacity", Range(0, 1)) = 0.93
        _Flicker ("Shade shimmer (script/anim driven)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterCore"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDeep;
                half4 _ColorMid;
                half4 _ColorLight;
                half4 _ColorFoam;
                float4 _Bands;
                float _Softness;
                float _FoamStart;
                float _FoamNoise;
                float _VeinScale;
                float _VeinWidth;
                float _VeinStrength;
                float _HighlightSize;
                float _NoiseScale;
                float _ScrollSpeed;
                float _Displace;
                float _Ripple;
                float _Tail;
                float _Alpha;
                float _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 noiseUV    : TEXCOORD2;
            };

            // Object-space projection that scrolls along local Z so the surface flows toward the tail.
            float2 FlowUV(float3 p)
            {
                float2 uv = (float2(p.x, p.z) + p.y * float2(0.7, 0.5)) * _NoiseScale;
                uv.y += _Time.y * _ScrollSpeed;
                return uv;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 p0 = IN.positionOS.xyz;
                float2 uv = FlowUV(p0);

                float n = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).r;

                float3 p = p0 + IN.normalOS * ((n - 0.5) * _Displace);
                p += IN.normalOS * (sin(p0.z * 9.0 - _Time.y * 5.0) * _Ripple);   // liquid ripples along the body

                float back = saturate(-IN.normalOS.z);
                p.z -= back * _Tail * (0.4 + n);                                   // tapered watery tail

                float3 posWS = TransformObjectToWorld(p0);
                OUT.positionCS = TransformObjectToHClip(p);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = _WorldSpaceCameraPos - posWS;
                OUT.noiseUV = uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 nW = normalize(IN.normalWS);
                float ndv = saturate(dot(nW, normalize(IN.viewDirWS)));
                float rim = 1.0 - ndv;

                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.noiseUV).r;
                float2 uv2 = IN.noiseUV * _VeinScale + float2(_Time.y * _ScrollSpeed * 0.35, 0.21);
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;

                // Lighter on top, noisy underneath, like the reference's pooled shading.
                float up = nW.y * 0.5 + 0.5;
                float shade = saturate(lerp(n1, up, 0.45) + _Flicker);

                float s = _Softness;
                half3 col = _ColorDeep.rgb;
                col = lerp(col, _ColorMid.rgb,   smoothstep(_Bands.x - s, _Bands.x + s, shade));
                col = lerp(col, _ColorLight.rgb, smoothstep(_Bands.y - s, _Bands.y + s, shade));

                // Thin white veins where the second noise sample crosses 0.5.
                float vein = 1.0 - smoothstep(0.0, _VeinWidth, abs(n2 - 0.5));
                col = lerp(col, _ColorFoam.rgb, vein * _VeinStrength);

                // Fixed toon specular blob + ragged foam rim.
                float hl = smoothstep(_HighlightSize - s, _HighlightSize + s,
                                      dot(nW, normalize(float3(-0.35, 0.85, 0.4))));
                float foam = smoothstep(_FoamStart - s, _FoamStart + s, rim + (n1 - 0.5) * _FoamNoise);
                col = lerp(col, _ColorFoam.rgb, saturate(max(hl, foam)));

                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
}
