Shader "Stylized/Fireball Core"
{
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, tileable)", 2D) = "gray" {}

        [Header(Colors)]
        [HDR] _ColorCore ("Core (hottest)", Color) = (1.0, 0.97, 0.80, 1)
        [HDR] _ColorHot  ("Hot",            Color) = (1.0, 0.78, 0.15, 1)
        [HDR] _ColorMid  ("Mid",            Color) = (1.0, 0.35, 0.05, 1)
        _ColorEdge       ("Edge (coolest)", Color) = (0.55, 0.06, 0.02, 1)

        [Header(Banding)]
        _Bands ("Band thresholds (mid, hot, core)", Vector) = (0.30, 0.55, 0.80, 0)
        _Cutoff ("Edge cutoff", Range(0, 1)) = 0.18
        _Softness ("Band softness", Range(0.001, 0.2)) = 0.025
        _Heat ("Center heat (fresnel power)", Range(0.5, 4)) = 1.5
        _NoiseStrength ("Noise strength", Range(0, 1)) = 0.45

        [Header(Motion)]
        _NoiseScale ("Noise scale", Float) = 1.6
        _ScrollSpeed ("Scroll speed", Float) = 1.2
        _Displace ("Vertex displacement", Range(0, 0.5)) = 0.18
        _Tail ("Tail stretch", Range(0, 2)) = 0.9
        _Flicker ("Flicker (script/anim driven)", Float) = 0
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
            Name "FireballCore"

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
                half4 _ColorCore;
                half4 _ColorHot;
                half4 _ColorMid;
                half4 _ColorEdge;
                float4 _Bands;
                float _Cutoff;
                float _Softness;
                float _Heat;
                float _NoiseStrength;
                float _NoiseScale;
                float _ScrollSpeed;
                float _Displace;
                float _Tail;
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

            // Object-space projection that scrolls along local Z (the fireball's travel axis),
            // so the noise streams toward the tail. No UVs needed -> no seams on any sphere mesh.
            float2 FlameUV(float3 p)
            {
                float2 uv = (float2(p.x, p.z) + p.y * float2(0.7, 0.5)) * _NoiseScale;
                uv.y += _Time.y * _ScrollSpeed;
                return uv;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 p0 = IN.positionOS.xyz;
                float2 uv = FlameUV(p0);

                // One vertex texture fetch (LOD 0) drives both the wobble and the tail.
                float n = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv, 0).r;

                float3 p = p0 + IN.normalOS * ((n - 0.5) * _Displace);
                float back = saturate(-IN.normalOS.z);          // 1 at the rear pole
                p.z -= back * _Tail * (0.4 + n);                // teardrop tail, ragged via noise

                float3 posWS = TransformObjectToWorld(p0);
                OUT.positionCS = TransformObjectToHClip(p);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = _WorldSpaceCameraPos - posWS;
                OUT.noiseUV = uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Second, differently-scaled/scrolled lookup of the same texture for fine detail.
                float2 uv = IN.noiseUV * 1.7 + float2(0.13, _Time.y * _ScrollSpeed * 0.6);
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv).r;

                float ndv = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float heat = pow(ndv, _Heat);

                // "Temperature": hot in the middle, ragged and cooler toward the silhouette.
                float v = heat + (n - 0.5) * _NoiseStrength * 2.0 + _Flicker;

                float s = _Softness;
                half3 col = _ColorEdge.rgb;
                col = lerp(col, _ColorMid.rgb,  smoothstep(_Bands.x - s, _Bands.x + s, v));
                col = lerp(col, _ColorHot.rgb,  smoothstep(_Bands.y - s, _Bands.y + s, v));
                col = lerp(col, _ColorCore.rgb, smoothstep(_Bands.z - s, _Bands.z + s, v));

                half a = smoothstep(_Cutoff - s, _Cutoff + s, v);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
