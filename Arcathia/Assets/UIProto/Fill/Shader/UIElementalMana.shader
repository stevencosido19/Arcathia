// UI shader for the mana fill Images. One shader, three elements (set by ManaFillAnimator via _Element):
//   0 = Water      : rippling UV wobble + drifting caustic shimmer
//   1 = Fire       : rising flame distortion, hot colour flicker
//   2 = Lightning  : stepped UV jitter, crackling arc lines, random flash frames
// Standard UI shader features (stencil, masks, clip rect) are kept so it works inside Masks/ScrollRects.
// Works with Image Type = Filled (any fill method) because the effect is based on the sprite UVs, not the fill edge.
Shader "UI/ElementalMana"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Element ("Element (0 Water, 1 Fire, 2 Lightning)", Float) = 0
        _Intensity ("Effect Intensity", Range(0, 3)) = 1
        _Distortion ("UV Distortion", Range(0, 3)) = 1
        _Speed ("Animation Speed", Range(0, 3)) = 1
        _Flash ("Gain Flash (driven by script)", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Element;
            float _Intensity;
            float _Distortion;
            float _Speed;
            float _Flash;
            fixed4 _FlashColor;

            // ---- tiny procedural noise (no texture needed) ----
            float hash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = IN.texcoord;
                float bright = 1.0;
                float3 add = float3(0.0, 0.0, 0.0);

                if (_Element < 0.5)
                {
                    // ---------------- WATER ----------------
                    float w1 = sin(uv.y * 18.0 + t * 2.6);
                    float w2 = sin(uv.x * 15.0 - t * 2.0);
                    uv.x += w1 * 0.012 * _Distortion;
                    uv.y += w2 * 0.012 * _Distortion;

                    // drifting caustic shimmer
                    float caustic = sin((uv.x + uv.y) * 22.0 - t * 2.2) * 0.5 + 0.5;
                    caustic = caustic * caustic;
                    add += float3(0.10, 0.20, 0.26) * caustic * _Intensity;
                    bright = 0.96 + caustic * 0.10;
                }
                else if (_Element < 1.5)
                {
                    // ---------------- FIRE ----------------
                    float2 p = float2(uv.x * 5.0, uv.y * 3.0 - t * 2.6);
                    float n = vnoise(p) * 0.65 + vnoise(p * 2.1 + 7.3) * 0.35;

                    uv.x += (n - 0.5) * 0.07 * _Distortion;
                    uv.y += (n - 0.5) * 0.04 * _Distortion;

                    float flick = 1.0 + 0.07 * sin(t * 17.0) + 0.05 * sin(t * 31.0 + 1.3);
                    bright = (0.85 + n * 0.55) * flick;
                    add += float3(1.0, 0.55, 0.10) * max(0.0, n - 0.45) * 0.9 * _Intensity;
                }
                else
                {
                    // ---------------- LIGHTNING ----------------
                    float tick = floor(t * 14.0);          // stepped time = snappy, electric
                    float2 j = (hash22(float2(tick, 1.7)) - 0.5) * 0.02 * _Distortion;
                    uv += j;

                    float arcN = vnoise(float2(uv.x * 14.0 + tick * 1.3, uv.y * 14.0 - tick * 0.7));
                    float ridge = 1.0 - abs(arcN * 2.0 - 1.0);
                    float arc = smoothstep(0.92, 0.99, ridge);

                    float flashFrame = step(0.86, hash11(tick * 3.1));
                    add += float3(0.65, 0.85, 1.0) * (arc * 1.2 + flashFrame * 0.35) * _Intensity;
                    bright = 0.9 + hash11(tick * 7.7) * 0.25;
                }

                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                color.rgb = color.rgb * bright + add * color.a;
                color.rgb += _FlashColor.rgb * _Flash * color.a;   // gain flash from script

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
