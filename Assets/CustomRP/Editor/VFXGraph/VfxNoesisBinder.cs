#if HAS_VFX_GRAPH
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.VFX;
using System;
using NoesisRender;

namespace NoesisRender.VFX
{
    class VfxNoesisBinder : VFXSRPBinder
    {
        public override string templatePath { get { return "Assets/CustomRP/Editor/VFXGraph/Shaders"; } }
        public override string runtimePath { get { return "Assets/CustomRP/ShaderLibrary/VFXGraph"; } }

        public override string SRPAssetTypeStr { get { return /*CustomRenderPipeline.Name*/ "CustomRenderPipelineAsset"; } } // Pretend that it a URP

        public override Type SRPOutputDataType { get { return null; } } // null by now but use VFXURPSubOutput when there is a need to store URP specific data

        /*public override string SRPAssetTypeStr
        {
        get
        {
        return typeof().Name; 
        } 
        }*/

        //public override Type SRPOutputDataType => null;
        public override bool IsShaderVFXCompatible(Shader shader) => true;
    }
}
#endif