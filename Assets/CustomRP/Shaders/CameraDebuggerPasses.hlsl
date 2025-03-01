#ifndef CAMERA_DEBUGGER_PASSES_INCLUDED
#define CAMERA_DEBUGGER_PASSES_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"


float _DebugOpacity;
TEXTURE2D(_SourceTexture);


struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};


Varyings DefaultPassVertex (uint vertexID : SV_VertexID) 
{
	Varyings output;
	output.positionCS = float4
	(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2
	(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);

	if (_ProjectionParams.x < 0.0) 
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


float4 ForwardPlusTilesPassFragment(Varyings input) : SV_TARGET
{
	ForwardPlusTile tile = GetForwardPlusTile(input.screenUV);
	float3 color;
	if (tile.IsMinimumEdgePixel(input.screenUV))
	{
		color = 1.0;
	}
	else
	{
		color = OverlayHeatMap(
			input.screenUV * _CameraBufferSize.zw, tile.GetScreenSize(),
			tile.GetLightCount(), tile.GetMaxLightsPerTile(), 1.0).rgb;
	}
	//color = float3(input.screenUV / _ForwardPlusTileSettings.xy, 1);
	//color = float3(tile.GetMaxLightsPerTile() / 10, 1, 1);
	return float4(color, _DebugOpacity);
}


#endif