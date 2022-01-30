using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static AKCondinoO.Voxels.VoxelSystem;
using static AKCondinoO.Voxels.VoxelTerrain.MarchingCubesBackgroundContainer;
namespace AKCondinoO.Voxels{
    internal class VoxelTerrain:MonoBehaviour{
        internal readonly object synchronizer=new object();
        internal Bounds worldBounds=new Bounds(Vector3.zero,new Vector3(Width,Height,Depth));
        MeshFilter filter;
        void Awake(){
         mesh=new Mesh(){
          bounds=worldBounds,
         };
         filter=GetComponent<MeshFilter>();
         filter.mesh=mesh;
        }
        internal LinkedListNode<VoxelTerrain>expropriated;
        internal void OnInstantiated(){
         marchingCubesBG=new MarchingCubesBackgroundContainer(synchronizer);
         marchingCubesBG.TempVer=new NativeList<Vertex>(Allocator.Persistent);
         marchingCubesBG.TempTri=new NativeList<UInt32>(Allocator.Persistent);
        }
        internal void OnExit(){
         marchingCubesBG.IsCompleted(VoxelSystem.Singleton.marchingCubesBGThreads[0].IsRunning,-1);
         if(marchingCubesBG.TempVer.IsCreated)marchingCubesBG.TempVer.Dispose();
         if(marchingCubesBG.TempTri.IsCreated)marchingCubesBG.TempTri.Dispose();
        }
        Vector2Int cCoord;
        Vector2Int cnkRgn;
        internal int?cnkIdx=null;
        internal void OncCoordChanged(Vector2Int cCoord1,bool firstCall){
         if(firstCall||cCoord1!=cCoord){
          cCoord=cCoord1;
          cnkRgn=cCoordTocnkRgn(cCoord);
          pendingMovement=true;
         }
        }
        bool waitingMarchingCubes;
        bool pendingMovement;
        internal void ManualUpdate(){
            if(waitingMarchingCubes&&OnBuilt()){
                waitingMarchingCubes=false;
            }else{
                if(pendingMovement&&OnMoving()){
                    pendingMovement=false;OnMoved();
                }
            }
        }
        bool OnMoving(){
         if(marchingCubesBG.IsCompleted(VoxelSystem.Singleton.marchingCubesBGThreads[0].IsRunning)){
          worldBounds.center=transform.position=new Vector3(cnkRgn.x,0,cnkRgn.y);
          marchingCubesBG.cCoord=cCoord;
          marchingCubesBG.cnkRgn=cnkRgn;
          marchingCubesBG.cnkIdx=cnkIdx.Value;
          MarchingCubesMultithreaded.Schedule(marchingCubesBG);
          return true;
         }
         return false;
        }
        void OnMoved(){
         waitingMarchingCubes=true;
        }
        #region Rendering
            static readonly VertexAttributeDescriptor[]layout=new[]{
             new VertexAttributeDescriptor(VertexAttribute.Position ,VertexAttributeFormat.Float32,3),
             new VertexAttributeDescriptor(VertexAttribute.Normal   ,VertexAttributeFormat.Float32,3),
             new VertexAttributeDescriptor(VertexAttribute.Color    ,VertexAttributeFormat.Float32,4),
             new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
             new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,2),
             new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,2),
             new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float32,2),
            };
            MeshUpdateFlags meshFlags=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontResetBoneBounds;
            internal Mesh mesh;
        #endregion
        bool OnBuilt(){
         if(marchingCubesBG.IsCompleted(VoxelSystem.Singleton.marchingCubesBGThreads[0].IsRunning)){
          bool resize;
          if(resize=marchingCubesBG.TempVer.Length>mesh.vertexCount){
           mesh.SetVertexBufferParams(marchingCubesBG.TempVer.Length,layout);
          }
          mesh.SetVertexBufferData(marchingCubesBG.TempVer.AsArray(),0,0,marchingCubesBG.TempVer.Length,0,meshFlags);
          if(resize){
           mesh.SetIndexBufferParams(marchingCubesBG.TempTri.Length,IndexFormat.UInt32);
          }
          mesh.SetIndexBufferData(marchingCubesBG.TempTri.AsArray(),0,0,marchingCubesBG.TempTri.Length,meshFlags);
          mesh.subMeshCount=1;
          mesh.SetSubMesh(0,new SubMeshDescriptor(0,marchingCubesBG.TempTri.Length){firstVertex=0,vertexCount=marchingCubesBG.TempVer.Length},meshFlags);

          mesh.OptimizeIndexBuffers();
          mesh.OptimizeReorderVertexBuffer();
          return true;
         }
         return false;
        }
        internal MarchingCubesBackgroundContainer marchingCubesBG;
        internal class MarchingCubesBackgroundContainer:BackgroundContainer{
         internal readonly object voxelSystemSynchronizer;        
         internal Vector2Int cCoord;
         internal Vector2Int cnkRgn;
         internal        int cnkIdx;
         internal MarchingCubesBackgroundContainer(object syn){
          voxelSystemSynchronizer=syn;
         }
         [StructLayout(LayoutKind.Sequential)]internal struct Vertex{
          internal Vector3 pos;
          internal Vector3 normal;
          internal Color color;
          internal Vector2 texCoord0;
          internal Vector2 texCoord1;
          internal Vector2 texCoord2;
          internal Vector2 texCoord3;
          internal Vertex(Vector3 p,Vector3 n,Vector2 uv0){
           pos=p;
           normal=n;
           color=new Color(1f,0f,0f,0f);
           texCoord0=uv0;
           texCoord1=new Vector2(-1f,-1f);
           texCoord2=new Vector2(-1f,-1f);
           texCoord3=new Vector2(-1f,-1f);
          }
         }
         internal NativeList<Vertex>TempVer;
         internal NativeList<UInt32>TempTri;
        }
        internal class MarchingCubesMultithreaded:BaseMultithreaded<MarchingCubesBackgroundContainer>{
         readonly Voxel[]voxels=new Voxel[VoxelsPerChunk];
         readonly Voxel[]polygonCell=new Voxel[8];
         readonly Voxel[][][]voxelsCache1=new Voxel[3][][]{new Voxel[1][]{new Voxel[4],},new Voxel[Depth][],new Voxel[FlattenOffset][],};
         readonly Voxel[]tmpvxl=new Voxel[6];
         readonly Voxel[][]voxelsCache2=new Voxel[3][]{new Voxel[1],new Voxel[Depth],new Voxel[FlattenOffset],};
         readonly Vector3[]vertices=new Vector3[12];
         readonly Vector3[][][]verticesCache=new Vector3[3][][]{new Vector3[1][]{new Vector3[4],},new Vector3[Depth][],new Vector3[FlattenOffset][],};
         readonly MaterialId[]materials=new MaterialId[12];
         readonly Vector3[]normals=new Vector3[12];
         readonly double[]density=new double[2];
         readonly Vector3[]vertex=new Vector3[2];
         readonly MaterialId[]material=new MaterialId[2];
         readonly float[]distance=new float[2];
         readonly int[]idx=new int[3];
         readonly Vector3[]verPos=new Vector3[3];
         readonly Dictionary<Vector3,List<Vector2>>vertexUV=new Dictionary<Vector3,List<Vector2>>();
         internal MarchingCubesMultithreaded(){
          for(int i=0;i<voxelsCache1[2].Length;++i){voxelsCache1[2][i]=new Voxel[4];if(i<voxelsCache1[1].Length){voxelsCache1[1][i]=new Voxel[4];}}
          for(int i=0;i<verticesCache[2].Length;++i){verticesCache[2][i]=new Vector3[4];if(i<verticesCache[1].Length){verticesCache[1][i]=new Vector3[4];}}
         }
         protected override void Cleanup(){
          Array.Clear(voxels,0,voxels.Length);
          for(int i=0;i<voxelsCache1[0].Length;++i){Array.Clear(voxelsCache1[0][i],0,voxelsCache1[0][i].Length);}
          for(int i=0;i<voxelsCache1[1].Length;++i){Array.Clear(voxelsCache1[1][i],0,voxelsCache1[1][i].Length);}
          for(int i=0;i<voxelsCache1[2].Length;++i){Array.Clear(voxelsCache1[2][i],0,voxelsCache1[2][i].Length);}
          for(int i=0;i<voxelsCache2.Length;++i){if(voxelsCache2[i]!=null)Array.Clear(voxelsCache2[i],0,voxelsCache2[i].Length);}
          for(int i=0;i<verticesCache[0].Length;++i){Array.Clear(verticesCache[0][i],0,verticesCache[0][i].Length);}
          for(int i=0;i<verticesCache[1].Length;++i){Array.Clear(verticesCache[1][i],0,verticesCache[1][i].Length);}
          for(int i=0;i<verticesCache[2].Length;++i){Array.Clear(verticesCache[2][i],0,verticesCache[2][i].Length);}
          foreach(var kvp in vertexUV){
           var list=kvp.Value;
           list.Clear();
           VoxelSystem.vertexUVListPool.Enqueue(list);
          }
          vertexUV.Clear();
         }
         protected override void Execute(){
          //Logger.Debug("MarchingCubesMultithreaded Execute");
          container.TempVer.Clear();
          container.TempTri.Clear();
          Vector2Int posOffset=Vector2Int.zero;
          UInt32 vertexCount=0;
          Vector3Int vCoord1;
          for(vCoord1=new Vector3Int();vCoord1.y<Height;vCoord1.y++){
          for(vCoord1.x=0             ;vCoord1.x<Width ;vCoord1.x++){
          for(vCoord1.z=0             ;vCoord1.z<Depth ;vCoord1.z++){
           int corner=0;Vector3Int vCoord2=vCoord1;                                       if(vCoord1.z>0)polygonCell[corner]=voxelsCache1[0][0][0];else if(vCoord1.x>0)polygonCell[corner]=voxelsCache1[1][vCoord1.z][0];else if(vCoord1.y>0)polygonCell[corner]=voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][0];else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;vCoord2.x+=1;                          if(vCoord1.z>0)polygonCell[corner]=voxelsCache1[0][0][1];                                                                      else if(vCoord1.y>0)polygonCell[corner]=voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][1];else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;             if(vCoord1.z>0)polygonCell[corner]=voxelsCache1[0][0][2];                                                                                                                                                            else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;             vCoord2.y+=1;             if(vCoord1.z>0)polygonCell[corner]=voxelsCache1[0][0][3];else if(vCoord1.x>0)polygonCell[corner]=voxelsCache1[1][vCoord1.z][1];                                                                                      else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;                          vCoord2.z+=1;                                                              if(vCoord1.x>0)polygonCell[corner]=voxelsCache1[1][vCoord1.z][2];else if(vCoord1.y>0)polygonCell[corner]=voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][2];else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;vCoord2.x+=1;             vCoord2.z+=1;                                                                                                                                    if(vCoord1.y>0)polygonCell[corner]=voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][3];else SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;vCoord2.x+=1;vCoord2.y+=1;vCoord2.z+=1;                                                                                                                                                                                                                          SetpolygonCellVoxel();
               corner++;           vCoord2=vCoord1;             vCoord2.y+=1;vCoord2.z+=1;                                                              if(vCoord1.x>0)polygonCell[corner]=voxelsCache1[1][vCoord1.z][3];                                                                                      else SetpolygonCellVoxel();
           voxelsCache1[0][0][0]=polygonCell[4];
           voxelsCache1[0][0][1]=polygonCell[5];
           voxelsCache1[0][0][2]=polygonCell[6];
           voxelsCache1[0][0][3]=polygonCell[7];
           voxelsCache1[1][vCoord1.z][0]=polygonCell[1];
           voxelsCache1[1][vCoord1.z][1]=polygonCell[2];
           voxelsCache1[1][vCoord1.z][2]=polygonCell[5];
           voxelsCache1[1][vCoord1.z][3]=polygonCell[6];
           voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][0]=polygonCell[3];
           voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][1]=polygonCell[2];
           voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][2]=polygonCell[7];
           voxelsCache1[2][vCoord1.z+vCoord1.x*Depth][3]=polygonCell[6];
           void SetpolygonCellVoxel(){
            Vector2Int cnkRgn2=container.cnkRgn;
            Vector2Int cCoord2=container.cCoord;
            bool cache2=false;
            if(vCoord2.y<=0){/*  :fora do mundo, baixo:  */
             polygonCell[corner]=Voxel.Bedrock;
            }else if(vCoord2.y>=Height){/*  :fora do mundo, cima:  */
             polygonCell[corner]=Voxel.Air;
            }else{
             if(vCoord2.x<0||vCoord2.x>=Width||
                vCoord2.z<0||vCoord2.z>=Depth
             ){
              ValidateCoord(ref cnkRgn2,ref vCoord2);
              cCoord2=cnkRgnTocCoord(cnkRgn2);
             }else{
              cache2=true;
             }
             polygonCell[corner]=Voxel.Air;
            }
            if(polygonCell[corner].Normal==Vector3.zero){
             //  calcular normal:
             int tmpIdx=0;Vector3Int vCoord3=vCoord2;vCoord3.x++;                                                                                                                                                                                              SetpolygonCellNormalSettmpvxl();
                 tmpIdx++;           vCoord3=vCoord2;vCoord3.x--;                        if(cache2&&vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsCache2[1][vCoord2.z].IsCreated)                tmpvxl[tmpIdx]=voxelsCache2[1][vCoord2.z];                else SetpolygonCellNormalSettmpvxl();
                 tmpIdx++;           vCoord3=vCoord2;            vCoord3.y++;                                                                                                                                                                                  SetpolygonCellNormalSettmpvxl();
                 tmpIdx++;           vCoord3=vCoord2;            vCoord3.y--;            if(cache2&&vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsCache2[2][vCoord2.z+vCoord2.x*Depth].IsCreated)tmpvxl[tmpIdx]=voxelsCache2[2][vCoord2.z+vCoord2.x*Depth];else SetpolygonCellNormalSettmpvxl();
                 tmpIdx++;           vCoord3=vCoord2;                        vCoord3.z++;                                                                                                                                                                      SetpolygonCellNormalSettmpvxl();
                 tmpIdx++;           vCoord3=vCoord2;                        vCoord3.z--;if(cache2&&vCoord2.z>1&&vCoord2.x>1&&vCoord2.y>1&&voxelsCache2[0][0].IsCreated)                        tmpvxl[tmpIdx]=voxelsCache2[0][0];                        else SetpolygonCellNormalSettmpvxl();
             void SetpolygonCellNormalSettmpvxl(){
              if(vCoord3.y<=0){
               tmpvxl[tmpIdx]=Voxel.Bedrock;
              }else if(vCoord3.y>=Height){
               tmpvxl[tmpIdx]=Voxel.Air;
              }else{
               tmpvxl[tmpIdx]=Voxel.Air;
              }
             }
             Vector3 polygonCellNormal=new Vector3{
              x=(float)(tmpvxl[1].Density-tmpvxl[0].Density),
              y=(float)(tmpvxl[3].Density-tmpvxl[2].Density),
              z=(float)(tmpvxl[5].Density-tmpvxl[4].Density)
             };
             polygonCell[corner].Normal=polygonCellNormal;
             if(polygonCell[corner].Normal!=Vector3.zero){
              polygonCell[corner].Normal.Normalize();
             }
            }
            if(cache2){
             voxelsCache2[0][0]=polygonCell[corner];
             voxelsCache2[1][vCoord2.z]=polygonCell[corner];
             voxelsCache2[2][vCoord2.z+vCoord2.x*Depth]=polygonCell[corner];
            }
           }
           MarchingCubes(polygonCell,vCoord1,vertices,verticesCache,materials,normals,density,vertex,material,distance,idx,verPos,posOffset,ref vertexCount,container.TempVer,container.TempTri,vertexUV);
          }}}
         }
        }
        #if UNITY_EDITOR
            void OnDrawGizmos(){
             Logger.DrawBounds(worldBounds,Color.gray);
            }
        #endif
    }
}