Shader "Custom/SpritePaletteStar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0

        _StarEnabled ("Star Enabled", Float) = 0
        _PaletteIndex ("Palette Index", Float) = 0

        _BaseColor0 ("Base Color 0 (Mario)", Color) = (0.41960785,0.42745098,0.0,1)          // #6b6d00
        _BaseColor1 ("Base Color 1 (Mario)", Color) = (0.70980394,0.19215687,0.1254902,1)      // #b53120
        _BaseColor2 ("Base Color 2 (Mario)", Color) = (0.91764706,0.61960787,0.13333334,1)      // #ea9e22
        _BaseColor3 ("Base Color 3 (Mario)", Color) = (0.96862745,0.84705883,0.64705884,1)      // #f7d8a5

        _Palette0Shade0 ("Palette 0 Color 0", Color) = (0.41960785,0.42745098,0.0,1)            // #6b6d00
        _Palette0Shade1 ("Palette 0 Color 1", Color) = (0.70980394,0.19215687,0.1254902,1)      // #b53120
        _Palette0Shade2 ("Palette 0 Color 2", Color) = (0.91764706,0.61960787,0.13333334,1)      // #ea9e22
        _Palette0Shade3 ("Palette 0 Color 3", Color) = (0.96862745,0.84705883,0.64705884,1)      // #f7d8a5

        _Palette1Shade0 ("Palette 1 Color 0", Color) = (0.41960785,0.42745098,0.0,1)            // #6b6d00
        _Palette1Shade1 ("Palette 1 Color 1", Color) = (0.0,0.6509804,0.0,1)                      // #00a600
        _Palette1Shade2 ("Palette 1 Color 2", Color) = (0.972549,0.5921569,0.2509804,1)          // #f89740
        _Palette1Shade3 ("Palette 1 Color 3", Color) = (0.98039216,0.9647059,0.9529412,1)        // #faf6f3

        _Palette2Shade0 ("Palette 2 Color 0", Color) = (0.09019608,0.05882353,0.06666667,1)      // #170f11
        _Palette2Shade1 ("Palette 2 Color 1", Color) = (0.76862746,0.2901961,0.043137256,1)      // #c44a0b
        _Palette2Shade2 ("Palette 2 Color 2", Color) = (0.972549,0.7294118,0.6431373,1)          // #f8baa4
        _Palette2Shade3 ("Palette 2 Color 3", Color) = (0.9529412,0.83137256,0.78039217,1)       // #f3d4c7

        _Palette3Shade0 ("Palette 3 Color 0", Color) = (0.70980394,0.19215687,0.1254902,1)      // #b53120
        _Palette3Shade1 ("Palette 3 Color 1", Color) = (0.8509804,0.14901961,0.0,1)               // #d92600
        _Palette3Shade2 ("Palette 3 Color 2", Color) = (0.9843137,0.5921569,0.227451,1)          // #fb973a
        _Palette3Shade3 ("Palette 3 Color 3", Color) = (1.0,0.972549,0.9764706,1)                 // #fff8f9
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment StarSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            fixed _StarEnabled;
            float _PaletteIndex;
            fixed4 _BaseColor0;
            fixed4 _BaseColor1;
            fixed4 _BaseColor2;
            fixed4 _BaseColor3;

            fixed4 _Palette0Shade0;
            fixed4 _Palette0Shade1;
            fixed4 _Palette0Shade2;
            fixed4 _Palette0Shade3;
            fixed4 _Palette1Shade0;
            fixed4 _Palette1Shade1;
            fixed4 _Palette1Shade2;
            fixed4 _Palette1Shade3;
            fixed4 _Palette2Shade0;
            fixed4 _Palette2Shade1;
            fixed4 _Palette2Shade2;
            fixed4 _Palette2Shade3;
            fixed4 _Palette3Shade0;
            fixed4 _Palette3Shade1;
            fixed4 _Palette3Shade2;
            fixed4 _Palette3Shade3;

            fixed4 ResolvePaletteColor(int paletteIndex, int colorIndex)
            {
                if (paletteIndex == 0)
                {
                    if (colorIndex == 0) return _Palette0Shade0;
                    if (colorIndex == 1) return _Palette0Shade1;
                    if (colorIndex == 2) return _Palette0Shade2;
                    return _Palette0Shade3;
                }

                if (paletteIndex == 1)
                {
                    if (colorIndex == 0) return _Palette1Shade0;
                    if (colorIndex == 1) return _Palette1Shade1;
                    if (colorIndex == 2) return _Palette1Shade2;
                    return _Palette1Shade3;
                }

                if (paletteIndex == 2)
                {
                    if (colorIndex == 0) return _Palette2Shade0;
                    if (colorIndex == 1) return _Palette2Shade1;
                    if (colorIndex == 2) return _Palette2Shade2;
                    return _Palette2Shade3;
                }

                if (colorIndex == 0) return _Palette3Shade0;
                if (colorIndex == 1) return _Palette3Shade1;
                if (colorIndex == 2) return _Palette3Shade2;
                return _Palette3Shade3;
            }

            int ResolveNearestBaseIndex(fixed3 color)
            {
                float d0 = dot(color - _BaseColor0.rgb, color - _BaseColor0.rgb);
                float d1 = dot(color - _BaseColor1.rgb, color - _BaseColor1.rgb);
                float d2 = dot(color - _BaseColor2.rgb, color - _BaseColor2.rgb);
                float d3 = dot(color - _BaseColor3.rgb, color - _BaseColor3.rgb);

                int best = 0;
                float bestDist = d0;
                if (d1 < bestDist) { best = 1; bestDist = d1; }
                if (d2 < bestDist) { best = 2; bestDist = d2; }
                if (d3 < bestDist) { best = 3; }
                return best;
            }

            fixed4 StarSpriteFrag(v2f IN) : SV_Target
            {
                fixed4 texColor = SampleSpriteTexture(IN.texcoord);
                if (texColor.a <= 0.0f)
                    return 0;

                fixed4 finalColor = texColor;
                if (_StarEnabled >= 0.5f)
                {
                    int paletteIndex = clamp((int)floor(_PaletteIndex + 0.5f), 0, 3);
                    int colorIndex = ResolveNearestBaseIndex(texColor.rgb);
                    finalColor.rgb = ResolvePaletteColor(paletteIndex, colorIndex).rgb;
                    finalColor.a = texColor.a;
                }

                finalColor *= IN.color;
                finalColor.rgb *= finalColor.a;
                return finalColor;
            }
            ENDCG
        }
    }
}
