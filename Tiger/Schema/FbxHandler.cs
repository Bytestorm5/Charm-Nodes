﻿using Internal.Fbx;
using SharpDX;

namespace Tiger.Schema;

public class FbxHandler
{
    private readonly FbxManager _manager;
    private readonly FbxScene _scene;
    public InfoConfigHandler InfoHandler;
    private static object _fbxLock = new object();
    public FbxHandler(bool bMakeInfoHandler = true)
    {
        lock (_fbxLock) // bc fbx is not thread-safe
        {
            _manager = FbxManager.Create();
            _scene = FbxScene.Create(_manager, "");
        }

        if (bMakeInfoHandler)
            InfoHandler = new InfoConfigHandler();
    }

    public FbxMesh AddMeshPartToScene(MeshPart part, int index, string meshName)
    {
        FbxMesh mesh = CreateMeshPart(part, index, meshName);
        FbxNode node;
        lock (_fbxLock)
        {
            node = FbxNode.Create(_manager, mesh.GetName());
        }
        node.SetNodeAttribute(mesh);

        if (part.VertexNormals.Count > 0)
        {
            AddNormalsToMesh(mesh, part);
        }

        if (part.VertexTangents.Count > 0)
        {
            AddTangentsToMesh(mesh, part);
        }

        if (part.VertexTexcoords0.Count > 0)
        {
            AddTexcoordsToMesh(mesh, part);
        }

        if (part.VertexColours.Count > 0)
        {
            AddColoursToMesh(mesh, part);
        }

        if ((part as DynamicMeshPart)?.VertexColourSlots.Count > 0 || (part as DynamicMeshPart)?.GearDyeChangeColorIndex != 0xFF)  // api item, so do slots and uv1
        {
            AddSlotColoursToMesh(mesh, part as DynamicMeshPart);
            AddTexcoords1ToMesh(mesh, part);
        }

        // for importing to other engines
        if (InfoHandler != null && part.Material != null) // todo consider why some materials are null
        {
            InfoHandler.AddMaterial(part.Material);
            InfoHandler.AddPart(part, node.GetName());
        }


        AddMaterial(mesh, node, index, part.Material);
        AddSmoothing(mesh);

        lock (_fbxLock)
        {
            //node.LclRotation.Set(new FbxDouble3(-90, 0, 0)); //This is fucking up the source 2 map import for whatever reason and I cant be bothered to suffer through that again
            _scene.GetRootNode().AddChild(node);
        }

        return mesh;
    }

    private FbxMesh CreateMeshPart(MeshPart part, int index, string meshName)
    {
        bool done = false;
        FbxMesh mesh;
        lock (_fbxLock)
        {
            mesh = FbxMesh.Create(_manager, $"{meshName}_Group{part.GroupIndex}_Index{part.Index}_{index}_{part.LodCategory}");
        }

        // Conversion lookup table
        Dictionary<int, int> lookup = new Dictionary<int, int>();
        for (int i = 0; i < part.VertexIndices.Count; i++)
        {
            lookup[(int)part.VertexIndices[i]] = i;
        }
        foreach (int vertexIndex in part.VertexIndices.OfType<int>())
        {
            // todo utilise dictionary to make this control point thing better maybe?
            var pos = part.VertexPositions[lookup[vertexIndex]];
            mesh.SetControlPointAt(new FbxVector4(pos.X, pos.Y, pos.Z, 1), lookup[vertexIndex]);
        }
        foreach (var face in part.Indices)
        {
            mesh.BeginPolygon();
            mesh.AddPolygon(lookup[(int)face.X]);
            mesh.AddPolygon(lookup[(int)face.Y]);
            mesh.AddPolygon(lookup[(int)face.Z]);
            mesh.EndPolygon();
        }

        mesh.CreateLayer();
        return mesh;
    }

    private void AddNormalsToMesh(FbxMesh mesh, MeshPart part)
    {
        FbxLayerElementNormal normalsLayer;
        lock (_fbxLock)
        {
            normalsLayer = FbxLayerElementNormal.Create(mesh, "normalLayerName");
        }
        normalsLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        normalsLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        // Check if quaternion
        foreach (var normal in part.VertexNormals)
        {
            normalsLayer.GetDirectArray().Add(new FbxVector4(normal.X, normal.Y, normal.Z));
        }
        mesh.GetLayer(0).SetNormals(normalsLayer);
    }

    private void AddTangentsToMesh(FbxMesh mesh, MeshPart part)
    {
        FbxLayerElementTangent tangentsLayer;
        lock (_fbxLock)
        {
            tangentsLayer = FbxLayerElementTangent.Create(mesh, "tangentLayerName");
        }
        tangentsLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        tangentsLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        // todo more efficient to do AddMultiple
        foreach (var tangent in part.VertexTangents)
        {
            tangentsLayer.GetDirectArray().Add(new FbxVector4(tangent.X, tangent.Y, tangent.Z));
        }
        mesh.GetLayer(0).SetTangents(tangentsLayer);
    }


    private void AddTexcoordsToMesh(FbxMesh mesh, MeshPart part)
    {
        FbxLayerElementUV uvLayer;
        lock (_fbxLock)
        {
            uvLayer = FbxLayerElementUV.Create(mesh, "uv0");
        }
        uvLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        uvLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        foreach (var tx in part.VertexTexcoords0)
        {
            uvLayer.GetDirectArray().Add(new FbxVector2(tx.X, tx.Y));
        }
        mesh.GetLayer(0).SetUVs(uvLayer);
    }

    private void AddTexcoords1ToMesh(FbxMesh mesh, MeshPart part)
    {
        FbxLayerElementUV uvLayer;
        lock (_fbxLock)
        {
            uvLayer = FbxLayerElementUV.Create(mesh, "uv1");
        }
        uvLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        uvLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        foreach (var tx in part.VertexTexcoords1)
        {
            uvLayer.GetDirectArray().Add(new FbxVector2(tx.X, tx.Y));
        }
        if (mesh.GetLayer(1) == null)
            mesh.CreateLayer();
        mesh.GetLayer(1).SetUVs(uvLayer);
    }


    private void AddColoursToMesh(FbxMesh mesh, MeshPart part)
    {
        FbxLayerElementVertexColor colLayer;
        lock (_fbxLock)
        {
            colLayer = FbxLayerElementVertexColor.Create(mesh, "colourLayerName");
        }
        colLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        colLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        foreach (var colour in part.VertexColours)
        {
            colLayer.GetDirectArray().Add(new FbxColor(colour.X, colour.Y, colour.Z, colour.W));
        }
        mesh.GetLayer(0).SetVertexColors(colLayer);
    }

    private void AddSlotColoursToMesh(FbxMesh mesh, DynamicMeshPart part)
    {
        FbxLayerElementVertexColor colLayer;
        lock (_fbxLock)
        {
            colLayer = FbxLayerElementVertexColor.Create(mesh, "slots");
        }
        colLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
        colLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
        if (part.PrimitiveType == EPrimitiveType.Triangles)
        {
            VertexBuffer.AddVertexColourSlotInfo(part, part.GearDyeChangeColorIndex);
            for (var i = 0; i < part.VertexPositions.Count; i++)
            {
                colLayer.GetDirectArray().Add(new FbxColor(part.VertexColourSlots[0].X, part.VertexColourSlots[0].Y, part.VertexColourSlots[0].Z, part.VertexColourSlots[0].W));
            }
        }
        else
        {
            foreach (var colour in part.VertexColourSlots)
            {
                colLayer.GetDirectArray().Add(new FbxColor(colour.X, colour.Y, colour.Z, colour.W));
            }
        }

        if (mesh.GetLayer(1) == null)
            mesh.CreateLayer();
        mesh.GetLayer(1).SetVertexColors(colLayer);
    }

    private void AddMaterial(FbxMesh mesh, FbxNode node, int index, Material material)
    {
        FbxSurfacePhong fbxMaterial;
        FbxLayerElementMaterial materialLayer;
        lock (_fbxLock)
        {
            fbxMaterial = FbxSurfacePhong.Create(_scene, material.Hash);
            materialLayer = FbxLayerElementMaterial.Create(mesh, $"matlayer_{node.GetName()}_{index}");
        }
        fbxMaterial.DiffuseFactor.Set(1);
        node.SetShadingMode(FbxNode.EShadingMode.eTextureShading);
        node.AddMaterial(fbxMaterial);

        // if this doesnt exist, it wont load the material slots in unreal
        materialLayer.SetMappingMode(FbxLayerElement.EMappingMode.eAllSame);
        mesh.GetLayer(0).SetMaterials(materialLayer);
    }

    private void AddSmoothing(FbxMesh mesh)
    {
        FbxLayerElementSmoothing smoothingLayer;
        lock (_fbxLock)
        {
            smoothingLayer = FbxLayerElementSmoothing.Create(mesh, $"smoothingLayerName");
        }
        smoothingLayer.SetMappingMode(FbxLayerElement.EMappingMode.eByEdge);
        smoothingLayer.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);

        FbxArrayInt edges = mesh.mEdgeArray;
        List<int> sharpEdges = new List<int>();
        var numEdges = edges.GetCount();
        for (int i = 0; i < numEdges; i++)
        {
            smoothingLayer.GetDirectArray().Add(i);
        }

        mesh.GetLayer(0).SetSmoothing(smoothingLayer);

        mesh.SetMeshSmoothness(FbxMesh.ESmoothness.eFine);
    }

    // public List<FbxNode> AddSkeleton(List<BoneNode> boneNodes)
    // {
    //     FbxNode rootNode = null;
    //     List<FbxNode> skeletonNodes = new List<FbxNode>();
    //     foreach (var boneNode in boneNodes)
    //     {
    //         FbxSkeleton skeleton;
    //         FbxNode node;
    //         lock (_fbxLock)
    //         {
    //             skeleton = FbxSkeleton.Create(_manager, boneNode.Hash.ToString());
    //             node = FbxNode.Create(_manager, boneNode.Hash.ToString());
    //         }
    //         skeleton.SetSkeletonType(FbxSkeleton.EType.eLimbNode);
    //         node.SetNodeAttribute(skeleton);
    //         Vector3 location = boneNode.DefaultObjectSpaceTransform.Translation;
    //         if (boneNode.ParentNodeIndex != -1)
    //         {
    //             location -= boneNodes[boneNode.ParentNodeIndex].DefaultObjectSpaceTransform.Translation;
    //         }
    //         node.LclTranslation.Set(new FbxDouble3(location.X, location.Y, location.Z));
    //         if (rootNode == null)
    //         {
    //             skeleton.SetSkeletonType(FbxSkeleton.EType.eRoot);
    //             rootNode = node;
    //         }
    //         else
    //         {
    //             skeletonNodes[boneNode.ParentNodeIndex].AddChild(node);
    //         }
    //         skeletonNodes.Add(node);
    //     }
    //
    //     _scene.GetRootNode().AddChild(rootNode);
    //     return skeletonNodes;
    // }

    public void ExportScene(string fileName)
    {
        // Make directory for file
        string directory = Path.GetDirectoryName(fileName);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        lock (_fbxLock)
        {
            if (_manager.GetIOSettings() == null)
            {
                FbxIOSettings ios = FbxIOSettings.Create(_manager, FbxWrapperNative.IOSROOT);
                _manager.SetIOSettings(ios);
            }
            _manager.GetIOSettings().SetBoolProp(FbxWrapperNative.EXP_FBX_MATERIAL, true);
            _manager.GetIOSettings().SetBoolProp(FbxWrapperNative.EXP_FBX_TEXTURE, true);
            _manager.GetIOSettings().SetBoolProp(FbxWrapperNative.EXP_FBX_EMBEDDED, true);
            _manager.GetIOSettings().SetBoolProp(FbxWrapperNative.EXP_FBX_ANIMATION, true);
            _manager.GetIOSettings().SetBoolProp(FbxWrapperNative.EXP_FBX_GLOBAL_SETTINGS, true);
            var exporter = FbxExporter.Create(_manager, "");
            exporter.Initialize(fileName, -1);  // -1 == use binary not ascii, binary is more space efficient
            exporter.Export(_scene);
            exporter.Destroy();
        }
        _scene.Clear();
        if (InfoHandler != null)
            InfoHandler.WriteToFile(directory);

    }

    // public void AddEntityToScene(Entity entity, List<DynamicPart> dynamicParts, ELOD detailLevel, List<FbxNode> skeletonNodes = null)
    // {
    //     if (skeletonNodes == null)
    //     {
    //         skeletonNodes = new List<FbxNode>();
    //     }
    //     // _scene.GetRootNode().LclRotation.Set(new FbxDouble3(90, 0, 0));
    //     // List<FbxNode> skeletonNodes = new List<FbxNode>();
    //     if (entity.Skeleton != null)
    //     {
    //         skeletonNodes = AddSkeleton(entity.Skeleton.GetBoneNodes());
    //     }
    //     for( int i = 0; i < dynamicParts.Count; i++)
    //     {
    //         var dynamicPart = dynamicParts[i];
    //         FbxMesh mesh = AddMeshPartToScene(dynamicPart, i, entity.Hash);
    //
    //         if (dynamicPart.VertexWeights.Count > 0)
    //         {
    //             if (skeletonNodes.Count > 0)
    //             {
    //                 AddWeightsToMesh(mesh, dynamicPart, skeletonNodes);
    //             }
    //         }
    //     }
    // }

    public void AddStaticToScene(List<StaticPart> parts, string meshName)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            StaticPart part = parts[i];
            AddMeshPartToScene(part, i, meshName);
        }
    }

    public void Clear()
    {
        _scene.Clear();
    }

    public void Dispose()
    {
        lock (_fbxLock)
        {
            _scene.Destroy();
            _manager.Destroy();
        }
        if (InfoHandler != null)
            InfoHandler.Dispose();
    }

    public void AddStaticInstancesToScene(List<StaticPart> parts, List<SStaticMeshInstanceTransform> instances, string meshName)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            FbxMesh mesh = CreateMeshPart(parts[i], i, meshName);
            for (int j = 0; j < instances.Count; j++)
            {
                FbxNode node;
                lock (_fbxLock)
                {
                    node = FbxNode.Create(_manager, $"{meshName}_{i}_{j}");
                }
                node.SetNodeAttribute(mesh);
                Quaternion quatRot = new Quaternion(instances[j].Rotation.X, instances[j].Rotation.Y, instances[j].Rotation.Z, instances[j].Rotation.W);
                System.Numerics.Vector3 eulerRot = QuaternionToEulerAngles(quatRot);

                node.LclTranslation.Set(new FbxDouble3(instances[j].Position.X, instances[j].Position.Y, instances[j].Position.Z));
                node.LclRotation.Set(new FbxDouble3(eulerRot.X, eulerRot.Y, eulerRot.Z));
                node.LclScaling.Set(new FbxDouble3(instances[j].Scale.X, instances[j].Scale.X, instances[j].Scale.X));

                lock (_fbxLock)
                {
                    _scene.GetRootNode().AddChild(node);
                }
            }
        }
    }

    // public void AddDynamicPointsToScene(D2Class_85988080 points, string meshName, FbxHandler dynamicHandler)
    // {
    //     Entity entity = PackageHandler.GetTag(typeof(Entity), points.Entity.Hash);
    //     if(entity.Model != null)
    //     {
    //         meshName += "_Model";
    //         //Console.WriteLine($"{meshName} has geometry");
    //         //dynamicHandler.AddEntityToScene(entity, entity.Model.Load(ELOD.MostDetail, points.Entity.ModelParentResource), ELOD.MostDetail);
    //     }
    //
    //     FbxNode node;
    //     lock (_fbxLock)
    //     {
    //         node = FbxNode.Create(_manager, $"{meshName}");
    //     }
    //     Quaternion quatRot = new Quaternion(points.Rotation.X, points.Rotation.Y, points.Rotation.Z, points.Rotation.W);
    //     System.Numerics.Vector3 eulerRot = QuaternionToEulerAngles(quatRot);
    //
    //     node.LclTranslation.Set(new FbxDouble3(points.Translation.X*100, points.Translation.Y*100, points.Translation.Z*100));
    //     node.LclRotation.Set(new FbxDouble3(eulerRot.X, eulerRot.Y, eulerRot.Z));
    //     node.LclScaling.Set(new FbxDouble3(100,100,100));
    //
    //     // Scale and rotate
    //     //ScaleAndRotateForBlender(node);
    //
    //     lock (_fbxLock)
    //     {
    //         _scene.GetRootNode().AddChild(node);
    //     }
    // }

    // From https://github.com/OwlGamingCommunity/V/blob/492d0cb3e89a97112ac39bf88de39da57a3a1fbf/Source/owl_core/Server/MapLoader.cs
    private static System.Numerics.Vector3 QuaternionToEulerAngles(Quaternion q)
    {
        System.Numerics.Vector3 retVal = new System.Numerics.Vector3();

        // roll (x-axis rotation)
        double sinr_cosp = +2.0 * (q.W * q.X + q.Y * q.Z);
        double cosr_cosp = +1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
        retVal.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

        // pitch (y-axis rotation)
        double sinp = +2.0 * (q.W * q.Y - q.Z * q.X);
        double absSinP = Math.Abs(sinp);
        bool bSinPOutOfRage = absSinP >= 1.0;
        if (bSinPOutOfRage)
        {
            retVal.Y = 90.0f; // use 90 degrees if out of range
        }
        else
        {
            retVal.Y = (float)Math.Asin(sinp);
        }

        // yaw (z-axis rotation)
        double siny_cosp = +2.0 * (q.W * q.Z + q.X * q.Y);
        double cosy_cosp = +1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
        retVal.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

        // Rad to Deg
        retVal.X *= (float)(180.0f / Math.PI);

        if (!bSinPOutOfRage) // only mult if within range
        {
            retVal.Y *= (float)(180.0f / Math.PI);
        }
        retVal.Z *= (float)(180.0f / Math.PI);

        return retVal;
    }
}