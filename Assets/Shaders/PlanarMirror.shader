Shader "ColorManRun/PlanarMirror"
{
    Properties
    {
        _ReflectionTex ("Reflection (managed)", 2D) = "black" {}
        _Tint ("Reflection Tint", Color) = (0.88, 0.93, 1, 1)
        _Opacity ("Reflection Opacity", Range(0, 1)) = 0.9
        _EdgeFade ("Boundary Fade (meters)", Float) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _ReflectionTex;
            float4x4 _ReflectionVP;
            float4 _MirrorSize;
            fixed4 _Tint;
            float _Opacity, _EdgeFade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float4 reflection : TEXCOORD0; float2 uv : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.reflection = mul(_ReflectionVP, mul(unity_ObjectToWorld, v.vertex));
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.reflection.xy / i.reflection.w * 0.5 + 0.5;
                float2 distanceToEdge = min(i.uv, 1 - i.uv) * _MirrorSize.xy;
                float edge = saturate(min(distanceToEdge.x, distanceToEdge.y) / max(_EdgeFade, 0.001));
                fixed3 reflected = tex2D(_ReflectionTex, uv).rgb * _Tint.rgb;
                return fixed4(reflected, _Opacity * edge);
            }
            ENDCG
        }
    }
    Fallback Off
}
