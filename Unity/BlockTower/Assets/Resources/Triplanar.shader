// CAD meshes come without UVs: project the texture along the object's three axes,
// in object space so it stays on a chair that slides. _Size = texture's real size (m).
// Colour only: the meshes have no tangents for normal maps.
Shader "BlockTower/Triplanar"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Size ("Texture size (m)", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _BlendSharpness ("Blend sharpness", Float) = 8
        _Offset ("Texture offset (m)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Size, _Glossiness, _BlendSharpness;
        float4 _Offset;  // per object, so small parts show different grain

        struct Input { float3 objPos; float3 objNormal; };

        void vert(inout appdata_full v, out Input o)
        {
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.objNormal), _BlendSharpness);
            w /= w.x + w.y + w.z;
            float3 p = (IN.objPos + _Offset.xyz) / _Size;
            // grain/planks run along X on the top faces and vertically on the sides
            float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;
            o.Albedo = (tex2D(_MainTex, uvX) * w.x + tex2D(_MainTex, uvY) * w.y + tex2D(_MainTex, uvZ) * w.z).rgb * _Color.rgb;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
