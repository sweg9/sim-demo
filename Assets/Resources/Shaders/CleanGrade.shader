// Clean colour grade for the main camera: exposure, white balance, filmic tone curve,
// softer saturation, gentle contrast, lifted blacks and a light vignette.
Shader "Hidden/CleanGrade"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure, _Saturation, _Contrast, _Vignette;
            float4 _Tint, _Lift;

            // ACES filmic fit (Narkowicz), rescaled so mid-grey stays roughly in place
            float3 Filmic(float3 x)
            {
                const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float3 c = src.rgb;
            #if defined(UNITY_COLORSPACE_GAMMA)
                c = GammaToLinearSpace(c);
            #endif
                c *= _Exposure * _Tint.rgb;
                c = Filmic(c);
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(l.xxx, c, _Saturation);

                float3 g = LinearToGammaSpace(max(c, 0));
                g = (g - 0.5) * _Contrast + 0.5;
                g = g + _Lift.rgb * (1 - g);
                float v = distance(i.uv, float2(0.5, 0.5));
                g *= 1 - _Vignette * smoothstep(0.38, 0.85, v);
                g = saturate(g);
            #if defined(UNITY_COLORSPACE_GAMMA)
                return float4(g, src.a);
            #else
                return float4(GammaToLinearSpace(g), src.a);
            #endif
            }
            ENDCG
        }
    }
    Fallback Off
}
