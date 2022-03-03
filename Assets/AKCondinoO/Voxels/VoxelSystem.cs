#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using AKCondinoO.Voxels.Biomes;
using paulbourke.MarchingCubes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using static AKCondinoO.Voxels.VoxelSystem.TerrainEditingMultithreaded;
using static AKCondinoO.Voxels.VoxelTerrain.MarchingCubesBackgroundContainer;
namespace AKCondinoO.Voxels{
    internal class VoxelSystem:MonoBehaviour{internal static VoxelSystem Singleton;
        internal const int MaxcCoordx=312;
        internal const int MaxcCoordy=312;
        internal static Vector2Int instantiationDistance{get;}=new Vector2Int(12,12);
        internal static Vector2Int expropriationDistance{get;}=new Vector2Int(12,12);
        internal static Vector2Int navDistance{get;}=new Vector2Int(5,5);
        internal const ushort Height=(256);
        internal const ushort Width=(16);
        internal const ushort Depth=(16);
        internal const ushort FlattenOffset=(Width*Depth);
        internal const int VoxelsPerChunk=(FlattenOffset*Height);
        #region chunk
            internal static Vector2Int vecPosTocCoord(Vector3 pos){
                                                              pos.x/=(float)Width;
                                                              pos.z/=(float)Depth;
             return new Vector2Int((pos.x>0)?(pos.x-(int)pos.x==0.5f?Mathf.FloorToInt(pos.x):Mathf.RoundToInt(pos.x)):(int)Math.Round(pos.x,MidpointRounding.AwayFromZero),
                                   (pos.z>0)?(pos.z-(int)pos.z==0.5f?Mathf.FloorToInt(pos.z):Mathf.RoundToInt(pos.z)):(int)Math.Round(pos.z,MidpointRounding.AwayFromZero)
                                  );
            }
            internal static Vector2Int vecPosTocnkRgn(Vector3 pos){Vector2Int coord=vecPosTocCoord(pos);
             return new Vector2Int(coord.x*Width,
                                   coord.y*Depth);
            }
            internal static Vector2Int cnkRgnTocCoord(Vector2Int cnkRgn){return new Vector2Int(cnkRgn.x/Width,cnkRgn.y/Depth);}
            internal static Vector2Int cCoordTocnkRgn(Vector2Int cCoord){return new Vector2Int(cCoord.x*Width,cCoord.y*Depth);}
            internal static int GetcnkIdx(int cx,int cy){return cy+cx*(MaxcCoordy*2+1);}
        #endregion
        #region voxel
            internal static Vector3Int vecPosTovCoord(Vector3 pos){
             Vector2Int rgn=vecPosTocnkRgn(pos);
             pos.x=(pos.x>0)?(pos.x-(int)pos.x==0.5f?Mathf.FloorToInt(pos.x):Mathf.RoundToInt(pos.x)):(int)Math.Round(pos.x,MidpointRounding.AwayFromZero);
             pos.y=(pos.y>0)?(pos.y-(int)pos.y==0.5f?Mathf.FloorToInt(pos.y):Mathf.RoundToInt(pos.y)):(int)Math.Round(pos.y,MidpointRounding.AwayFromZero);
             pos.z=(pos.z>0)?(pos.z-(int)pos.z==0.5f?Mathf.FloorToInt(pos.z):Mathf.RoundToInt(pos.z)):(int)Math.Round(pos.z,MidpointRounding.AwayFromZero);
             Vector3Int coord=new Vector3Int((int)pos.x-rgn.x,(int)pos.y,(int)pos.z-rgn.y);
             coord.x+=Mathf.FloorToInt(Width /2.0f);coord.x=Mathf.Clamp(coord.x,0,Width -1);
             coord.y+=Mathf.FloorToInt(Height/2.0f);coord.y=Mathf.Clamp(coord.y,0,Height-1);
             coord.z+=Mathf.FloorToInt(Depth /2.0f);coord.z=Mathf.Clamp(coord.z,0,Depth -1);
             return coord;
            }
            internal static int GetvxlIdx(int vcx,int vcy,int vcz){return vcy*FlattenOffset+vcx*Depth+vcz;}
            internal static int GetoftIdx(Vector2Int offset){//  ..for neighbors
             if(offset.x== 0&&offset.y== 0)return 0;
             if(offset.x==-1&&offset.y== 0)return 1;
             if(offset.x== 1&&offset.y== 0)return 2;
             if(offset.x== 0&&offset.y==-1)return 3;
             if(offset.x==-1&&offset.y==-1)return 4;
             if(offset.x== 1&&offset.y==-1)return 5;
             if(offset.x== 0&&offset.y== 1)return 6;
             if(offset.x==-1&&offset.y== 1)return 7;
             if(offset.x== 1&&offset.y== 1)return 8;
             return -1;
            }
        #endregion
        #region validation
            internal static void ValidateCoord(ref Vector2Int region,ref Vector3Int vxlCoord){int a,c;
             a=region.x;c=vxlCoord.x;ValidateCoordAxis(ref a,ref c,Width);region.x=a;vxlCoord.x=c;
             a=region.y;c=vxlCoord.z;ValidateCoordAxis(ref a,ref c,Depth);region.y=a;vxlCoord.z=c;
            }
            internal static void ValidateCoordAxis(ref int axis,ref int coord,int axisLength){
             if      (coord<0){          axis-=axisLength*Mathf.CeilToInt (Math.Abs(coord)/(float)axisLength);coord=(coord%axisLength)+axisLength;
             }else if(coord>=axisLength){axis+=axisLength*Mathf.FloorToInt(Math.Abs(coord)/(float)axisLength);coord=(coord%axisLength);
             }
            }
        #endregion
        internal const double IsoLevel=-50.0d;
        #region Terrain
        internal enum MaterialId:ushort{
         Air=0,//  Default value
         Bedrock=1,//  Indestrutível
         Dirt=2,
         Rock=3,
         Sand=4,
        }
        internal static class AtlasHelper{
         internal static Material material{get;private set;}
         internal static readonly Vector2[]uv=new Vector2[Enum.GetNames(typeof(MaterialId)).Length];
         internal static void GetAtlasData(Material material){
          AtlasHelper.material=material;
          uv[(int)MaterialId.Dirt]=new Vector2(1,0);
          uv[(int)MaterialId.Rock]=new Vector2(0,0);
         }
        }
        internal struct Voxel{
         internal Voxel(double d,Vector3 n,MaterialId m){
          Density=d;Normal=n;Material=m;IsCreated=true;
         }
         internal double Density;
         internal Vector3 Normal;
         internal MaterialId Material;
         internal bool IsCreated;
         internal static Voxel Air    {get;}=new Voxel(  0.0,Vector3.zero,MaterialId.Air    );
         internal static Voxel Bedrock{get;}=new Voxel(101.0,Vector3.zero,MaterialId.Bedrock);
        }
        internal static readonly ReadOnlyCollection<Vector3>corners=new ReadOnlyCollection<Vector3>(new Vector3[8]{
         new Vector3(-.5f,-.5f,-.5f),
         new Vector3( .5f,-.5f,-.5f),
         new Vector3( .5f, .5f,-.5f),
         new Vector3(-.5f, .5f,-.5f),
         new Vector3(-.5f,-.5f, .5f),
         new Vector3( .5f,-.5f, .5f),
         new Vector3( .5f, .5f, .5f),
         new Vector3(-.5f, .5f, .5f),
        });
        internal static Vector3 trianglePosAdj{get;}=new Vector3((Width/2.0f)-0.5f,(Height/2.0f)-0.5f,(Depth/2.0f)-0.5f);
        internal static ConcurrentQueue<List<Vector2>>vertexUVListPool=new ConcurrentQueue<List<Vector2>>();
        internal static void MarchingCubes(Voxel[]polygonCell,Vector3Int vCoord1,Vector3[]vertices,Vector3[][][]verticesCache,MaterialId[]materials,Vector3[]normals,double[]density,Vector3[]vertex,MaterialId[]material,float[]distance,int[]idx,Vector3[]verPos,ref UInt32 vertexCount,NativeList<Vertex>TempVer,NativeList<UInt32>TempTri,Dictionary<Vector3,List<Vector2>>vertexUV){
         int edgeIndex;
         /*
             Determine the index into the edge table which
             tells us which vertices are inside of the surface
         */
                                             edgeIndex =  0;
         if(-polygonCell[0].Density<IsoLevel)edgeIndex|=  1;
         if(-polygonCell[1].Density<IsoLevel)edgeIndex|=  2;
         if(-polygonCell[2].Density<IsoLevel)edgeIndex|=  4;
         if(-polygonCell[3].Density<IsoLevel)edgeIndex|=  8;
         if(-polygonCell[4].Density<IsoLevel)edgeIndex|= 16;
         if(-polygonCell[5].Density<IsoLevel)edgeIndex|= 32;
         if(-polygonCell[6].Density<IsoLevel)edgeIndex|= 64;
         if(-polygonCell[7].Density<IsoLevel)edgeIndex|=128;
         if(Tables.EdgeTable[edgeIndex]!=0){/*  Cube is not entirely in/out of the surface  */
          //  Use cached data if available
          Array.Clear(vertices,0,vertices.Length);
          vertices[ 0]=(vCoord1.z>0?verticesCache[0][0][0]:(vCoord1.y>0?verticesCache[2][vCoord1.z+vCoord1.x*Depth][0]:Vector3.zero));
          vertices[ 1]=(vCoord1.z>0?verticesCache[0][0][1]:Vector3.zero);
          vertices[ 2]=(vCoord1.z>0?verticesCache[0][0][2]:Vector3.zero);
          vertices[ 3]=(vCoord1.z>0?verticesCache[0][0][3]:(vCoord1.x>0?verticesCache[1][vCoord1.z][0]:Vector3.zero));
          vertices[ 4]=(vCoord1.y>0?verticesCache[2][vCoord1.z+vCoord1.x*Depth][1]:Vector3.zero);
          vertices[ 7]=(vCoord1.x>0?verticesCache[1][vCoord1.z][1]:Vector3.zero);
          vertices[ 8]=(vCoord1.x>0?verticesCache[1][vCoord1.z][2]:(vCoord1.y>0?verticesCache[2][vCoord1.z+vCoord1.x*Depth][3]:Vector3.zero));
          vertices[ 9]=(vCoord1.y>0?verticesCache[2][vCoord1.z+vCoord1.x*Depth][2]:Vector3.zero);
          vertices[11]=(vCoord1.x>0?verticesCache[1][vCoord1.z][3]:Vector3.zero);                                
          if(0!=(Tables.EdgeTable[edgeIndex]&   1)){vertexInterp(0,1,ref vertices[ 0],ref normals[ 0],ref materials[ 0]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   2)){vertexInterp(1,2,ref vertices[ 1],ref normals[ 1],ref materials[ 1]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   4)){vertexInterp(2,3,ref vertices[ 2],ref normals[ 2],ref materials[ 2]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   8)){vertexInterp(3,0,ref vertices[ 3],ref normals[ 3],ref materials[ 3]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  16)){vertexInterp(4,5,ref vertices[ 4],ref normals[ 4],ref materials[ 4]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  32)){vertexInterp(5,6,ref vertices[ 5],ref normals[ 5],ref materials[ 5]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  64)){vertexInterp(6,7,ref vertices[ 6],ref normals[ 6],ref materials[ 6]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 128)){vertexInterp(7,4,ref vertices[ 7],ref normals[ 7],ref materials[ 7]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 256)){vertexInterp(0,4,ref vertices[ 8],ref normals[ 8],ref materials[ 8]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 512)){vertexInterp(1,5,ref vertices[ 9],ref normals[ 9],ref materials[ 9]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&1024)){vertexInterp(2,6,ref vertices[10],ref normals[10],ref materials[10]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&2048)){vertexInterp(3,7,ref vertices[11],ref normals[11],ref materials[11]);}
          void vertexInterp(int c0,int c1,ref Vector3 p,ref Vector3 n,ref MaterialId m){
           density[0]=-polygonCell[c0].Density;vertex[0]=corners[c0];material[0]=polygonCell[c0].Material;
           density[1]=-polygonCell[c1].Density;vertex[1]=corners[c1];material[1]=polygonCell[c1].Material;
           //  p
           if(p!=Vector3.zero){goto _Normal;}
           if(Math.Abs(IsoLevel-density[0])<double.Epsilon){p=vertex[0];goto _Normal;}
           if(Math.Abs(IsoLevel-density[1])<double.Epsilon){p=vertex[1];goto _Normal;}
           if(Math.Abs(density[0]-density[1])<double.Epsilon){p=vertex[0];goto _Normal;}
           double marchingUnit=(IsoLevel-density[0])/(density[1]-density[0]);
           p.x=(float)(vertex[0].x+marchingUnit*(vertex[1].x-vertex[0].x));
           p.y=(float)(vertex[0].y+marchingUnit*(vertex[1].y-vertex[0].y));
           p.z=(float)(vertex[0].z+marchingUnit*(vertex[1].z-vertex[0].z));
           //  n
           _Normal:{
            distance[0]=Vector3.Distance(vertex[0],vertex[1]);
            distance[1]=Vector3.Distance(vertex[1],p);
            n=Vector3.Lerp(
             polygonCell[c1].Normal,
             polygonCell[c0].Normal,
             distance[1]/distance[0]
            );
            n=n!=Vector3.zero?n.normalized:Vector3.down;
           }
           //  m
           m=material[0];
           if(density[1]<density[0]){
            m=material[1];
           }else if(density[1]==density[0]&&(int)material[1]>(int)material[0]){
            m=material[1];
           }
          }
          //  Cache the data
          verticesCache[0][0][0]=vertices[ 4]+Vector3.back;//  Adiciona um valor "negativo" porque o voxelCoord próximo vai usar esse valor mas precisa obter "uma posição anterior"
          verticesCache[0][0][1]=vertices[ 5]+Vector3.back;
          verticesCache[0][0][2]=vertices[ 6]+Vector3.back;
          verticesCache[0][0][3]=vertices[ 7]+Vector3.back;
          verticesCache[1][vCoord1.z][0]=vertices[ 1]+Vector3.left;
          verticesCache[1][vCoord1.z][1]=vertices[ 5]+Vector3.left;
          verticesCache[1][vCoord1.z][2]=vertices[ 9]+Vector3.left;
          verticesCache[1][vCoord1.z][3]=vertices[10]+Vector3.left;
          verticesCache[2][vCoord1.z+vCoord1.x*Depth][0]=vertices[ 2]+Vector3.down;
          verticesCache[2][vCoord1.z+vCoord1.x*Depth][1]=vertices[ 6]+Vector3.down;
          verticesCache[2][vCoord1.z+vCoord1.x*Depth][2]=vertices[10]+Vector3.down;
          verticesCache[2][vCoord1.z+vCoord1.x*Depth][3]=vertices[11]+Vector3.down;
          /*  Create the triangle  */
          for(int i=0;Tables.TriangleTable[edgeIndex][i]!=-1;i+=3){
           idx[0]=Tables.TriangleTable[edgeIndex][i  ];
           idx[1]=Tables.TriangleTable[edgeIndex][i+1];
           idx[2]=Tables.TriangleTable[edgeIndex][i+2];
           Vector3 pos=vCoord1-trianglePosAdj;
           Vector2 materialUV=AtlasHelper.uv[Mathf.Max((int)materials[idx[0]],
                                                       (int)materials[idx[1]],
                                                       (int)materials[idx[2]]
           )];
           TempVer.Add(new Vertex(verPos[0]=pos+vertices[idx[0]],normals[idx[0]],materialUV));
           TempVer.Add(new Vertex(verPos[1]=pos+vertices[idx[1]],normals[idx[1]],materialUV));
           TempVer.Add(new Vertex(verPos[2]=pos+vertices[idx[2]],normals[idx[2]],materialUV));
           TempTri.Add(vertexCount+2u);
           TempTri.Add(vertexCount+1u);
           TempTri.Add(vertexCount  );
                       vertexCount+=3u;
           if(!vertexUV.ContainsKey(verPos[0])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[0],list);}else{vertexUV.Add(verPos[0],new List<Vector2>());}}vertexUV[verPos[0]].Add(materialUV);
           if(!vertexUV.ContainsKey(verPos[1])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[1],list);}else{vertexUV.Add(verPos[1],new List<Vector2>());}}vertexUV[verPos[1]].Add(materialUV);
           if(!vertexUV.ContainsKey(verPos[2])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[2],list);}else{vertexUV.Add(verPos[2],new List<Vector2>());}}vertexUV[verPos[2]].Add(materialUV);
          }
         }
        }
        internal static void MarchingCubes2(Voxel[]polygonCell,Vector3Int vCoord1,Vector3[]vertices,MaterialId[]materials,double[]density,Vector3[]vertex,MaterialId[]material,int[]idx,Vector3[]verPos,Vector2Int posOffset,Dictionary<Vector3,List<Vector2>>vertexUV){ 
         int edgeIndex;
         /*
             Determine the index into the edge table which
             tells us which vertices are inside of the surface
         */
                                             edgeIndex =  0;
         if(-polygonCell[0].Density<IsoLevel)edgeIndex|=  1;
         if(-polygonCell[1].Density<IsoLevel)edgeIndex|=  2;
         if(-polygonCell[2].Density<IsoLevel)edgeIndex|=  4;
         if(-polygonCell[3].Density<IsoLevel)edgeIndex|=  8;
         if(-polygonCell[4].Density<IsoLevel)edgeIndex|= 16;
         if(-polygonCell[5].Density<IsoLevel)edgeIndex|= 32;
         if(-polygonCell[6].Density<IsoLevel)edgeIndex|= 64;
         if(-polygonCell[7].Density<IsoLevel)edgeIndex|=128;
         if(Tables.EdgeTable[edgeIndex]!=0){
          if(0!=(Tables.EdgeTable[edgeIndex]&   1)){vertexInterp(0,1,ref vertices[ 0],ref materials[ 0]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   2)){vertexInterp(1,2,ref vertices[ 1],ref materials[ 1]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   4)){vertexInterp(2,3,ref vertices[ 2],ref materials[ 2]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&   8)){vertexInterp(3,0,ref vertices[ 3],ref materials[ 3]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  16)){vertexInterp(4,5,ref vertices[ 4],ref materials[ 4]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  32)){vertexInterp(5,6,ref vertices[ 5],ref materials[ 5]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&  64)){vertexInterp(6,7,ref vertices[ 6],ref materials[ 6]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 128)){vertexInterp(7,4,ref vertices[ 7],ref materials[ 7]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 256)){vertexInterp(0,4,ref vertices[ 8],ref materials[ 8]);}
          if(0!=(Tables.EdgeTable[edgeIndex]& 512)){vertexInterp(1,5,ref vertices[ 9],ref materials[ 9]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&1024)){vertexInterp(2,6,ref vertices[10],ref materials[10]);}
          if(0!=(Tables.EdgeTable[edgeIndex]&2048)){vertexInterp(3,7,ref vertices[11],ref materials[11]);}
          void vertexInterp(int c0,int c1,ref Vector3 p,ref MaterialId m){
           density[0]=-polygonCell[c0].Density;vertex[0]=corners[c0];material[0]=polygonCell[c0].Material;
           density[1]=-polygonCell[c1].Density;vertex[1]=corners[c1];material[1]=polygonCell[c1].Material;
           //  p
           if(Math.Abs(IsoLevel-density[0])<double.Epsilon){p=vertex[0];goto _Material;}
           if(Math.Abs(IsoLevel-density[1])<double.Epsilon){p=vertex[1];goto _Material;}
           if(Math.Abs(density[0]-density[1])<double.Epsilon){p=vertex[0];goto _Material;}
           double marchingUnit=(IsoLevel-density[0])/(density[1]-density[0]);
           p.x=(float)(vertex[0].x+marchingUnit*(vertex[1].x-vertex[0].x));
           p.y=(float)(vertex[0].y+marchingUnit*(vertex[1].y-vertex[0].y));
           p.z=(float)(vertex[0].z+marchingUnit*(vertex[1].z-vertex[0].z));
           _Material:{
            m=material[0];
            if(density[1]<density[0]){
             m=material[1];
            }else if(density[1]==density[0]&&(int)material[1]>(int)material[0]){
             m=material[1];
            }
           }
          }
          /*  Create the triangle  */
          for(int i=0;Tables.TriangleTable[edgeIndex][i]!=-1;i+=3){
           idx[0]=Tables.TriangleTable[edgeIndex][i  ];
           idx[1]=Tables.TriangleTable[edgeIndex][i+1];
           idx[2]=Tables.TriangleTable[edgeIndex][i+2];
           Vector3 pos=vCoord1-trianglePosAdj;pos.x+=posOffset.x;
                                              pos.z+=posOffset.y;
           Vector2 materialUV=AtlasHelper.uv[Mathf.Max((int)materials[idx[0]],
                                                       (int)materials[idx[1]],
                                                       (int)materials[idx[2]]
           )];
           verPos[0]=pos+vertices[idx[0]];
           verPos[1]=pos+vertices[idx[1]];
           verPos[2]=pos+vertices[idx[2]];
           if(!vertexUV.ContainsKey(verPos[0])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[0],list);}else{vertexUV.Add(verPos[0],new List<Vector2>());}}vertexUV[verPos[0]].Add(materialUV);
           if(!vertexUV.ContainsKey(verPos[1])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[1],list);}else{vertexUV.Add(verPos[1],new List<Vector2>());}}vertexUV[verPos[1]].Add(materialUV);
           if(!vertexUV.ContainsKey(verPos[2])){if(vertexUVListPool.TryDequeue(out List<Vector2>list)){vertexUV.Add(verPos[2],list);}else{vertexUV.Add(verPos[2],new List<Vector2>());}}vertexUV[verPos[2]].Add(materialUV);
          }
         }
        }
        #endregion 
        #region Water
        internal struct WaterVoxel{
         internal double Density;
         internal bool   Sleeping;
         internal double Absorbing;
        }
        #endregion 
        [SerializeField]internal int marchingCubesExecutionCountLimit=7;
        internal static readonly Biome biome=new Biome();
        internal VoxelTerrain[]terrain;
        internal readonly VoxelTerrain.MarchingCubesMultithreaded[]marchingCubesBGThreads=new VoxelTerrain.MarchingCubesMultithreaded[Environment.ProcessorCount];
        internal readonly VoxelTerrain.AddSimObjectsMultithreaded[]addSimObjectsBGThreads=new VoxelTerrain.AddSimObjectsMultithreaded[Environment.ProcessorCount];
        internal static string addedSimObjectsFile;
        internal static string editsFile;
        #region Awake
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         Core.Singleton.OnDestroyingCoreEvent+=OnDestroyingCoreEvent;
         editsFile=string.Format("{0}{1}",Core.savePath,"edits.txt");
         new FileStream(VoxelSystem.editsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite).Dispose();
         addedSimObjectsFile=string.Format("{0}{1}",Core.savePath,"addedSimObjectsAt.txt");
         AtlasHelper.GetAtlasData(PrefabVoxelTerrain.GetComponent<MeshRenderer>().sharedMaterial);
         biome.Seed=0;
         VoxelTerrain.marchingCubesExecutionCount=0;
         VoxelTerrain.MarchingCubesMultithreaded.Stop=false;for(int i=0;i<marchingCubesBGThreads.Length;++i){marchingCubesBGThreads[i]=new VoxelTerrain.MarchingCubesMultithreaded();}
         VoxelTerrain.AddSimObjectsMultithreaded.Stop=false;for(int i=0;i<addSimObjectsBGThreads.Length;++i){addSimObjectsBGThreads[i]=new VoxelTerrain.AddSimObjectsMultithreaded();}
         TerrainEditingMultithreaded.Stop=false;terrainEditingBGThread=new TerrainEditingMultithreaded();
         StartCoroutine(ProceduralGenerationFollowUpCoroutine());
        }
        #endregion
        void OnDestroyingCoreEvent(object sender,EventArgs e){
         if(terrain!=null){
          for(int i=0;i<terrain.Length;++i){
           terrain[i].OnExit();
          }
         }
         if(VoxelTerrain.MarchingCubesMultithreaded.Clear()!=0){
          //Logger.Error("terrain MarchingCubes tasks will stop with pending work");
         }
         VoxelTerrain.MarchingCubesMultithreaded.Stop=true;for(int i=0;i<marchingCubesBGThreads.Length;++i){marchingCubesBGThreads[i].Wait();
          marchingCubesBGThreads[i].editsFileStream      .Dispose();
          marchingCubesBGThreads[i].editsFileStreamReader.Dispose();
         }
         if(VoxelTerrain.AddSimObjectsMultithreaded.Clear()!=0){
          //Logger.Error("terrain AddSimObjects tasks will stop with pending work");
         }
         VoxelTerrain.AddSimObjectsMultithreaded.Stop=true;for(int i=0;i<addSimObjectsBGThreads.Length;++i){addSimObjectsBGThreads[i].Wait();
          addSimObjectsBGThreads[i].addedSimObjectsFileStreamWriter.Dispose();
          addSimObjectsBGThreads[i].addedSimObjectsFileStreamReader.Dispose();
         }
         terrainEditingBG.IsCompleted(terrainEditingBGThread.IsRunning,-1);
         if(TerrainEditingMultithreaded.Clear()!=0){
          //Logger.Error("TerrainEditing task will stop with pending work");
         }
         TerrainEditingMultithreaded.Stop=true;terrainEditingBGThread.Wait();
         terrainEditingBGThread.editsFileStreamWriter.Dispose();
         terrainEditingBGThread.editsFileStreamReader.Dispose();
         biome.DisposeModules();
         waterActive.Clear();
         if(Singleton==this){Singleton=null;}
        }
        internal readonly SortedDictionary<int,NavMeshBuildSource>navMeshSources=new SortedDictionary<int,NavMeshBuildSource>();
        internal readonly SortedDictionary<int,NavMeshBuildMarkup>navMeshMarkups=new SortedDictionary<int,NavMeshBuildMarkup>();
         readonly List<NavMeshBuildSource>sources=new List<NavMeshBuildSource>();
         readonly List<NavMeshBuildMarkup>markups=new List<NavMeshBuildMarkup>();
        internal bool navMeshSourcesCollectionChanged;
        internal bool CollectNavMeshSources(out List<NavMeshBuildSource>sourcesCollected){
         sourcesCollected=sources;
         if(navMeshSourcesCollectionChanged){
            navMeshSourcesCollectionChanged=false;
          Logger.Debug("CollectNavMeshSources");
          sources.Clear();
          markups.Clear();
          sources.AddRange(navMeshSources.Values);
          markups.AddRange(navMeshMarkups.Values);
          NavMeshBuilder.CollectSources(null,PhysHelper.NavMesh,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
         }
         return true;
        }
        void EditTerrain(Vector3 at,TerrainEditingBackgroundContainer.EditMode mode,Vector3Int size,double density,MaterialId material,int smoothness){
         terrainEditingRequests.Enqueue(
          new TerrainEditRequest{
           center=at,
           mode=mode,
           size=size,
           density=density,
           material=material,
           smoothness=smoothness,
          }
         );
        }
        [SerializeField]bool    DEBUG_ADD_WATER_SOURCE;
        [SerializeField]Vector3 DEBUG_ADD_WATER_SOURCE_AT=new Vector3(0,40,0);
        [SerializeField]bool                                       DEBUG_EDIT=false;
        [SerializeField]Vector3                                    DEBUG_EDIT_AT=new Vector3Int(0,40,0);
        [SerializeField]TerrainEditingBackgroundContainer.EditMode DEBUG_EDIT_MODE=TerrainEditingBackgroundContainer.EditMode.Cube;
        [SerializeField]Vector3Int                                 DEBUG_EDIT_SIZE=new Vector3Int(3,3,3);
        [SerializeField]double                                     DEBUG_EDIT_DENSITY=100.0;
        [SerializeField]MaterialId                                 DEBUG_EDIT_MATERIAL_ID=MaterialId.Dirt;
        [SerializeField]int                                        DEBUG_EDIT_SMOOTHNESS=5;
        [SerializeField]VoxelTerrain PrefabVoxelTerrain;
        int maxConnections=1;
        internal readonly LinkedList<VoxelTerrain>terrainPool=new LinkedList<VoxelTerrain>();
         internal readonly Dictionary<int,VoxelTerrain>terrainActive=new Dictionary<int,VoxelTerrain>();
          internal static readonly ConcurrentDictionary<int,VoxelWater>waterActive=new ConcurrentDictionary<int,VoxelWater>();
        bool terrainEditingRequested;
        void Update(){
         if(terrain==null){
          terrainSynchronization.Clear();
          int poolSize=maxConnections*(expropriationDistance.x*2+1)
                                     *(expropriationDistance.y*2+1);
          Logger.Debug("terrain poolSize required:"+poolSize);
          terrain=new VoxelTerrain[poolSize];
          for(int i=0;i<terrain.Length;++i){
           VoxelTerrain cnk;
           terrain[i]=cnk=Instantiate(PrefabVoxelTerrain);
           terrainSynchronization.Add(cnk,cnk.synchronizer);
           terrain[i].OnInstantiated();
           cnk.expropriated=terrainPool.AddLast(cnk);
          }
         }
         foreach(var kvp in terrainActive){VoxelTerrain cnk=kvp.Value;
          cnk.ManualUpdate();
         }
         proceduralGenerationCoroutineBeginFlag=generationStarters.Count>0;
         if(DEBUG_EDIT){
            DEBUG_EDIT=false;
          //Logger.Debug("DEBUG_EDIT_AT:"+DEBUG_EDIT_AT);
          EditTerrain(
           DEBUG_EDIT_AT,
           DEBUG_EDIT_MODE,
           DEBUG_EDIT_SIZE,
           DEBUG_EDIT_DENSITY,
           DEBUG_EDIT_MATERIAL_ID,
           DEBUG_EDIT_SMOOTHNESS
          );
         }
         if(terrainEditingRequested&&OnTerrainEditingRequestsApplied()){
            terrainEditingRequested=false;
         }else if(!terrainEditingRequested){
          if(terrainEditingRequests.Count>0&&OnTerrainEditingRequestsPush()){
           OnTerrainEditingRequestsPushed();
          }
         }
        }
        bool OnTerrainEditingRequestsPush(){
         if(terrainEditingBG.IsCompleted(terrainEditingBGThread.IsRunning)){
          while(terrainEditingRequests.Count>0){terrainEditingBG.requests.Enqueue(terrainEditingRequests.Dequeue());}
          TerrainEditingMultithreaded.Schedule(terrainEditingBG);
          return true;
         }
         return false;
        }
        void OnTerrainEditingRequestsPushed(){
         terrainEditingRequested=true;
        }
        bool OnTerrainEditingRequestsApplied(){
         if(terrainEditingBG.IsCompleted(terrainEditingBGThread.IsRunning)){
          foreach(int cnkIdx in terrainEditingBG.dirty){
           if(terrainActive.TryGetValue(cnkIdx,out VoxelTerrain cnk)){
            cnk.OnEdited();
           }
          }
          return true;
         }
         return false;
        }
        internal readonly HashSet<Gameplayer>generationStarters=new HashSet<Gameplayer>();
         readonly HashSet<Vector2Int>coordinates=new HashSet<Vector2Int>();
          readonly HashSet<Vector2Int>pastCoordinates=new HashSet<Vector2Int>();
        internal bool proceduralGenerationCoroutineBeginFlag;
         WaitUntil waitForProceduralGenerationFollowUpBeginFlag;
        IEnumerator ProceduralGenerationFollowUpCoroutine(){
         waitForProceduralGenerationFollowUpBeginFlag=new WaitUntil(()=>proceduralGenerationCoroutineBeginFlag);
         Loop:{
          yield return waitForProceduralGenerationFollowUpBeginFlag;
           proceduralGenerationCoroutineBeginFlag=false;
          coordinates.Clear();
          foreach(Gameplayer gameplayer in generationStarters){
           coordinates.Add(gameplayer.cCoord);
          }
          generationStarters.Clear();
          foreach(Vector2Int pastCoord in pastCoordinates){
           #region expropriation
           for(Vector2Int eCoord=new Vector2Int(),cCoord1=new Vector2Int();eCoord.y<=expropriationDistance.y;eCoord.y++){for(cCoord1.y=-eCoord.y+pastCoord.y;cCoord1.y<=eCoord.y+pastCoord.y;cCoord1.y+=eCoord.y*2){
           for(           eCoord.x=0                                      ;eCoord.x<=expropriationDistance.x;eCoord.x++){for(cCoord1.x=-eCoord.x+pastCoord.x;cCoord1.x<=eCoord.x+pastCoord.x;cCoord1.x+=eCoord.x*2){
            if(Math.Abs(cCoord1.x)>=MaxcCoordx||
               Math.Abs(cCoord1.y)>=MaxcCoordy){
             goto _skip;
            }
            //Logger.Debug("expropriation at:"+cCoord1);
            if(coordinates.All(
             currCoord=>{
              return Mathf.Abs(cCoord1.x-currCoord.x)>instantiationDistance.x||
                     Mathf.Abs(cCoord1.y-currCoord.y)>instantiationDistance.y;
             })
            ){
             int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);
             if(terrainActive.TryGetValue(cnkIdx1,out VoxelTerrain cnk)){
              if(cnk.expropriated==null){
               cnk.expropriated=terrainPool.AddLast(cnk);
              }
             }
            }
            _skip:{}
            if(eCoord.x==0){break;}
           }}
            if(eCoord.y==0){break;}
           }}
           #endregion
          }
          pastCoordinates.Clear();
          foreach(Vector2Int currCoord in coordinates){
           pastCoordinates.Add(currCoord);
           #region instantiation
           for(Vector2Int iCoord=new Vector2Int(),cCoord1=new Vector2Int();iCoord.y<=instantiationDistance.y;iCoord.y++){for(cCoord1.y=-iCoord.y+currCoord.y;cCoord1.y<=iCoord.y+currCoord.y;cCoord1.y+=iCoord.y*2){
           for(           iCoord.x=0                                      ;iCoord.x<=instantiationDistance.x;iCoord.x++){for(cCoord1.x=-iCoord.x+currCoord.x;cCoord1.x<=iCoord.x+currCoord.x;cCoord1.x+=iCoord.x*2){
            if(Math.Abs(cCoord1.x)>=MaxcCoordx||
               Math.Abs(cCoord1.y)>=MaxcCoordy){
             goto _skip;
            }
            int cnkIdx1=GetcnkIdx(cCoord1.x,cCoord1.y);
            if(!terrainActive.TryGetValue(cnkIdx1,out VoxelTerrain cnk)){
             cnk=terrainPool.First.Value;
             terrainPool.RemoveFirst();
             cnk.expropriated=null;
             bool firstCall=cnk.cnkIdx==null;
             if(cnk.cnkIdx!=null&&terrainActive.ContainsKey(cnk.cnkIdx.Value)){
              terrainActive.Remove(cnk.cnkIdx.Value);
              waterActive.TryRemove(cnk.cnkIdx.Value,out _);
             }
             terrainActive.Add(cnkIdx1,cnk);
             cnk.cnkIdx=cnkIdx1;
             cnk.OncCoordChanged(cCoord1,firstCall);
             waterActive[cnkIdx1]=cnk.water;
            }else{
             if(cnk.expropriated!=null){
              terrainPool.Remove(cnk.expropriated);
              cnk.expropriated=null;
             }
            }
            _skip:{}
            if(iCoord.x==0){break;}
           }}
            if(iCoord.y==0){break;}
           }}
           #endregion
          }
         }
         goto Loop;
        }
        internal static readonly Dictionary<VoxelTerrain,object>terrainSynchronization=new Dictionary<VoxelTerrain,object>();
        static int GetcnkIdxFromFileLine(string line,out int cnkIdx){
         int cnkIdxStringStart=line.IndexOf("cnkIdx=")+7;
         int cnkIdxStringEnd=line.IndexOf(" ,",cnkIdxStringStart);
         int cnkIdxStringLength=cnkIdxStringEnd-cnkIdxStringStart;
         cnkIdx=int.Parse(line.Substring(cnkIdxStringStart,cnkIdxStringLength));
         return cnkIdxStringEnd;
        }
        static void GetNextTerrainEditDataFromFileLine(string line,ref int vCoordStringStart,out Vector3Int vCoord,out double density,out MaterialId materialId){
         vCoordStringStart+=8;
         int vCoordStringEnd=line.IndexOf(")",vCoordStringStart);
         string vCoordString=line.Substring(vCoordStringStart,vCoordStringEnd-vCoordStringStart);
         int xStringStart=0;
         int xStringEnd=vCoordString.IndexOf(", ",xStringStart);
         int x=int.Parse(vCoordString.Substring(xStringStart,xStringEnd-xStringStart));
         int yStringStart=xStringEnd+2;
         int yStringEnd=vCoordString.IndexOf(", ",yStringStart);
         int y=int.Parse(vCoordString.Substring(yStringStart,yStringEnd-yStringStart));
         int zStringStart=yStringEnd+2;
         int z=int.Parse(vCoordString.Substring(zStringStart));
         vCoord=new Vector3Int(x,y,z);
         //Logger.Debug("WriteFile merging vCoord:"+vCoord);
         int densityStringStart=vCoordStringEnd;
         densityStringStart=line.IndexOf("density=",densityStringStart);
         densityStringStart+=8;
         int densityStringEnd=line.IndexOf(", ",densityStringStart);
         density=double.Parse(line.Substring(densityStringStart,densityStringEnd-densityStringStart));
         //Logger.Debug("WriteFile merging density:"+density);
         int materialIdStringStart=densityStringEnd+13;
         int materialIdStringEnd=line.IndexOf(")",materialIdStringStart);
         materialId=(MaterialId)Enum.Parse(typeof(MaterialId),line.Substring(materialIdStringStart,materialIdStringEnd-materialIdStringStart));
         //Logger.Debug("WriteFile merging materialId:"+materialId);
        }
        internal static void ReadFile(FileStream fileStream,StreamReader fileStreamReader,Dictionary<int,Dictionary<Vector3Int,(double density,MaterialId materialId)>>data,List<int>cnkIdxValuesToRead){
         fileStream.Position=0L;
         fileStreamReader.DiscardBufferedData();
         string line;
         while((line=fileStreamReader.ReadLine())!=null){
          if(string.IsNullOrEmpty(line)){continue;}
          int cnkIdxStringEnd=GetcnkIdxFromFileLine(line,out int cnkIdx);
          if(cnkIdxValuesToRead.Contains(cnkIdx)){
           int vCoordStringStart=cnkIdxStringEnd+2;
           while((vCoordStringStart=line.IndexOf("vCoord=",vCoordStringStart))>=0){
            GetNextTerrainEditDataFromFileLine(line,ref vCoordStringStart,out Vector3Int vCoord,out double density,out MaterialId materialId);
            if(!data.ContainsKey(cnkIdx)){
             if(TerrainEditingMultithreaded.chunkDataPool.TryDequeue(out Dictionary<Vector3Int,(double,MaterialId)>chunkData)){
              data.Add(cnkIdx,chunkData);
             }else{
              data.Add(cnkIdx,new Dictionary<Vector3Int,(double,MaterialId)>());
             }
            }
            data[cnkIdx][vCoord]=(density,materialId);
           }
          }
         }
        }
        static void WriteFile(FileStream fileStream,StreamWriter fileStreamWriter,StreamReader fileStreamReader,StringBuilder stringBuilder,Dictionary<int,Dictionary<Vector3Int,(double density,MaterialId materialId)>>data){         
         stringBuilder.Clear();
         fileStream.Position=0L;
         fileStreamReader.DiscardBufferedData();
         string line;
         while((line=fileStreamReader.ReadLine())!=null){
          if(string.IsNullOrEmpty(line)){continue;}
          int cnkIdxStringEnd=GetcnkIdxFromFileLine(line,out int cnkIdx);
          if(data.ContainsKey(cnkIdx)){
           //Logger.Debug("WriteFile merge edits for cnkIdx:"+cnkIdx);
           int vCoordStringStart=cnkIdxStringEnd+2;
           while((vCoordStringStart=line.IndexOf("vCoord=",vCoordStringStart))>=0){
            GetNextTerrainEditDataFromFileLine(line,ref vCoordStringStart,out Vector3Int vCoord,out double density,out MaterialId materialId);
            if(!data[cnkIdx].ContainsKey(vCoord)){
             data[cnkIdx].Add(vCoord,(density,materialId));
            }
           }
          }else{
           stringBuilder.AppendLine(line);
          }
         }
         foreach(var cnkIdxEditsPair in data){
          stringBuilder.AppendFormat("{{ cnkIdx={0} , {{ ",cnkIdxEditsPair.Key);
          foreach(var vCoordEditPair in cnkIdxEditsPair.Value){
           stringBuilder.AppendFormat("{{ vCoord={0} , (density={1}, materialId={2}) }}, ",vCoordEditPair.Key,vCoordEditPair.Value.density,vCoordEditPair.Value.materialId);
          }
          stringBuilder.AppendFormat("}} }}, {0}",Environment.NewLine);
         }
         fileStream.SetLength(0L);
         fileStreamWriter.Write(stringBuilder.ToString());
         fileStreamWriter.Flush();
        }
        internal struct TerrainEditRequest{
         internal Vector3                                    center;
         internal TerrainEditingBackgroundContainer.EditMode mode;
         internal Vector3Int                                 size;
         internal double                                     density;
         internal MaterialId                                 material;
         internal int                                        smoothness;
        }
        readonly Queue<TerrainEditRequest>terrainEditingRequests=new Queue<TerrainEditRequest>();
        internal readonly TerrainEditingBackgroundContainer terrainEditingBG=new TerrainEditingBackgroundContainer();
        internal class TerrainEditingBackgroundContainer:BackgroundContainer{
         internal enum EditMode{Cube,}
         internal readonly Queue<TerrainEditRequest>requests=new Queue<TerrainEditRequest>();
         internal readonly HashSet<int>dirty=new HashSet<int>();
        }
        internal TerrainEditingMultithreaded terrainEditingBGThread;
        internal class TerrainEditingMultithreaded:BaseMultithreaded<TerrainEditingBackgroundContainer>{
         internal readonly FileStream editsFileStream;
          internal readonly StreamWriter editsFileStreamWriter;
          internal readonly StreamReader editsFileStreamReader;
           internal readonly StringBuilder editsStringBuilder=new StringBuilder();
         internal static readonly ConcurrentQueue<Dictionary<Vector3Int,(double,MaterialId)>>chunkDataPool=new ConcurrentQueue<Dictionary<Vector3Int,(double,MaterialId)>>();
         readonly Dictionary<int,Dictionary<Vector3Int,(double density,MaterialId materialId)>>readData=new Dictionary<int,Dictionary<Vector3Int,(double,MaterialId)>>();
         readonly Dictionary<int,Dictionary<Vector3Int,(double density,MaterialId materialId)>>saveData=new Dictionary<int,Dictionary<Vector3Int,(double,MaterialId)>>();
         readonly List<int>cnkIdxValuesToRead=new List<int>();
         internal TerrainEditingMultithreaded(){
          editsFileStream=new FileStream(VoxelSystem.editsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
           editsFileStreamWriter=new StreamWriter(editsFileStream);
           editsFileStreamReader=new StreamReader(editsFileStream);
         }
         protected override void Cleanup(){
          foreach(var chunkData in readData){chunkData.Value.Clear();chunkDataPool.Enqueue(chunkData.Value);}
          readData.Clear();
          foreach(var chunkData in saveData){chunkData.Value.Clear();chunkDataPool.Enqueue(chunkData.Value);}
          saveData.Clear();
         }
         protected override void Execute(){
          //Logger.Debug("TerrainEditingMultithreaded Execute");
          lock(VoxelSystem.terrainSynchronization){
           //Logger.Debug("container.requests.Count:"+container.requests.Count);
           container.dirty.Clear();
           while(container.requests.Count>0){var editRequest=container.requests.Dequeue();
            Vector3    center    =editRequest.center;
            Vector3Int size      =editRequest.size;
            double     density   =editRequest.density;
            MaterialId material  =editRequest.material;
            int        smoothness=editRequest.smoothness;
            switch(editRequest.mode){
             default:{
              float sqrt_yx_1=Mathf.Sqrt(Mathf.Pow(size.y,2)+Mathf.Pow(size.x,2));
              float sqrt_xz_1=Mathf.Sqrt(Mathf.Pow(size.x,2)+Mathf.Pow(size.z,2));
              float sqrt_zy_1=Mathf.Sqrt(Mathf.Pow(size.z,2)+Mathf.Pow(size.y,2));
               float sqrt_yx_xz_1=Mathf.Sqrt(Mathf.Pow(sqrt_yx_1,2)+Mathf.Pow(sqrt_xz_1,2));
                float sqrt_yx_xz_zy_1=Mathf.Sqrt(Mathf.Pow(sqrt_yx_xz_1,2)+Mathf.Pow(sqrt_zy_1,2));
              float sqrt_yx_2;
              float sqrt_xz_2;
              float sqrt_zy_2;
              Vector2Int cCoord1=vecPosTocCoord(center ),        cCoord3;
              Vector2Int cnkRgn1=cCoordTocnkRgn(cCoord1),        cnkRgn3;
              Vector3Int vCoord1=vecPosTovCoord(center ),vCoord2,vCoord3;
              for(int y=0;y<size.y+smoothness;++y){for(vCoord2=new Vector3Int(vCoord1.x,vCoord1.y-y,vCoord1.z);vCoord2.y<=vCoord1.y+y;vCoord2.y+=y*2){
               if(vCoord2.y>=0&&vCoord2.y<Height){
              for(int x=0;x<size.x+smoothness;++x){for(vCoord2.x=vCoord1.x-x                                  ;vCoord2.x<=vCoord1.x+x;vCoord2.x+=x*2){
                sqrt_yx_2=Mathf.Sqrt(Mathf.Pow(y,2)+Mathf.Pow(x,2));
              for(int z=0;z<size.z+smoothness;++z){for(vCoord2.z=vCoord1.z-z                                  ;vCoord2.z<=vCoord1.z+z;vCoord2.z+=z*2){
                cCoord3=cCoord1;
                cnkRgn3=cnkRgn1;
                vCoord3=vCoord2;
                if(vCoord2.x<0||vCoord2.x>=Width||
                   vCoord2.z<0||vCoord2.z>=Depth
                ){
                 ValidateCoord(ref cnkRgn3,ref vCoord3);
                 cCoord3=cnkRgnTocCoord(cnkRgn3);
                }
                int cnkIdx3=GetcnkIdx(cCoord3.x,cCoord3.y);
                sqrt_xz_2=Mathf.Sqrt(Mathf.Pow(x,2)+Mathf.Pow(z,2));
                sqrt_zy_2=Mathf.Sqrt(Mathf.Pow(z,2)+Mathf.Pow(y,2));
                double resultDensity;
                if(y>=size.y||x>=size.x||z>=size.z){
                 if(y>=size.y&&x>=size.x&&z>=size.z){
                  float sqrt_yx_xz_2=Mathf.Sqrt(Mathf.Pow(sqrt_yx_2,2)+Mathf.Pow(sqrt_xz_2,2));
                   float sqrt_yx_xz_zy_2=Mathf.Sqrt(Mathf.Pow(sqrt_yx_xz_2,2)+Mathf.Pow(sqrt_zy_2,2));
                  resultDensity=density*(1f-(sqrt_yx_xz_zy_2-sqrt_yx_xz_1)/(sqrt_yx_xz_zy_2));
                 }else if(y>=size.y&&x>=size.x){resultDensity=density*(1f-(sqrt_yx_2-sqrt_yx_1)/(sqrt_yx_2));
                 }else if(x>=size.x&&z>=size.z){resultDensity=density*(1f-(sqrt_xz_2-sqrt_xz_1)/(sqrt_xz_2));
                 }else if(z>=size.z&&y>=size.y){resultDensity=density*(1f-(sqrt_zy_2-sqrt_zy_1)/(sqrt_zy_2));
                 }else if(y>=size.y){resultDensity=density*(1f-(y-size.y)/(float)y)*1.414f;
                 }else if(x>=size.x){resultDensity=density*(1f-(x-size.x)/(float)x)*1.414f;
                 }else if(z>=size.z){resultDensity=density*(1f-(z-size.z)/(float)z)*1.414f;
                 }else{
                  resultDensity=0d;
                 }
                }else{
                 resultDensity=density;
                }
                //  get current file data to merge
                if(!readData.ContainsKey(cnkIdx3)){
                 if(chunkDataPool.TryDequeue(out Dictionary<Vector3Int,(double,MaterialId)>chunkData)){
                  readData.Add(cnkIdx3,chunkData);
                 }else{
                  readData.Add(cnkIdx3,new Dictionary<Vector3Int,(double,MaterialId)>());
                 }
                 cnkIdxValuesToRead.Clear();
                 cnkIdxValuesToRead.Add(cnkIdx3);
                 VoxelSystem.ReadFile(editsFileStream,editsFileStreamReader,readData,cnkIdxValuesToRead);
                }
                Voxel currentVoxel;
                if(readData.ContainsKey(cnkIdx3)&&readData[cnkIdx3].ContainsKey(vCoord3)){
                 (double density,MaterialId materialId)voxelData=readData[cnkIdx3][vCoord3];
                 currentVoxel=new Voxel(voxelData.density,Vector3.zero,voxelData.materialId);
                }else{
                 currentVoxel=new Voxel();
                 Vector3Int noiseInput=vCoord3;noiseInput.x+=cnkRgn3.x;
                                               noiseInput.z+=cnkRgn3.y;
                 VoxelSystem.biome.Setvxl(noiseInput,null,null,0,vCoord3.z+vCoord3.x*Depth,ref currentVoxel);
                }
                resultDensity=Math.Max(resultDensity,currentVoxel.Density);
                if(material==MaterialId.Air&&!(-resultDensity>=-IsoLevel)){
                 resultDensity=-resultDensity;
                }
                if(!saveData.ContainsKey(cnkIdx3)){
                 if(chunkDataPool.TryDequeue(out Dictionary<Vector3Int,(double,MaterialId)>chunkData)){
                  saveData.Add(cnkIdx3,chunkData);
                 }else{
                  saveData.Add(cnkIdx3,new Dictionary<Vector3Int,(double,MaterialId)>());
                 }
                }
                saveData[cnkIdx3][vCoord3]=(resultDensity,-resultDensity>=-IsoLevel?MaterialId.Air:material);
                container.dirty.Add(cnkIdx3);
                for(int ngbx=-1;ngbx<=1;ngbx++){
                for(int ngbz=-1;ngbz<=1;ngbz++){
                 if(ngbx==0&&ngbz==0){
                  continue;
                 }
                 Vector2Int nCoord1=cCoord3+new Vector2Int(ngbx,ngbz);
                 if(Math.Abs(nCoord1.x)>=MaxcCoordx||
                    Math.Abs(nCoord1.y)>=MaxcCoordy){
                  continue;
                 }
                 int ngbIdx1=GetcnkIdx(nCoord1.x,nCoord1.y);
                 container.dirty.Add(ngbIdx1);
                }}
               if(z==0){break;}
              }}
               if(x==0){break;}
              }}
               }
               if(y==0){break;}
              }}
              break;
             }
            }
           }           
           foreach(var syn in VoxelSystem.terrainSynchronization)Monitor.Enter(syn.Value);
           try{
            VoxelSystem.WriteFile(editsFileStream,editsFileStreamWriter,editsFileStreamReader,editsStringBuilder,saveData);
           }catch{
            throw;
           }finally{
            foreach(var syn in VoxelSystem.terrainSynchronization)Monitor.Exit(syn.Value);
           }
          }
         }
        }
    }
}
namespace paulbourke.MarchingCubes{
    internal static class Tables{
        internal static readonly ReadOnlyCollection<int>EdgeTable=new ReadOnlyCollection<int>(new int[256]{
            0x0  ,0x109,0x203,0x30a,0x406,0x50f,0x605,0x70c,
            0x80c,0x905,0xa0f,0xb06,0xc0a,0xd03,0xe09,0xf00,
            0x190,0x99 ,0x393,0x29a,0x596,0x49f,0x795,0x69c,
            0x99c,0x895,0xb9f,0xa96,0xd9a,0xc93,0xf99,0xe90,
            0x230,0x339,0x33 ,0x13a,0x636,0x73f,0x435,0x53c,
            0xa3c,0xb35,0x83f,0x936,0xe3a,0xf33,0xc39,0xd30,
            0x3a0,0x2a9,0x1a3,0xaa ,0x7a6,0x6af,0x5a5,0x4ac,
            0xbac,0xaa5,0x9af,0x8a6,0xfaa,0xea3,0xda9,0xca0,
            0x460,0x569,0x663,0x76a,0x66 ,0x16f,0x265,0x36c,
            0xc6c,0xd65,0xe6f,0xf66,0x86a,0x963,0xa69,0xb60,
            0x5f0,0x4f9,0x7f3,0x6fa,0x1f6,0xff ,0x3f5,0x2fc,
            0xdfc,0xcf5,0xfff,0xef6,0x9fa,0x8f3,0xbf9,0xaf0,
            0x650,0x759,0x453,0x55a,0x256,0x35f,0x55 ,0x15c,
            0xe5c,0xf55,0xc5f,0xd56,0xa5a,0xb53,0x859,0x950,
            0x7c0,0x6c9,0x5c3,0x4ca,0x3c6,0x2cf,0x1c5,0xcc ,
            0xfcc,0xec5,0xdcf,0xcc6,0xbca,0xac3,0x9c9,0x8c0,
            0x8c0,0x9c9,0xac3,0xbca,0xcc6,0xdcf,0xec5,0xfcc,
            0xcc ,0x1c5,0x2cf,0x3c6,0x4ca,0x5c3,0x6c9,0x7c0,
            0x950,0x859,0xb53,0xa5a,0xd56,0xc5f,0xf55,0xe5c,
            0x15c,0x55 ,0x35f,0x256,0x55a,0x453,0x759,0x650,
            0xaf0,0xbf9,0x8f3,0x9fa,0xef6,0xfff,0xcf5,0xdfc,
            0x2fc,0x3f5,0xff ,0x1f6,0x6fa,0x7f3,0x4f9,0x5f0,
            0xb60,0xa69,0x963,0x86a,0xf66,0xe6f,0xd65,0xc6c,
            0x36c,0x265,0x16f,0x66 ,0x76a,0x663,0x569,0x460,
            0xca0,0xda9,0xea3,0xfaa,0x8a6,0x9af,0xaa5,0xbac,
            0x4ac,0x5a5,0x6af,0x7a6,0xaa ,0x1a3,0x2a9,0x3a0,
            0xd30,0xc39,0xf33,0xe3a,0x936,0x83f,0xb35,0xa3c,
            0x53c,0x435,0x73f,0x636,0x13a,0x33 ,0x339,0x230,
            0xe90,0xf99,0xc93,0xd9a,0xa96,0xb9f,0x895,0x99c,
            0x69c,0x795,0x49f,0x596,0x29a,0x393,0x99 ,0x190,
            0xf00,0xe09,0xd03,0xc0a,0xb06,0xa0f,0x905,0x80c,
            0x70c,0x605,0x50f,0x406,0x30a,0x203,0x109,0x0
        });
        #region TriangleTable
        internal static readonly ReadOnlyCollection<int[]>TriangleTable=new ReadOnlyCollection<int[]>(new int[256][]{
            new int[16]{-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 8, 3, 9, 8, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3, 1, 2,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 2,10, 0, 2, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 8, 3, 2,10, 8,10, 9, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3,11, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0,11, 2, 8,11, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 9, 0, 2, 3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1,11, 2, 1, 9,11, 9, 8,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3,10, 1,11,10, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0,10, 1, 0, 8,10, 8,11,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 9, 0, 3,11, 9,11,10, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 8,10,10, 8,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 7, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 3, 0, 7, 3, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 1, 9, 4, 7, 1, 7, 3, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 4, 7, 3, 0, 4, 1, 2,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 2,10, 9, 0, 2, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2,10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4,-1,-1,-1,-1},
            new int[16]{ 8, 4, 7, 3,11, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 4, 7,11, 2, 4, 2, 0, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 0, 1, 8, 4, 7, 2, 3,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 7,11, 9, 4,11, 9,11, 2, 9, 2, 1,-1,-1,-1,-1},
            new int[16]{ 3,10, 1, 3,11,10, 7, 8, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1,11,10, 1, 4,11, 1, 0, 4, 7,11, 4,-1,-1,-1,-1},
            new int[16]{ 4, 7, 8, 9, 0,11, 9,11,10,11, 0, 3,-1,-1,-1,-1},
            new int[16]{ 4, 7,11, 4,11, 9, 9,11,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 5, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 5, 4, 0, 8, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 5, 4, 1, 5, 0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 5, 4, 8, 3, 5, 3, 1, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 0, 8, 1, 2,10, 4, 9, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5, 2,10, 5, 4, 2, 4, 0, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2,10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8,-1,-1,-1,-1},
            new int[16]{ 9, 5, 4, 2, 3,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0,11, 2, 0, 8,11, 4, 9, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 5, 4, 0, 1, 5, 2, 3,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 1, 5, 2, 5, 8, 2, 8,11, 4, 8, 5,-1,-1,-1,-1},
            new int[16]{10, 3,11,10, 1, 3, 9, 5, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 9, 5, 0, 8, 1, 8,10, 1, 8,11,10,-1,-1,-1,-1},
            new int[16]{ 5, 4, 0, 5, 0,11, 5,11,10,11, 0, 3,-1,-1,-1,-1},
            new int[16]{ 5, 4, 8, 5, 8,10,10, 8,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 7, 8, 5, 7, 9,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 3, 0, 9, 5, 3, 5, 7, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 7, 8, 0, 1, 7, 1, 5, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 5, 3, 3, 5, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 7, 8, 9, 5, 7,10, 1, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3,-1,-1,-1,-1},
            new int[16]{ 8, 0, 2, 8, 2, 5, 8, 5, 7,10, 5, 2,-1,-1,-1,-1},
            new int[16]{ 2,10, 5, 2, 5, 3, 3, 5, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7, 9, 5, 7, 8, 9, 3,11, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7,11,-1,-1,-1,-1},
            new int[16]{ 2, 3,11, 0, 1, 8, 1, 7, 8, 1, 5, 7,-1,-1,-1,-1},
            new int[16]{11, 2, 1,11, 1, 7, 7, 1, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 5, 8, 8, 5, 7,10, 1, 3,10, 3,11,-1,-1,-1,-1},
            new int[16]{ 5, 7, 0, 5, 0, 9, 7,11, 0, 1, 0,10,11,10, 0,-1},
            new int[16]{11,10, 0,11, 0, 3,10, 5, 0, 8, 0, 7, 5, 7, 0,-1},
            new int[16]{11,10, 5, 7,11, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 6, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3, 5,10, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 0, 1, 5,10, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 8, 3, 1, 9, 8, 5,10, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 6, 5, 2, 6, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 6, 5, 1, 2, 6, 3, 0, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 6, 5, 9, 0, 6, 0, 2, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8,-1,-1,-1,-1},
            new int[16]{ 2, 3,11,10, 6, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 0, 8,11, 2, 0,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9, 2, 3,11, 5,10, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5,10, 6, 1, 9, 2, 9,11, 2, 9, 8,11,-1,-1,-1,-1},
            new int[16]{ 6, 3,11, 6, 5, 3, 5, 1, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8,11, 0,11, 5, 0, 5, 1, 5,11, 6,-1,-1,-1,-1},
            new int[16]{ 3,11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9,-1,-1,-1,-1},
            new int[16]{ 6, 5, 9, 6, 9,11,11, 9, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5,10, 6, 4, 7, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 3, 0, 4, 7, 3, 6, 5,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 9, 0, 5,10, 6, 8, 4, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4,-1,-1,-1,-1},
            new int[16]{ 6, 1, 2, 6, 5, 1, 4, 7, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7,-1,-1,-1,-1},
            new int[16]{ 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6,-1,-1,-1,-1},
            new int[16]{ 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9,-1},
            new int[16]{ 3,11, 2, 7, 8, 4,10, 6, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5,10, 6, 4, 7, 2, 4, 2, 0, 2, 7,11,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9, 4, 7, 8, 2, 3,11, 5,10, 6,-1,-1,-1,-1},
            new int[16]{ 9, 2, 1, 9,11, 2, 9, 4,11, 7,11, 4, 5,10, 6,-1},
            new int[16]{ 8, 4, 7, 3,11, 5, 3, 5, 1, 5,11, 6,-1,-1,-1,-1},
            new int[16]{ 5, 1,11, 5,11, 6, 1, 0,11, 7,11, 4, 0, 4,11,-1},
            new int[16]{ 0, 5, 9, 0, 6, 5, 0, 3, 6,11, 6, 3, 8, 4, 7,-1},
            new int[16]{ 6, 5, 9, 6, 9,11, 4, 7, 9, 7,11, 9,-1,-1,-1,-1},
            new int[16]{10, 4, 9, 6, 4,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4,10, 6, 4, 9,10, 0, 8, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 0, 1,10, 6, 0, 6, 4, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1,10,-1,-1,-1,-1},
            new int[16]{ 1, 4, 9, 1, 2, 4, 2, 6, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4,-1,-1,-1,-1},
            new int[16]{ 0, 2, 4, 4, 2, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 3, 2, 8, 2, 4, 4, 2, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 4, 9,10, 6, 4,11, 2, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 2, 2, 8,11, 4, 9,10, 4,10, 6,-1,-1,-1,-1},
            new int[16]{ 3,11, 2, 0, 1, 6, 0, 6, 4, 6, 1,10,-1,-1,-1,-1},
            new int[16]{ 6, 4, 1, 6, 1,10, 4, 8, 1, 2, 1,11, 8,11, 1,-1},
            new int[16]{ 9, 6, 4, 9, 3, 6, 9, 1, 3,11, 6, 3,-1,-1,-1,-1},
            new int[16]{ 8,11, 1, 8, 1, 0,11, 6, 1, 9, 1, 4, 6, 4, 1,-1},
            new int[16]{ 3,11, 6, 3, 6, 0, 0, 6, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 6, 4, 8,11, 6, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7,10, 6, 7, 8,10, 8, 9,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 7, 3, 0,10, 7, 0, 9,10, 6, 7,10,-1,-1,-1,-1},
            new int[16]{10, 6, 7, 1,10, 7, 1, 7, 8, 1, 8, 0,-1,-1,-1,-1},
            new int[16]{10, 6, 7,10, 7, 1, 1, 7, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7,-1,-1,-1,-1},
            new int[16]{ 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9,-1},
            new int[16]{ 7, 8, 0, 7, 0, 6, 6, 0, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7, 3, 2, 6, 7, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 3,11,10, 6, 8,10, 8, 9, 8, 6, 7,-1,-1,-1,-1},
            new int[16]{ 2, 0, 7, 2, 7,11, 0, 9, 7, 6, 7,10, 9,10, 7,-1},
            new int[16]{ 1, 8, 0, 1, 7, 8, 1,10, 7, 6, 7,10, 2, 3,11,-1},
            new int[16]{11, 2, 1,11, 1, 7,10, 6, 1, 6, 7, 1,-1,-1,-1,-1},
            new int[16]{ 8, 9, 6, 8, 6, 7, 9, 1, 6,11, 6, 3, 1, 3, 6,-1},
            new int[16]{ 0, 9, 1,11, 6, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7, 8, 0, 7, 0, 6, 3,11, 0,11, 6, 0,-1,-1,-1,-1},
            new int[16]{ 7,11, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7, 6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 0, 8,11, 7, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9,11, 7, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 1, 9, 8, 3, 1,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 1, 2, 6,11, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10, 3, 0, 8, 6,11, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 9, 0, 2,10, 9, 6,11, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 6,11, 7, 2,10, 3,10, 8, 3,10, 9, 8,-1,-1,-1,-1},
            new int[16]{ 7, 2, 3, 6, 2, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 7, 0, 8, 7, 6, 0, 6, 2, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 7, 6, 2, 3, 7, 0, 1, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6,-1,-1,-1,-1},
            new int[16]{10, 7, 6,10, 1, 7, 1, 3, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 7, 6, 1, 7,10, 1, 8, 7, 1, 0, 8,-1,-1,-1,-1},
            new int[16]{ 0, 3, 7, 0, 7,10, 0,10, 9, 6,10, 7,-1,-1,-1,-1},
            new int[16]{ 7, 6,10, 7,10, 8, 8,10, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 6, 8, 4,11, 8, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 6,11, 3, 0, 6, 0, 4, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 6,11, 8, 4, 6, 9, 0, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 4, 6, 9, 6, 3, 9, 3, 1,11, 3, 6,-1,-1,-1,-1},
            new int[16]{ 6, 8, 4, 6,11, 8, 2,10, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10, 3, 0,11, 0, 6,11, 0, 4, 6,-1,-1,-1,-1},
            new int[16]{ 4,11, 8, 4, 6,11, 0, 2, 9, 2,10, 9,-1,-1,-1,-1},
            new int[16]{10, 9, 3,10, 3, 2, 9, 4, 3,11, 3, 6, 4, 6, 3,-1},
            new int[16]{ 8, 2, 3, 8, 4, 2, 4, 6, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 4, 2, 4, 6, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8,-1,-1,-1,-1},
            new int[16]{ 1, 9, 4, 1, 4, 2, 2, 4, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 1, 3, 8, 6, 1, 8, 4, 6, 6,10, 1,-1,-1,-1,-1},
            new int[16]{10, 1, 0,10, 0, 6, 6, 0, 4,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 6, 3, 4, 3, 8, 6,10, 3, 0, 3, 9,10, 9, 3,-1},
            new int[16]{10, 9, 4, 6,10, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 9, 5, 7, 6,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3, 4, 9, 5,11, 7, 6,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5, 0, 1, 5, 4, 0, 7, 6,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5,-1,-1,-1,-1},
            new int[16]{ 9, 5, 4,10, 1, 2, 7, 6,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 6,11, 7, 1, 2,10, 0, 8, 3, 4, 9, 5,-1,-1,-1,-1},
            new int[16]{ 7, 6,11, 5, 4,10, 4, 2,10, 4, 0, 2,-1,-1,-1,-1},
            new int[16]{ 3, 4, 8, 3, 5, 4, 3, 2, 5,10, 5, 2,11, 7, 6,-1},
            new int[16]{ 7, 2, 3, 7, 6, 2, 5, 4, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7,-1,-1,-1,-1},
            new int[16]{ 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0,-1,-1,-1,-1},
            new int[16]{ 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8,-1},
            new int[16]{ 9, 5, 4,10, 1, 6, 1, 7, 6, 1, 3, 7,-1,-1,-1,-1},
            new int[16]{ 1, 6,10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4,-1},
            new int[16]{ 4, 0,10, 4,10, 5, 0, 3,10, 6,10, 7, 3, 7,10,-1},
            new int[16]{ 7, 6,10, 7,10, 8, 5, 4,10, 4, 8,10,-1,-1,-1,-1},
            new int[16]{ 6, 9, 5, 6,11, 9,11, 8, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 6,11, 0, 6, 3, 0, 5, 6, 0, 9, 5,-1,-1,-1,-1},
            new int[16]{ 0,11, 8, 0, 5,11, 0, 1, 5, 5, 6,11,-1,-1,-1,-1},
            new int[16]{ 6,11, 3, 6, 3, 5, 5, 3, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,10, 9, 5,11, 9,11, 8,11, 5, 6,-1,-1,-1,-1},
            new int[16]{ 0,11, 3, 0, 6,11, 0, 9, 6, 5, 6, 9, 1, 2,10,-1},
            new int[16]{11, 8, 5,11, 5, 6, 8, 0, 5,10, 5, 2, 0, 2, 5,-1},
            new int[16]{ 6,11, 3, 6, 3, 5, 2,10, 3,10, 5, 3,-1,-1,-1,-1},
            new int[16]{ 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2,-1,-1,-1,-1},
            new int[16]{ 9, 5, 6, 9, 6, 0, 0, 6, 2,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8,-1},
            new int[16]{ 1, 5, 6, 2, 1, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 3, 6, 1, 6,10, 3, 8, 6, 5, 6, 9, 8, 9, 6,-1},
            new int[16]{10, 1, 0,10, 0, 6, 9, 5, 0, 5, 6, 0,-1,-1,-1,-1},
            new int[16]{ 0, 3, 8, 5, 6,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 5, 6,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 5,10, 7, 5,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 5,10,11, 7, 5, 8, 3, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5,11, 7, 5,10,11, 1, 9, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{10, 7, 5,10,11, 7, 9, 8, 1, 8, 3, 1,-1,-1,-1,-1},
            new int[16]{11, 1, 2,11, 7, 1, 7, 5, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2,11,-1,-1,-1,-1},
            new int[16]{ 9, 7, 5, 9, 2, 7, 9, 0, 2, 2,11, 7,-1,-1,-1,-1},
            new int[16]{ 7, 5, 2, 7, 2,11, 5, 9, 2, 3, 2, 8, 9, 8, 2,-1},
            new int[16]{ 2, 5,10, 2, 3, 5, 3, 7, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 2, 0, 8, 5, 2, 8, 7, 5,10, 2, 5,-1,-1,-1,-1},
            new int[16]{ 9, 0, 1, 5,10, 3, 5, 3, 7, 3,10, 2,-1,-1,-1,-1},
            new int[16]{ 9, 8, 2, 9, 2, 1, 8, 7, 2,10, 2, 5, 7, 5, 2,-1},
            new int[16]{ 1, 3, 5, 3, 7, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 7, 0, 7, 1, 1, 7, 5,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 0, 3, 9, 3, 5, 5, 3, 7,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9, 8, 7, 5, 9, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5, 8, 4, 5,10, 8,10,11, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 5, 0, 4, 5,11, 0, 5,10,11,11, 3, 0,-1,-1,-1,-1},
            new int[16]{ 0, 1, 9, 8, 4,10, 8,10,11,10, 4, 5,-1,-1,-1,-1},
            new int[16]{10,11, 4,10, 4, 5,11, 3, 4, 9, 4, 1, 3, 1, 4,-1},
            new int[16]{ 2, 5, 1, 2, 8, 5, 2,11, 8, 4, 5, 8,-1,-1,-1,-1},
            new int[16]{ 0, 4,11, 0,11, 3, 4, 5,11, 2,11, 1, 5, 1,11,-1},
            new int[16]{ 0, 2, 5, 0, 5, 9, 2,11, 5, 4, 5, 8,11, 8, 5,-1},
            new int[16]{ 9, 4, 5, 2,11, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 5,10, 3, 5, 2, 3, 4, 5, 3, 8, 4,-1,-1,-1,-1},
            new int[16]{ 5,10, 2, 5, 2, 4, 4, 2, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3,10, 2, 3, 5,10, 3, 8, 5, 4, 5, 8, 0, 1, 9,-1},
            new int[16]{ 5,10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2,-1,-1,-1,-1},
            new int[16]{ 8, 4, 5, 8, 5, 3, 3, 5, 1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 4, 5, 1, 0, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5,-1,-1,-1,-1},
            new int[16]{ 9, 4, 5,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4,11, 7, 4, 9,11, 9,10,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 8, 3, 4, 9, 7, 9,11, 7, 9,10,11,-1,-1,-1,-1},
            new int[16]{ 1,10,11, 1,11, 4, 1, 4, 0, 7, 4,11,-1,-1,-1,-1},
            new int[16]{ 3, 1, 4, 3, 4, 8, 1,10, 4, 7, 4,11,10,11, 4,-1},
            new int[16]{ 4,11, 7, 9,11, 4, 9, 2,11, 9, 1, 2,-1,-1,-1,-1},
            new int[16]{ 9, 7, 4, 9,11, 7, 9, 1,11, 2,11, 1, 0, 8, 3,-1},
            new int[16]{11, 7, 4,11, 4, 2, 2, 4, 0,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{11, 7, 4,11, 4, 2, 8, 3, 4, 3, 2, 4,-1,-1,-1,-1},
            new int[16]{ 2, 9,10, 2, 7, 9, 2, 3, 7, 7, 4, 9,-1,-1,-1,-1},
            new int[16]{ 9,10, 7, 9, 7, 4,10, 2, 7, 8, 7, 0, 2, 0, 7,-1},
            new int[16]{ 3, 7,10, 3,10, 2, 7, 4,10, 1,10, 0, 4, 0,10,-1},
            new int[16]{ 1,10, 2, 8, 7, 4,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 9, 1, 4, 1, 7, 7, 1, 3,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1,-1,-1,-1,-1},
            new int[16]{ 4, 0, 3, 7, 4, 3,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 4, 8, 7,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9,10, 8,10,11, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 0, 9, 3, 9,11,11, 9,10,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 1,10, 0,10, 8, 8,10,11,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 1,10,11, 3,10,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 2,11, 1,11, 9, 9,11, 8,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 0, 9, 3, 9,11, 1, 2, 9, 2,11, 9,-1,-1,-1,-1},
            new int[16]{ 0, 2,11, 8, 0,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 3, 2,11,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 3, 8, 2, 8,10,10, 8, 9,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 9,10, 2, 0, 9, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 2, 3, 8, 2, 8,10, 0, 1, 8, 1,10, 8,-1,-1,-1,-1},
            new int[16]{ 1,10, 2,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 1, 3, 8, 9, 1, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 9, 1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{ 0, 3, 8,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1},
            new int[16]{-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1}
        });
        #endregion
    }
}