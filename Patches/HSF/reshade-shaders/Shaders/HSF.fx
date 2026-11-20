// HSF (Hunter's Sloppy Frames) by Capt-Hunter: asks iMMERSE LaunchPad for its optical flow, so the HSF add-on can read it directly
// (Deferred::MotionVectorsTex, delta UV from this frame to the previous one) without GITC Uplift in between.
// LaunchPad computes optical flow only for frames some effect asked for, and this technique does nothing but ask. Its place in the list does not
// matter: LaunchPad clears the requests at the end of its own technique, so a request made anywhere holds until LaunchPad's next run. The add-on
// switches it on when its motion source is LaunchPad. Without LaunchPad installed it only draws one point into a 1x1 texture.

#define HSF_LAUNCHPAD 0
#if __RESHADE__ >= 60000
  #if exists "MartysMods/mmx_deferred.fxh"
    #include "MartysMods/mmx_deferred.fxh"
    #undef HSF_LAUNCHPAD
    #define HSF_LAUNCHPAD 1
  #elif exists "iMMERSE/MartysMods/mmx_deferred.fxh"
    #include "iMMERSE/MartysMods/mmx_deferred.fxh"
    #undef HSF_LAUNCHPAD
    #define HSF_LAUNCHPAD 1
  #endif
#endif

texture HSFMarkerTarget { Width = 1; Height = 1; Format = R8; };
float4 HSFMarkerVS(uint id : SV_VertexID) : SV_Position { return float4(0.0, 0.0, 0.0, 1.0); }
float HSFMarkerPS(float4 position : SV_Position) : SV_Target { return 0.0; }

// The game's depth buffer, as ReShade's Generic Depth picks it. The add-on reads what this texture is bound to and hands it to DLSS-G; the pass only
// makes sure the effect uses the texture.
texture HSFDepth : DEPTH;
sampler HSFDepthSampler { Texture = HSFDepth; };
texture HSFDepthMarkerTarget { Width = 1; Height = 1; Format = R32F; };
float HSFDepthPS(float4 position : SV_Position) : SV_Target { return tex2Dfetch(HSFDepthSampler, int2(0, 0)).x; }

technique HSF_Depth <
  ui_label = "HSF depth (for DLSS Frame Generation)";
  ui_tooltip = "Lets HSF read the game's depth buffer. HSF switches it on by itself while it sends depth to DLSS-G; it costs nothing on its own.";
>
{
  pass Marker
  {
    PrimitiveTopology = POINTLIST;
    VertexCount = 1;
    VertexShader = HSFMarkerVS;
    PixelShader = HSFDepthPS;
    RenderTarget = HSFDepthMarkerTarget;
  }
}

// The same depth in a texture of HSF's own, picture-sized like LaunchPad's flow. On Direct3D 12 the add-on reads this: ReShade has already moved the game's own buffer back by the time the add-on's
// event runs, while this one is always readable, and its size and format are known.
texture HSFDepthCopy { Width = BUFFER_WIDTH; Height = BUFFER_HEIGHT; Format = R32F; };
sampler HSFDepthPointSampler { Texture = HSFDepth; MinFilter = POINT; MagFilter = POINT; MipFilter = POINT; };
void HSFFullVS(uint id : SV_VertexID, out float4 position : SV_Position, out float2 uv : TEXCOORD0) {
  uv = float2((id == 2) ? 2.0 : 0.0, (id == 1) ? 2.0 : 0.0);
  position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}
// Where the depth goes in the copy, set by the add-on: xy = its top left corner, zw = its size, both as parts of the picture. The whole picture
// unless the depth has another shape (an emulator drawing a 16:9 picture centred on a wider screen); then the bars take the depth at its edge.
uniform float4 HSFDepthFit = float4(0.0, 0.0, 1.0, 1.0);
float HSFDepthCopyPS(float4 position : SV_Position, float2 uv : TEXCOORD0) : SV_Target {
  return tex2Dlod(HSFDepthPointSampler, float4(saturate((uv - HSFDepthFit.xy) / HSFDepthFit.zw), 0.0, 0.0)).x;
}

technique HSF_DepthCopy <
  ui_label = "HSF depth copy (for DLSS Frame Generation on Direct3D 12)";
  ui_tooltip = "HSF switches it on by itself in a Direct3D 12 game that it brings Streamline into.";
>
{
  pass Copy
  {
    VertexShader = HSFFullVS;
    PixelShader = HSFDepthCopyPS;
    RenderTarget = HSFDepthCopy;
  }
}

technique HSF_Motion <
  ui_label = "HSF motion (asks LaunchPad for optical flow)";
  ui_tooltip = "Lets HSF read iMMERSE LaunchPad's optical flow for frame generation. HSF switches it on by itself when its motion source is "
               "LaunchPad; it costs nothing on its own (LaunchPad does the work).";
>
{
#if HSF_LAUNCHPAD
  #ifdef IPC_REQUEST_FEATURE
  IPC_REQUEST_FEATURE(MARTYSMODS_IPC_FEATURE_OPTICALFLOW)
  #endif
#endif
  pass Marker
  {
    PrimitiveTopology = POINTLIST;
    VertexCount = 1;
    VertexShader = HSFMarkerVS;
    PixelShader = HSFMarkerPS;
    RenderTarget = HSFMarkerTarget;
  }
}
