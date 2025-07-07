using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.VFX;
using System;
using NoesisRender;

namespace NoesisVFX
{
    class VfxNoesisBinder : VFXSRPBinder
    {
        public override string templatePath { get { return "Assets/CustomRP/Editor/VFXGraph/Templates"; } }
        public override string runtimePath { get { return "hlsl file path (details below)"; } }

        public override string SRPAssetTypeStr { get { return /*CustomRenderPipeline.Name*/ "Unity.RenderPipelines.Universal"; } } // Pretend that im a URP

        public override Type SRPOutputDataType => throw new NotImplementedException();

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
