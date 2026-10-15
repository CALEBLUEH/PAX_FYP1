Shader "PAX/Block Cutaway"
{
    Properties
    {
        _Color ("Tile color", Color) = (0.4,0.65,0.5,1)
        _Visibility ("Body visibility", Range(0,1)) = 1
        _GhostColor ("Ghost height cue", Color) = (0.2,0.9,1,1)
        _GhostRelevance ("Local ghost relevance", Range(0,1)) = 1
        _EdgeWidth ("Ghost edge width", Range(0.005,0.08)) = 0.018
        _WorldSunlight ("Physical tile sunlight", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; SHADOW_COORDS(2) };
            fixed4 _Color, _GhostColor;
            float _Visibility, _EdgeWidth, _GhostRelevance, _WorldSunlight;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 edges = step(0.5 - _EdgeWidth, abs(i.local));
                float edge = step(1.5, edges.x + edges.y + edges.z);
                float coverage = max(_Visibility, edge * (1 - _Visibility) * 0.3 * _GhostRelevance);
                clip(coverage - 0.001);
                float3 n = normalize(i.normal);
                fixed3 lit = _Color.rgb * (ShadeSH9(float4(n,1)) + _LightColor0.rgb * max(0,dot(n,_WorldSpaceLightPos0.xyz)) * _WorldSunlight);
                return fixed4(lerp(lit, _GhostColor.rgb, edge * (1 - _Visibility)), coverage);
            }
            ENDCG
        }
        // The logical world always casts solid shadows, independent of camera presentation.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert(appdata_base v) { v2f o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o); return o; }
            float4 frag(v2f i):SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
