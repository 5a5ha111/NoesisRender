using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ForwardPlusTilesJob : IJobFor
{
    /// <summary>
    /// Input: Array of light bounds in screen space.
    /// </summary>
    [ReadOnly] public NativeArray<float4> lightBounds;

    /// <summary>
    /// Output: Data array for each tile, storing indices of overlapping lights.
    /// </summary>
    [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> tileData;

    public int otherLightCount;
    public float2 tileScreenUVSize;
    public int maxLightsPerTile;
    public int tilesPerRow;
    public int tileDataSize;

    /// <summary>
    /// Loop through all lights and check if the bounds overlap. If so add the light index to the tile's list and stop if the maximum amount of lights is reached
    /// </summary>
    /// <param name="tileIndex"></param>
    public void Execute(int tileIndex)
    {
        int y = tileIndex / tilesPerRow;
        int x = tileIndex - y * tilesPerRow;
        var bounds = float4(x, y, x + 1, y + 1) * tileScreenUVSize.xyxy;

        int headerIndex = tileIndex * tileDataSize;
        int dataIndex = headerIndex;
        int lightsInTileCount = 0;

        for (int i = 0; i < otherLightCount; i++)
        {
            float4 b = lightBounds[i];
            if (all(float4(b.xy, bounds.xy) <= float4(bounds.zw, b.zw)))
            {
                tileData[++dataIndex] = i;
                if (++lightsInTileCount >= maxLightsPerTile)
                {
                    break;
                }
            }
        }
        tileData[headerIndex] = lightsInTileCount;
    }
}