Shader "Custom/BiomeVertexColor"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };
            
            CBUFFER_START(UnityPerMaterial)
                half _Smoothness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Get main light
                Light mainLight = GetMainLight();
                
                // Simple diffuse lighting
                float NdotL = saturate(dot(input.normalWS, mainLight.direction));
                float3 diffuse = mainLight.color * NdotL;
                
                // Add ambient
                float3 ambient = SampleSH(input.normalWS);
                
                // Final color = vertex color * lighting
                float3 finalColor = input.color.rgb * (diffuse + ambient);
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}