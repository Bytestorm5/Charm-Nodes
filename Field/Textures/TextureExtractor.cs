﻿using System.ComponentModel;
using DirectXTexNet;

namespace Field;

public class TextureExtractor
{
    private static ETextureFormat _format = ETextureFormat.DDS_BGRA_UNCOMP_DX10;
    
    public static void SetTextureFormat(ETextureFormat textureFormat)
    {
        _format = textureFormat;
    }

    public static bool SaveTextureToFile(string savePath, ScratchImage scratchImage)
    {
        if (savePath.Contains('.'))
        {
            return false;
        }

        switch (_format)
        {
            case ETextureFormat.DDS_BGRA_UNCOMP_DX10:
                scratchImage.SaveToDDSFile(DDS_FLAGS.FORCE_DX10_EXT, savePath + ".dds");
                break;
            case ETextureFormat.DDS_BGRA_BC7_DX10:
                if (TexHelper.Instance.IsSRGB(scratchImage.GetMetadata().Format))
                    scratchImage = scratchImage.Compress(DXGI_FORMAT.BC7_UNORM_SRGB, TEX_COMPRESS_FLAGS.SRGB, 0);
                else
                    scratchImage = scratchImage.Compress(DXGI_FORMAT.BC7_UNORM, TEX_COMPRESS_FLAGS.DEFAULT, 0);

                scratchImage.SaveToDDSFile(DDS_FLAGS.FORCE_DX9_LEGACY, savePath + ".dds");
                break;
            case ETextureFormat.DDS_BGRA_UNCOMP:
                scratchImage.SaveToDDSFile(DDS_FLAGS.FORCE_DX9_LEGACY, savePath + ".dds");
                break;
            case ETextureFormat.PNG:
                Guid guid = TexHelper.Instance.GetWICCodec(WICCodecs.PNG);
                scratchImage.SaveToWICFile(0, WIC_FLAGS.NONE, guid, savePath + ".png");
                break;
            case ETextureFormat.TGA:
                scratchImage.SaveToTGAFile(0, savePath + ".tga");
                break;
        }
        scratchImage.Dispose();
        return true;
    }
    public static string GetExtension(ETextureFormat format)
    {
        switch (format)
        {
            case ETextureFormat.DDS_BGRA_UNCOMP_DX10:
            case ETextureFormat.DDS_BGRA_BC7_DX10:
            case ETextureFormat.DDS_BGRA_UNCOMP:
                return "dds";
            case ETextureFormat.PNG:
                return "png";
            case ETextureFormat.TGA:
                return "tga";
        }

        return String.Empty;
    }
    public static string GetExtension()
    {
        return GetExtension(_format);
    }
}

public enum ETextureFormat
{
    DDS_BGRA_UNCOMP_DX10,
    DDS_BGRA_BC7_DX10,
    DDS_BGRA_UNCOMP,
    PNG,
    TGA,
}