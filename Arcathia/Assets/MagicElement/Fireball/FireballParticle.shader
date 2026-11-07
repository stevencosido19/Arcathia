Shader "Stylized/Fireball Particle"
{
    // Procedural flame blob: no sprite sheet needed. Particle color alpha (over lifetime)
    // drives a noise dissolve, so flames "burn out" instead of just fading.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise scale", Float) = 1.2
        _Scroll ("Scroll speed", Float) = 0.8
        _NoiseStrength ("Noise strength", Range(0, 1)) = 0.6
        _Softness ("Edge softness", Range(0.01, 0.5)) = 0.12
        _Intensity ("Intensity", Float) = 1.6
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
            Name "FireballParticle"

            Blend One One      // additive
            ZWrite Off
            Cull Off
            ColorMask RGB

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
                float _Intensity;
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
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0;

                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                                           IN.uv * _NoiseScale + float2(0.0, -_Time.y * _Scroll)).r;

                float shape = (1.0 - r) + (n - 0.5) * _NoiseStrength * 2.0;

                float life = IN.color.a;                    // 1 -> 0 over lifetime
                float thr = (1.0 - life) * 0.85;            // dissolve threshold rises as it ages
                float a = smoothstep(thr, thr + _Softness, shape);

                a *= smoothstep(1.0, 0.8, r);               // keep the quad border invisible

                half3 rgb = IN.color.rgb * (a * _Intensity);
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
