
#ifdef TESSELLATION_ON

VertexDescriptionInputs VaryingsMeshToDSToVertexDescriptionInputs(VaryingsMeshToDS input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    $VertexDescriptionInputs.ObjectSpaceNormal:         output.ObjectSpaceNormal = TransformWorldToObjectNormal(input.normalWS);
    $VertexDescriptionInputs.WorldSpaceNormal:          output.WorldSpaceNormal = input.normalWS;
    $VertexDescriptionInputs.ViewSpaceNormal:           output.ViewSpaceNormal = TransformWorldToViewDir(output.WorldSpaceNormal);
    $VertexDescriptionInputs.TangentSpaceNormal:        output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
    $VertexDescriptionInputs.ObjectSpaceTangent:        output.ObjectSpaceTangent = TransformWorldToObjectDir(input.tangentWS.xyz);
    $VertexDescriptionInputs.WorldSpaceTangent:         output.WorldSpaceTangent = input.tangentWS.xyz;
    $VertexDescriptionInputs.ViewSpaceTangent:          output.ViewSpaceTangent = TransformWorldToViewDir(output.WorldSpaceTangent);
    $VertexDescriptionInputs.TangentSpaceTangent:       output.TangentSpaceTangent = float3(1.0f, 0.0f, 0.0f);
    $VertexDescriptionInputs.WorldSpaceBiTangent:       output.WorldSpaceBiTangent = normalize(cross(input.normalWS, input.tangentWS) * (input.tangentWS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
    $VertexDescriptionInputs.ObjectSpaceBiTangent:      output.ObjectSpaceBiTangent = TransformWorldToObjectDir(output.WorldSpaceBiTangent);
    $VertexDescriptionInputs.ViewSpaceBiTangent:        output.ViewSpaceBiTangent = TransformWorldToViewDir(output.WorldSpaceBiTangent);
    $VertexDescriptionInputs.TangentSpaceBiTangent:     output.TangentSpaceBiTangent = float3(0.0f, 1.0f, 0.0f);
    $VertexDescriptionInputs.ObjectSpacePosition:       output.ObjectSpacePosition = TransformWorldToObject(input.positionRWS);
    $VertexDescriptionInputs.WorldSpacePosition:        output.WorldSpacePosition = input.positionRWS; 
    $VertexDescriptionInputs.ViewSpacePosition:         output.ViewSpacePosition = TransformWorldToView(output.WorldSpacePosition);
    $VertexDescriptionInputs.TangentSpacePosition:      output.TangentSpacePosition = float3(0.0f, 0.0f, 0.0f);
    $VertexDescriptionInputs.AbsoluteWorldSpacePosition:output.AbsoluteWorldSpacePosition = GetAbsolutePositionWS(input.positionRWS);
    $VertexDescriptionInputs.WorldSpaceViewDirection:   output.WorldSpaceViewDirection = GetWorldSpaceNormalizeViewDir(output.WorldSpacePosition);
    $VertexDescriptionInputs.ObjectSpaceViewDirection:  output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);
    $VertexDescriptionInputs.ViewSpaceViewDirection:    output.ViewSpaceViewDirection = TransformWorldToViewDir(output.WorldSpaceViewDirection);
    $VertexDescriptionInputs.TangentSpaceViewDirection: float3x3 tangentSpaceTransform = float3x3(output.WorldSpaceTangent, output.WorldSpaceBiTangent, output.WorldSpaceNormal);
    $VertexDescriptionInputs.TangentSpaceViewDirection: output.TangentSpaceViewDirection = TransformWorldToTangent(output.WorldSpaceViewDirection, tangentSpaceTransform);
    $VertexDescriptionInputs.ScreenPosition:            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(output.WorldSpacePosition), _ProjectionParams.x);
    $VertexDescriptionInputs.uv0:                       output.uv0 = input.texCoord0;
    $VertexDescriptionInputs.uv1:                       output.uv1 = input.texCoord1;
    $VertexDescriptionInputs.uv2:                       output.uv2 = input.texCoord2;
    $VertexDescriptionInputs.uv3:                       output.uv3 = input.texCoord3;
    $VertexDescriptionInputs.VertexColor:               output.VertexColor = input.color;
    $VertexDescriptionInputs.TimeParameters:            output.TimeParameters = _TimeParameters.xyz; // Note: in case of animation this will be overwrite (allow to handle motion vector)
    //$VertexDescriptionInputs.BoneWeights:             output.BoneWeights = input.weights; // undefined for Hull shader
    //$VertexDescriptionInputs.BoneIndices:             output.BoneIndices = input.indices; // undefined for Hull shader
    //$VertexDescriptionInputs.VertexID:                output.VertexID = input.vertexID;   // undefined for Hull shader

    return output;
}

float GetTessellationFactor(VaryingsMeshToDS input)
{
    float tessellationFactor = 1.0;
    // HACK: As there is no specific tessellation stage for now in shadergraph, we reuse the vertex description mechanism.
    // It mean we store TessellationFactor inside vertex description causing extra read on both vertex and hull stage, but unusued paramater are optimize out by the shader compiler, so no impact.
    VertexDescriptionInputs vertexDescriptionInputs = VaryingsMeshToDSToVertexDescriptionInputs(input);
    VertexDescription vertexDescription = VertexDescriptionFunction(vertexDescriptionInputs);
    $VertexDescription.TessellationFactor: tessellationFactor = vertexDescription.TessellationFactor;

    return tessellationFactor;
}

float GetMaxDisplacement()
{
    return _TessellationMaxDisplacement;
}

// TODO => must take into account custom interpolator
VaryingsMeshToDS InterpolateWithBaryCoordsMeshToDS(VaryingsMeshToDS input0, VaryingsMeshToDS input1, VaryingsMeshToDS input2, float3 baryCoords)
{
    VaryingsMeshToDS output;

    UNITY_TRANSFER_INSTANCE_ID(input0, output);

    TESSELLATION_INTERPOLATE_BARY(positionRWS, baryCoords);
    TESSELLATION_INTERPOLATE_BARY(normalWS, baryCoords);
#ifdef VARYINGS_DS_NEED_TANGENT
    // This will interpolate the sign but should be ok in practice as we may expect a triangle to have same sign (? TO CHECK)
    TESSELLATION_INTERPOLATE_BARY(tangentWS, baryCoords);
#endif
#ifdef VARYINGS_DS_NEED_TEXCOORD0
    TESSELLATION_INTERPOLATE_BARY(texCoord0, baryCoords);
#endif
#ifdef VARYINGS_DS_NEED_TEXCOORD1
    TESSELLATION_INTERPOLATE_BARY(texCoord1, baryCoords);
#endif
#ifdef VARYINGS_DS_NEED_TEXCOORD2
    TESSELLATION_INTERPOLATE_BARY(texCoord2, baryCoords);
#endif
#ifdef VARYINGS_DS_NEED_TEXCOORD3
    TESSELLATION_INTERPOLATE_BARY(texCoord3, baryCoords);
#endif
#ifdef VARYINGS_DS_NEED_COLOR
    TESSELLATION_INTERPOLATE_BARY(color, baryCoords);
#endif

    return output;
}

#ifdef HAVE_TESSELLATION_MODIFICATION
// tessellationFactors
// x - 1->2 edge
// y - 2->0 edge
// z - 0->1 edge
// w - inside tessellation factor
VaryingsMeshToDS ApplyTessellationModification(VaryingsMeshToDS input, float3 timeParameters)
{
    // HACK: As there is no specific tessellation stage for now in shadergraph, we reuse the vertex description mechanism.
    // It mean we store TessellationFactor inside vertex description causing extra read on both vertex and hull stage, but unusued paramater are optimize out by the shader compiler, so no impact.
    VertexDescriptionInputs vertexDescriptionInputs = VaryingsMeshToDSToVertexDescriptionInputs(input);
    // Override time paramters with used one (This is required to correctly handle motion vector for tessellation animation based on time)
    $VertexDescriptionInputs.TimeParameters: vertexDescriptionInputs.TimeParameters = timeParameters;

    VertexDescription vertexDescription = VertexDescriptionFunction(vertexDescriptionInputs);
    $VertexDescription.TessellationPosition: input.positionRWS = vertexDescription.TessellationPosition;

    // TODO: Check custom interpolator
    // The purpose of the above ifdef, this allows shader graph custom interpolators to write directly to the varyings structs.
    // $splice(CustomInterpolatorVertexDefinitionToVaryings)

    return input;
}

#endif

#endif // TESSELLATION_ON
