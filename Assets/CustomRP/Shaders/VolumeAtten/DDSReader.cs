using GLTFast.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;


/// <summary>
/// Unity dont properly support dds files, but it very straightforward format, so i wrote my own reader
/// </summary>
public static class DDSReader
{
    // Constants and structures for DDS header
    private const uint DDS_MAGIC = 0x20534444; // "DDS "
    private const uint DDS_HEADER_SIZE = 124;
    private const uint DDS_PF_FLAGS_FOURCC = 0x04;
    private const uint DDS_DXT10_FOURCC = 0x30315844; // "DX10"

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DDS_PIXELFORMAT
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwFourCC;
        public uint dwRGBBitCount;
        public uint dwRBitMask;
        public uint dwGBitMask;
        public uint dwBBitMask;
        public uint dwABitMask;
    }

    [Flags]
    private enum DDS_HEADER_FLAGS : uint
    {
        HEIGHT = 0x2,
        WIDTH = 0x4,
        PITCH = 0x8,
        LINEAR_SIZE = 0x80000
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DDS_HEADER
    {
        public uint dwSize;
        public DDS_HEADER_FLAGS dwFlags;
        public uint dwHeight;
        public uint dwWidth;
        public uint dwPitchOrLinearSize;
        public uint dwDepth;
        public uint dwMipMapCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        public uint[] dwReserved;
        public DDS_PIXELFORMAT ddspf;
        public uint dwCaps;
        public uint dwCaps2;
        public uint dwCaps3;
        public uint dwCaps4;
        public uint dwReserved2;
    }

    // Supported DXGI formats (partial list)
    private enum DXGI_FORMAT : uint
    {
        R32G32B32A32_FLOAT = 2,
        R32G32B32_FLOAT = 6,
        R32G32_FLOAT = 16,
        R32_FLOAT = 41,
        R16G16B16A16_FLOAT = 10,
        R16G16_FLOAT = 34,
        R16_FLOAT = 54
    }

    // Format information container
    private struct FormatInfo
    {
        public int ComponentCount;
        public int BytesPerComponent;
        public bool IsFloat;
    }

    // Format mapping dictionaries
    private static readonly Dictionary<uint, FormatInfo> LegacyFormatMap = new()
    {
        { 114, new FormatInfo { ComponentCount = 1, BytesPerComponent = 4, IsFloat = true } }, // D3DFMT_R32F
        { 115, new FormatInfo { ComponentCount = 2, BytesPerComponent = 4, IsFloat = true } }, // D3DFMT_R32G32F
        { 116, new FormatInfo { ComponentCount = 4, BytesPerComponent = 4, IsFloat = true } }, // D3DFMT_R32G32B32A32F
    };

    private static readonly Dictionary<DXGI_FORMAT, FormatInfo> DxgiFormatMap = new()
    {
        { DXGI_FORMAT.R32_FLOAT, new FormatInfo { ComponentCount = 1, BytesPerComponent = 4, IsFloat = true } },
        { DXGI_FORMAT.R32G32_FLOAT, new FormatInfo { ComponentCount = 2, BytesPerComponent = 4, IsFloat = true } },
        { DXGI_FORMAT.R32G32B32_FLOAT, new FormatInfo { ComponentCount = 3, BytesPerComponent = 4, IsFloat = true } },
        { DXGI_FORMAT.R32G32B32A32_FLOAT, new FormatInfo { ComponentCount = 4, BytesPerComponent = 4, IsFloat = true } },
        { DXGI_FORMAT.R16_FLOAT, new FormatInfo { ComponentCount = 1, BytesPerComponent = 2, IsFloat = true } },
        { DXGI_FORMAT.R16G16_FLOAT, new FormatInfo { ComponentCount = 2, BytesPerComponent = 2, IsFloat = true } },
        { DXGI_FORMAT.R16G16B16A16_FLOAT, new FormatInfo { ComponentCount = 4, BytesPerComponent = 2, IsFloat = true } }
    };

    public static Vector4[] ReadDDSAsFloat4(string fileName)
    {
        //using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        string path = GetFilePath(fileName);
        FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        // Read and verify magic number
        if (reader.ReadUInt32() != DDS_MAGIC)
            throw new InvalidDataException("Invalid DDS file");

        // Read DDS header
        var header = ReadStructure<DDS_HEADER>(reader);
        if (header.dwSize != DDS_HEADER_SIZE)
            throw new InvalidDataException("Invalid DDS header size");

        FormatInfo formatInfo;
        DXGI_FORMAT dxgiFormat = 0;

        // Check for DX10 format
        if (header.ddspf.dwFourCC == DDS_DXT10_FOURCC)
        {
            dxgiFormat = (DXGI_FORMAT)reader.ReadUInt32();
            // Skip the remaining 16 bytes of the DDS_HEADER_DXT10 structure
            reader.BaseStream.Seek(16, SeekOrigin.Current);
            if (!DxgiFormatMap.TryGetValue(dxgiFormat, out formatInfo))
                throw new NotSupportedException($"Unsupported DXGI format: {dxgiFormat}");
        }
        else
        {
            if (!LegacyFormatMap.TryGetValue(header.ddspf.dwFourCC, out formatInfo))
                throw new NotSupportedException($"Unsupported FourCC format: {header.ddspf.dwFourCC}");
        }

        // Verify required header flags
        if ((header.dwFlags & DDS_HEADER_FLAGS.WIDTH) == 0 ||
            (header.dwFlags & DDS_HEADER_FLAGS.HEIGHT) == 0)
            throw new InvalidDataException("DDS header missing width/height flags");

        // Calculate data metrics
        uint width = header.dwWidth;
        uint height = header.dwHeight;
        int bytesPerPixel = formatInfo.ComponentCount * formatInfo.BytesPerComponent;
        int rowPitch = CalculateRowPitch(header, bytesPerPixel);
        long dataSize = rowPitch * height;

        // Read pixel data
        byte[] pixelData = reader.ReadBytes((int)dataSize);
        Vector4[] result = new Vector4[width * height];

        UnityEngine.Debug.Log(fileName + " binary width " + width + " binary height " + height);

        // Convert pixel data to Vector4 array
        ConvertPixelData(pixelData, result, width, height, rowPitch, formatInfo);

        return result;
    }

    private static int CalculateRowPitch(DDS_HEADER header, int bytesPerPixel)
    {
        if ((header.dwFlags & DDS_HEADER_FLAGS.PITCH) != 0)
            return (int)header.dwPitchOrLinearSize;

        if ((header.dwFlags & DDS_HEADER_FLAGS.LINEAR_SIZE) != 0)
            return (int)(header.dwPitchOrLinearSize / header.dwHeight);

        return (int)header.dwWidth * bytesPerPixel;
    }

    private static void ConvertPixelData(byte[] source, Vector4[] destination,
        uint width, uint height, int rowPitch, FormatInfo formatInfo)
    {
        int bytesPerPixel = formatInfo.ComponentCount * formatInfo.BytesPerComponent;
        int srcIndex = 0;
        int dstIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector4 pixel = formatInfo.BytesPerComponent switch
                {
                    4 => ReadFloatPixel(source, srcIndex, formatInfo.ComponentCount),
                    2 => ReadHalfPixel(source, srcIndex, formatInfo.ComponentCount),
                    _ => throw new NotSupportedException("Unsupported component size")
                };
                destination[dstIndex++] = pixel;
                srcIndex += bytesPerPixel;
            }
            // Skip row padding
            srcIndex += rowPitch - (int)(width * bytesPerPixel);
        }
    }

    private static Vector4 ReadFloatPixel(byte[] data, int offset, int componentCount)
    {
        float r = 0, g = 0, b = 0, a = 1f;

        if (componentCount >= 1) r = BitConverter.ToSingle(data, offset);
        if (componentCount >= 2) g = BitConverter.ToSingle(data, offset + 4);
        if (componentCount >= 3) b = BitConverter.ToSingle(data, offset + 8);
        if (componentCount >= 4) a = BitConverter.ToSingle(data, offset + 12);

        return new Vector4(r, g, b, a);
    }

    private static Vector4 ReadHalfPixel(byte[] data, int offset, int componentCount)
    {
        float r = 0, g = 0, b = 0, a = 1f;

        if (componentCount >= 1) r = HalfToFloat(BitConverter.ToUInt16(data, offset));
        if (componentCount >= 2) g = HalfToFloat(BitConverter.ToUInt16(data, offset + 2));
        if (componentCount >= 3) b = HalfToFloat(BitConverter.ToUInt16(data, offset + 4));
        if (componentCount >= 4) a = HalfToFloat(BitConverter.ToUInt16(data, offset + 6));

        return new Vector4(r, g, b, a);
    }

    private static float HalfToFloat(ushort value)
    {
        // Simplified half-precision to single-precision conversion
        int sign = (value >> 15) & 0x01;
        int exponent = (value >> 10) & 0x1F;
        int mantissa = value & 0x03FF;

        if (exponent == 0)
        {
            if (mantissa == 0) return sign * 0f;
            return (sign * -1f) * (float)(mantissa / 1024.0) * (float)Math.Pow(2, -14);
        }
        if (exponent == 31)
        {
            if (mantissa == 0) return sign * float.PositiveInfinity;
            return float.NaN;
        }

        float result = (float)(1.0 + mantissa / 1024.0) *
                       (float)Math.Pow(2, exponent - 15);
        return sign * -result;
    }

    private static T ReadStructure<T>(BinaryReader reader) where T : struct
    {
        byte[] buffer = new byte[Marshal.SizeOf<T>()];
        reader.Read(buffer, 0, buffer.Length);
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }


    private static string GetFilePath(string fileName)
    {
        // Combine with StreamingAssets path
        string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, fileName);

        return path;
        // Handle Editor vs Build path differences
        #if UNITY_EDITOR
                return "file://" + path;  // Editor requires file:// prefix
        #else
                return path;  // Build uses direct path
        #endif
    }
}