Shader "Stylized/Water Particle"
{
    // Procedural water blob / droplet. Particle color alpha (over lifetime) drives a noise dissolve,
    // and the band just inside the dissolving edge turns white, giving the foam-rimmed look
    // of the reference without any sprite sheet. Normal alpha blending so the blue stays blue.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale", Float) = 1.2
        _Scroll ("Scroll speed", Float) = 0.6
        _NoiseStrength ("Noise strength", Range(0, 1)) = 0.5
        _Softness ("Edge softness", Range(0.01, 0.5)) = 0.07
        _FoamWidth ("Foam width", Range(0, 0.6)) = 0.2
        _FoamColor ("Foam color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WaterParticle"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float _NoiseScale;
                float _Scroll;
                float _NoiseStrength;
                float _Softness;
                float _FoamWidth;
                half4 _FoamColor;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv - 0.5) * 2.0;

                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                                           IN.uv * _NoiseScale + float2(0.0, -_Time.y * _Scroll)).r;

                float shape = (1.0 - r) + (n - 0.5) * _NoiseStrength * 2.0;

                float life = IN.color.a;                    // 1 -> 0 over lifetime
                float thr = (1.0 - life) * 0.8;

                float body = smoothstep(thr, thr + _Softness, shape);
                float foam = 1.0 - smoothstep(thr + _FoamWidth, thr + _FoamWidth + _Softness, shape);

                half3 rgb = lerp(IN.color.rgb, _FoamColor.rgb, foam);
                float a = body * smoothstep(1.0, 0.8, r) * _Opacity;   // keep the quad border invisible
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
