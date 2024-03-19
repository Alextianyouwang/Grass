#ifndef GRASSFIELD_GRAPHIC_INCLUDE
#define GRASSFIELD_GRAPHIC_INCLUDE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../INCLUDE/HL_GraphicsHelper.hlsl"
#include "../INCLUDE/HL_Noise.hlsl"
#include "../INCLUDE/HL_CustomLighting.hlsl"

struct VertexInput
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    
};
struct VertexOutput
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float4 debug : TEXCOORD3;
    float4 clumpInfo : TEXCOORD4;
   
    
};
struct SpawnData
{
    float3 positionWS;
    float hash;
    float4 clumpInfo;
    float density;
    float wind;
};
StructuredBuffer<SpawnData> _SpawnBuffer;
float3 _ChunkColor,_LOD_Color;
TEXTURE2D( _MainTex);SAMPLER (sampler_MainTex);float4 _MainTex_ST;
float _Scale, _WindSpeed, _WindFrequency, _WindNoiseAmplitude, _WindDirection, _WindNoiseFrequency,_RandomBendOffset,_WindAmplitude,
_DetailSpeed, _DetailAmplitude, _DetailFrequency,
_HeightRandomnessAmplitude,
_Tilt,_Height,_Bend;
float4 _TopColor, _BotColor;
float2 _Offset; // World Pos Sampler Offset for Continuous Values;
// Dont know why unity make indirect draw call bounds
// adding vertex offset to its instance group... 
// and even though using this fucntion to make up their
// perlin-noise in graphic shader are not continuous anymore...


float Perlin(float2 uv)
{
    return perlinNoise(uv, float2(12.9898, 78.233));
}

float SinWaveWithNoise(float2 uv,float direction, float noiseFreq, float noiseWeight, float waveSpeed, float waveFreq)
{
    float2 rotatedUV = Rotate2D(uv,direction * 360);
    float noise = Perlin(rotatedUV * noiseFreq) *  noiseWeight * 10;
    float wave = sin((rotatedUV.x + rotatedUV.y + noise - _Time.y * waveSpeed * 10) * waveFreq);
    return wave;
}


void CalculateGrassCurve(float t, out float3 pos, out float3 tan)
{
    float2 P3 = float2(_Height, _Tilt);
    float2 P2 = float2(_Tilt, _Height) / 2 + normalize(float2(-_Height, _Tilt)) * _Bend;
    CubicBezierCurve_Tilt_Bend(float3(0, P2.y, P2.x), float3(0, P3.y, P3.x), t, pos, tan);
}



VertexOutput vert(VertexInput v, uint instanceID : SV_INSTANCEID)
{
    VertexOutput o;
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    float3 spawnPosWS_raw = _SpawnBuffer[instanceID].positionWS;
    float3 spawnPosWS_outVertex = _SpawnBuffer[instanceID].positionWS - float3(_Offset.x,0, _Offset.y);
    o.clumpInfo = _SpawnBuffer[instanceID].clumpInfo;
    float2 dirToCenter = normalize((spawnPosWS_raw).xz - o.clumpInfo.xy);
    float distToCenter = o.clumpInfo.z;
    float hash = _SpawnBuffer[instanceID].hash ;
    
   
    #if  _USE_MAINWAVE_ON
        float wave = SinWaveWithNoise(spawnPosWS_raw.xz , _WindDirection, _WindNoiseFrequency, _WindNoiseAmplitude, _WindSpeed, _WindFrequency) ;
        //wave = _SpawnBuffer[instanceID].wind;
    #else
        float wave = 0;
    #endif
    
    
    #if _USE_DETAIL_ON
        float detail = Perlin(Rotate2D(spawnPosWS_raw.xz, _WindDirection * 360) * _DetailFrequency * 10 - _Time.y * _DetailSpeed * 10) ;
    #else
        float detail = 0;
    #endif
    
    
    #if _USE_RANDOM_HEIGHT_ON
        float heightPerlin = Perlin((spawnPosWS_raw.xz+ _Offset) * 20)*_HeightRandomnessAmplitude;
    #else
        float heightPerlin  = 0;
    #endif
    
    float rand = _SpawnBuffer[instanceID].hash;
    rand *= 2;
    rand -= 1;
    //float3 pos = RotateAroundYInDegrees(float4(v.positionOS, 1), rand * 360).xyz;
    //float2 rotatedWindDir = Rotate2D(float2(1, -1), _WindDirection * 360);
    //pos = RotateAroundAxis(float4(pos, 1), float3(rotatedWindDir.x, 0, rotatedWindDir.y),
    //    v.uv.y * ((_Bend * 20 + rand * _RandomBendOffset * 20) + (wave * _WindAmplitude * 20 + (wave / 2 + 0.75) * detail * _DetailAmplitude * 20))).xyz;
    //pos *= _Scale + heightPerlin;
    float3 pos = v.positionOS;
    float3 curvePos;
    float3 curveTangent;
    CalculateGrassCurve(o.uv.y, curvePos, curveTangent);
    float3 curveNormal = normalize(cross(float3(-1, 0, 0), curveTangent));
    
    pos.yz = curvePos.yz;
    float rotDegree = - rand * 45 + _WindDirection - 90;
    pos = RotateAroundYInDegrees(float4(pos, 1), rotDegree).xyz;
    curveNormal = RotateAroundYInDegrees(float4(curveNormal, 0), rotDegree).xyz;

    pos *= _Scale + heightPerlin;
    pos += spawnPosWS_outVertex;
    
    o.positionWS = pos;
    o.positionCS = TransformObjectToHClip(pos);
    o.normalWS = TransformObjectToWorldNormal(curveNormal);

    o.debug = float4(dirToCenter,0, _SpawnBuffer[instanceID].wind);

    return o;
}

float4 frag(VertexOutput v) : SV_Target
{
    float4 color = lerp(_BotColor,_TopColor ,v.uv.y);
    float3 normal = normalize(v.normalWS);
    float3 finalColor;
    
   Light mainLight = GetMainLight();
    float nDotl = saturate(dot(mainLight.direction, normal));

   
#if _DEBUG_OFF
        return normal.xyzz ;
#elif _DEBUG_MAINWAVE
        return v.debug;
#elif _DEBUG_DETAILEDWAVE
        return v.debug.y;
#elif _DEBUG_CHUNKID
        return _ChunkColor.xyzz;
#elif _DEBUG_LOD
        return _LOD_Color.xyzz;
#elif _DEBUG_CLUMPCELL
        return v.clumpInfo.wwzz;
#elif _DEBUG_GLOBALWIND
        return v.debug.w;
#else 

    return color;
#endif
}
#endif