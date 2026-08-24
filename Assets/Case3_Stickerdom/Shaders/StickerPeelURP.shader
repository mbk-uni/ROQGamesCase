Shader "Custom/StickerPeelURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sticker Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _PeelProgress ("Peel Progress", Range(0, 1)) = 0
        [Toggle] _InvertProgress ("Invert Progress", Float) = 1
        _PeelCorner ("Peel Corner UV", Vector) = (1,0,0,0)
        _PeelDirection ("Direction From Corner", Vector) = (-0.707,0.707,0,0)
        _Travel ("Travel", Range(0.01, 2.0)) = 1.6
        _FoldPieceRotation ("Fold Piece Rotation", Range(-180, 180)) = 0

        _BackColor ("Sticker Back Color", Color) = (1,0.88,0.47,1)
        _BackTextureAmount ("Back Texture Visibility", Range(0, 1)) = 0.08
        _BackTint ("Back Texture Tint", Color) = (0.8,0.65,0.32,1)
        [Toggle] _MirrorBackArtwork ("Mirror Artwork On Fold", Float) = 0

        _ShadowWidth ("Front Shadow Width", Range(0.001, 0.5)) = 0.06
        _ShadowStrength ("Front Shadow Strength", Range(0, 1)) = 0.32
        _FoldEdgeWidth ("Fold Edge Width", Range(0.001, 0.1)) = 0.012
        _FoldColor ("Fold Edge Color", Color) = (1,0.96,0.70,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _PeelProgress;
            float _InvertProgress;
            float4 _PeelCorner;
            float4 _PeelDirection;
            float _Travel;
            float _FoldPieceRotation;
            float4 _BackColor;
            float _BackTextureAmount;
            float4 _BackTint;
            float _MirrorBackArtwork;
            float _ShadowWidth;
            float _ShadowStrength;
            float _FoldEdgeWidth;
            float4 _FoldColor;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.color = input.color;
            output.uv = input.uv;
            return output;
        }

        float IsInside01(float2 uv)
        {
            return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
        }

        // This is an inverse UV transform: a positive material value rotates
        // the visible folded piece counter-clockwise around the moving hinge.
        float2 RotateFoldUV(float2 inputUV, float2 pivotUV, float rotationDegrees)
        {
            float angle = -rotationDegrees * 0.017453292519943295;
            float sine = sin(angle);
            float cosine = cos(angle);
            float2 offset = inputUV - pivotUV;

            float2 rotated = float2(
                cosine * offset.x - sine * offset.y,
                sine * offset.x + cosine * offset.y
            );

            return pivotUV + rotated;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float2 uv = input.uv;
            float2 direction = normalize(_PeelDirection.xy);

            float foldProgress = lerp(_PeelProgress, 1.0 - _PeelProgress, _InvertProgress);
            float2 foldPoint = _PeelCorner.xy + direction * (foldProgress * _Travel);
            float signedDistance = dot(uv - foldPoint, direction);
            float attachedSide = step(0.0, signedDistance);

            // Undo the optional visible rotation, then reflect this output pixel
            // across the fold line to find the original peeled source pixel.
            // This places the back face over the still-attached part of the sprite.
            float2 foldOutputUV = RotateFoldUV(uv, foldPoint, _FoldPieceRotation);
            float foldOutputDistance = dot(foldOutputUV - foldPoint, direction);
            float2 foldedSourceUV = foldOutputUV - 2.0 * foldOutputDistance * direction;

            float sourceIsOnPeeledSide = step(dot(foldedSourceUV - foldPoint, direction), 0.0);
            float sourceIsInsideSticker = IsInside01(foldedSourceUV);
            float2 backArtworkUV = lerp(foldOutputUV, foldedSourceUV, _MirrorBackArtwork);

            float4 front = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color * _Color;
            float4 backShapeSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, foldedSourceUV);
            float4 backArtworkSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, backArtworkUV);

            float3 backRgb = lerp(
                _BackColor.rgb,
                backArtworkSample.rgb * _BackTint.rgb * input.color.rgb * _Color.rgb,
                _BackTextureAmount
            );
            float backAlpha = backShapeSample.a * sourceIsInsideSticker * sourceIsOnPeeledSide
                * _BackColor.a * input.color.a * _Color.a;
            float4 back = float4(backRgb, backAlpha);

            // Keep the original peeled corner empty. On the attached side, the
            // reflected back face overlays the original front artwork.
            float backRegion = sourceIsInsideSticker * sourceIsOnPeeledSide
                * step(0.001, backShapeSample.a) * attachedSide;

            float shadow = (1.0 - smoothstep(0.0, _ShadowWidth, signedDistance))
                * attachedSide * (1.0 - backRegion);
            front.rgb *= 1.0 - shadow * _ShadowStrength;

            float4 color = lerp(front, back, backRegion) * attachedSide;

            float foldEdge = 1.0 - smoothstep(_FoldEdgeWidth, _FoldEdgeWidth * 2.0, abs(signedDistance));
            float edge = foldEdge * attachedSide * max(front.a, backAlpha) * _FoldColor.a;
            color.rgb = lerp(color.rgb, _FoldColor.rgb, edge);
            color.a = max(color.a, edge);

            clip(color.a - 0.001);
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "StickerPeel2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "StickerPeelForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
