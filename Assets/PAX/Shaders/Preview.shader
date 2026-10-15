Shader "PAX/Preview"
{
    Properties { _Color ("Color", Color) = (0.2,1,0.5,0.3) _EdgeWidth ("Edge width", Range(0.005,0.1)) = 0.03 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; };
            fixed4 _Color; float _EdgeWidth;
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.local = v.vertex.xyz; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float3 edges = step(0.5 - _EdgeWidth, abs(i.local));
                return fixed4(_Color.rgb, lerp(_Color.a * 0.2, 0.95, step(1.5,edges.x+edges.y+edges.z)));
            }
            ENDCG
        }
    }
}
