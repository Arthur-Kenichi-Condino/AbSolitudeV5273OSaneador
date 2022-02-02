using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using static AKCondinoO.Voxels.VoxelSystem;
using static AKCondinoO.Voxels.VoxelSystem.TerrainEditingMultithreaded;
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
        internal void OnEdited(){
         pendingEditChanges=true;
        }
        bool waitingMarchingCubes;
        bool pendingMovement;
        bool pendingEditChanges;
        internal void ManualUpdate(){
            if(waitingMarchingCubes&&OnMeshDataSet()){
               waitingMarchingCubes=false;
            }else{
                if(pendingMovement&&OnApplyingMovement()){
                   pendingMovement=false;
                    OnMovementApplied();
                }else if(pendingEditChanges&&OnPushingEditChanges()){
                         pendingEditChanges=false;
                    OnEditChangesPushed();
                }
            }
        }
        bool OnApplyingMovement(){
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
        void OnMovementApplied(){
         waitingMarchingCubes=true;
        }
        bool OnPushingEditChanges(){
         if(marchingCubesBG.IsCompleted(VoxelSystem.Singleton.marchingCubesBGThreads[0].IsRunning)){
          MarchingCubesMultithreaded.Schedule(marchingCubesBG);
          return true;
         }
         return false;
        }
        void OnEditChangesPushed(){
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
        bool OnMeshDataSet(){
         if(marchingCubesBG.IsCompleted(VoxelSystem.Singleton.marchingCubesBGThreads[0].IsRunning)){
          mesh.Clear(false);
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
         internal readonly FileStream editsFileStream;
          internal readonly StreamReader editsFileStreamReader;
         internal static Vector2 emptyUV{get;}=new Vector2(-1,-1);
         readonly Voxel[]voxels=new Voxel[VoxelsPerChunk];
         readonly Dictionary<int,Voxel>[]neighbors=new Dictionary<int,Voxel>[8];
         readonly Voxel[]polygonCell=new Voxel[8];
         readonly double[][][]noiseForHeightCache=new double[biome.heightsCacheLength][][];
         readonly MaterialId[][][]materialIdPerHeightNoiseCache=new MaterialId[biome.heightsCacheLength][][];
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
         readonly Dictionary<Vector2,int>vertexUVCounted=new Dictionary<Vector2,int>();
         readonly SortedDictionary<(int,float,float),Vector2>vertexUVSorted=new SortedDictionary<(int,float,float),Vector2>();
         readonly Dictionary<int,int>weights=new Dictionary<int,int>(4);
         readonly Dictionary<int,Dictionary<Vector3Int,(double density,MaterialId materialId)>>readData=new Dictionary<int,Dictionary<Vector3Int,(double,MaterialId)>>();
         readonly List<int>cnkIdxValuesToRead=new List<int>();
         readonly Dictionary<int,int>ngbIdxoftIdxPairs=new Dictionary<int,int>();
         internal MarchingCubesMultithreaded(){
          editsFileStream=new FileStream(VoxelSystem.editsFile,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
          editsFileStreamReader=new StreamReader(editsFileStream);
          for(int i=0;i<neighbors.Length;++i){neighbors[i]=new Dictionary<int,Voxel>();}
          for(int i=0;i<biome.heightsCacheLength;++i){
           noiseForHeightCache[i]=new double[9][];
           materialIdPerHeightNoiseCache[i]=new MaterialId[9][];
          }
          for(int i=0;i<voxelsCache1[2].Length;++i){voxelsCache1[2][i]=new Voxel[4];if(i<voxelsCache1[1].Length){voxelsCache1[1][i]=new Voxel[4];}}
          for(int i=0;i<verticesCache[2].Length;++i){verticesCache[2][i]=new Vector3[4];if(i<verticesCache[1].Length){verticesCache[1][i]=new Vector3[4];}}
         }
         protected override void Cleanup(){
          Array.Clear(voxels,0,voxels.Length);
          for(int i=0;i<neighbors.Length;++i){neighbors[i].Clear();}
          for(int i=0;i<biome.heightsCacheLength;++i){
           for(int j=0;j<noiseForHeightCache[i].Length;++j){
            if(noiseForHeightCache[i][j]!=null)Array.Clear(noiseForHeightCache[i][j],0,noiseForHeightCache[i][j].Length);
           }
           for(int j=0;j<materialIdPerHeightNoiseCache[i].Length;++j){
            if(materialIdPerHeightNoiseCache[i][j]!=null)Array.Clear(materialIdPerHeightNoiseCache[i][j],0,materialIdPerHeightNoiseCache[i][j].Length);
           }
          }
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
          foreach(var chunkData in readData){chunkData.Value.Clear();TerrainEditingMultithreaded.chunkDataPool.Enqueue(chunkData.Value);}
          readData.Clear();
         }
         protected override void Execute(){
          //Logger.Debug("MarchingCubesMultithreaded Execute");
          container.TempVer.Clear();
          container.TempTri.Clear();
          lock(container.voxelSystemSynchronizer){
           ngbIdxoftIdxPairs.Clear();
           cnkIdxValuesToRead.Clear();
           cnkIdxValuesToRead.Add(container.cnkIdx);
           for(int x=-1;x<=1;x++){
           for(int z=-1;z<=1;z++){
            if(x==0&&z==0){
             continue;
            }
            Vector2Int nCoord1=container.cCoord;
                       nCoord1.x+=x;
                       nCoord1.y+=z;
            if(Math.Abs(nCoord1.x)>=MaxcCoordx||
               Math.Abs(nCoord1.y)>=MaxcCoordy){
             continue;
            }
            int ngbIdx1=GetcnkIdx(nCoord1.x,nCoord1.y);
            int oftIdx1=GetoftIdx(nCoord1-container.cCoord)-1;
            ngbIdxoftIdxPairs[ngbIdx1]=oftIdx1;
            cnkIdxValuesToRead.Add(ngbIdx1);
           }}
           VoxelSystem.ReadFile(editsFileStream,editsFileStreamReader,readData,cnkIdxValuesToRead);
           if(readData.TryGetValue(container.cnkIdx,out Dictionary<Vector3Int,(double density,MaterialId materialId)>cnkEdits)){
            foreach(var vCoordEditPair in cnkEdits){var vCoord=vCoordEditPair.Key;var voxelData=vCoordEditPair.Value;
             voxels[GetvxlIdx(vCoord.x,vCoord.y,vCoord.z)]=new Voxel(voxelData.density,Vector3.zero,voxelData.materialId);
            }
           }
           foreach(var ngbIdxoftIdxPair in ngbIdxoftIdxPairs){int ngbIdx=ngbIdxoftIdxPair.Key;int oftIdx=ngbIdxoftIdxPair.Value;
            if(readData.TryGetValue(ngbIdx,out Dictionary<Vector3Int,(double density,MaterialId materialId)>ngbEdits)){
             foreach(var vCoordEditPair in ngbEdits){var vCoord=vCoordEditPair.Key;var voxelData=vCoordEditPair.Value;
              neighbors[oftIdx][GetvxlIdx(vCoord.x,vCoord.y,vCoord.z)]=new Voxel(voxelData.density,Vector3.zero,voxelData.materialId);
             }
            }
           }
          }
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
            int oftIdx2=-1;
            int vxlIdx2=-1;
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
             oftIdx2=GetoftIdx(cCoord2-container.cCoord);
             vxlIdx2=GetvxlIdx(vCoord2.x,vCoord2.y,vCoord2.z);
             if(oftIdx2==0&&voxels[vxlIdx2].IsCreated){
              polygonCell[corner]=voxels[vxlIdx2];
             }else if(oftIdx2>0&&neighbors[oftIdx2-1].ContainsKey(vxlIdx2)){
              polygonCell[corner]=neighbors[oftIdx2-1][vxlIdx2];
             }else{
              //  pegar valor do bioma:
              Vector3Int noiseInput=vCoord2;noiseInput.x+=cnkRgn2.x;
                                            noiseInput.z+=cnkRgn2.y;
              VoxelSystem.biome.Setvxl(noiseInput,noiseForHeightCache,materialIdPerHeightNoiseCache,oftIdx2,vCoord2.z+vCoord2.x*Depth,ref polygonCell[corner]);
             }
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
              Vector2Int cnkRgn3=cnkRgn2;
              Vector2Int cCoord3=cCoord2;
              if(vCoord3.y<=0){
               tmpvxl[tmpIdx]=Voxel.Bedrock;
              }else if(vCoord3.y>=Height){
               tmpvxl[tmpIdx]=Voxel.Air;
              }else{
               if(vCoord3.x<0||vCoord3.x>=Width||
                  vCoord3.z<0||vCoord3.z>=Depth
               ){
                ValidateCoord(ref cnkRgn3,ref vCoord3);
                cCoord3=cnkRgnTocCoord(cnkRgn3);
               }
               int oftIdx3=GetoftIdx(cCoord3-container.cCoord);
               int vxlIdx3=GetvxlIdx(vCoord3.x,vCoord3.y,vCoord3.z);
               if(oftIdx3==0&&voxels[vxlIdx3].IsCreated){
                tmpvxl[tmpIdx]=voxels[vxlIdx3];
               }else if(oftIdx3>0&&neighbors[oftIdx3-1].ContainsKey(vxlIdx3)){
                tmpvxl[tmpIdx]=neighbors[oftIdx3-1][vxlIdx3];
               }else{
                //  pegar valor do bioma:
                Vector3Int noiseInput=vCoord3;noiseInput.x+=cnkRgn3.x;
                                              noiseInput.z+=cnkRgn3.y;
                VoxelSystem.biome.Setvxl(noiseInput,noiseForHeightCache,materialIdPerHeightNoiseCache,oftIdx3,vCoord3.z+vCoord3.x*Depth,ref tmpvxl[tmpIdx]);
                if(oftIdx3==0){
                 voxels[vxlIdx3]=tmpvxl[tmpIdx];
                }else if(oftIdx3>0){
                 neighbors[oftIdx3-1][vxlIdx3]=tmpvxl[tmpIdx];
                }
               }
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
            if(oftIdx2>=0&&vxlIdx2>=0){
             if(oftIdx2==0){
              voxels[vxlIdx2]=polygonCell[corner];
             }else if(oftIdx2>0){
              neighbors[oftIdx2-1][vxlIdx2]=polygonCell[corner];
             }//  :salvar valor construído
            }
            if(cache2){
             voxelsCache2[0][0]=polygonCell[corner];
             voxelsCache2[1][vCoord2.z]=polygonCell[corner];
             voxelsCache2[2][vCoord2.z+vCoord2.x*Depth]=polygonCell[corner];
            }
           }
           MarchingCubes(polygonCell,vCoord1,vertices,verticesCache,materials,normals,density,vertex,material,distance,idx,verPos,posOffset,ref vertexCount,container.TempVer,container.TempTri,vertexUV);
          }}}
          for(int i=0;i<container.TempVer.Length/3;i++){
           idx[0]=i*3;
           idx[1]=i*3+1;
           idx[2]=i*3+2;
           for(int j=0;j<3;j++){
            var vertexUVList=vertexUV[verPos[j]=container.TempVer[idx[j]].pos];
            vertexUVCounted.Clear();
            foreach(var uv in vertexUVList){
             if(!vertexUVCounted.ContainsKey(uv)){
              vertexUVCounted.Add(uv,1);
             }else{
              vertexUVCounted[uv]++;
             }
            }
            vertexUVSorted.Clear();
            foreach(var kvp in vertexUVCounted){
             vertexUVSorted.Add((kvp.Value,kvp.Key.x,kvp.Key.y),kvp.Key);
            }
            weights.Clear();
            int total=0;
            Vector2 uv0=container.TempVer[idx[j]].texCoord0;
            foreach(var materialId in vertexUVSorted){
             Vector2 uv=materialId.Value;
             bool add;
             if(uv0==uv){
              total+=weights[0]=materialId.Key.Item1;
             }else if(((add=container.TempVer[idx[j]].texCoord1==emptyUV)&&container.TempVer[idx[j]].texCoord2!=uv&&container.TempVer[idx[j]].texCoord3!=uv)||container.TempVer[idx[j]].texCoord1==uv){
              if(add){
               var v1=container.TempVer[idx[0]];v1.texCoord1=uv;container.TempVer[idx[0]]=v1;
                   v1=container.TempVer[idx[1]];v1.texCoord1=uv;container.TempVer[idx[1]]=v1;
                   v1=container.TempVer[idx[2]];v1.texCoord1=uv;container.TempVer[idx[2]]=v1;
              }
              total+=weights[1]=materialId.Key.Item1;
             }else if(((add=container.TempVer[idx[j]].texCoord2==emptyUV)&&container.TempVer[idx[j]].texCoord3!=uv                                         )||container.TempVer[idx[j]].texCoord2==uv){
              if(add){
               var v1=container.TempVer[idx[0]];v1.texCoord2=uv;container.TempVer[idx[0]]=v1;
                   v1=container.TempVer[idx[1]];v1.texCoord2=uv;container.TempVer[idx[1]]=v1;
                   v1=container.TempVer[idx[2]];v1.texCoord2=uv;container.TempVer[idx[2]]=v1;
              }
              total+=weights[2]=materialId.Key.Item1;
             }else if(((add=container.TempVer[idx[j]].texCoord3==emptyUV)                                                                                  )||container.TempVer[idx[j]].texCoord3==uv){
              if(add){
               var v1=container.TempVer[idx[0]];v1.texCoord3=uv;container.TempVer[idx[0]]=v1;
                   v1=container.TempVer[idx[1]];v1.texCoord3=uv;container.TempVer[idx[1]]=v1;
                   v1=container.TempVer[idx[2]];v1.texCoord3=uv;container.TempVer[idx[2]]=v1;
              }
              total+=weights[3]=materialId.Key.Item1;
             }
            }
            if(weights.Count>1){
             var v2=container.TempVer[idx[j]];
             Color col=v2.color;
                                        col.r=(weights[0]/(float)total);
             if(weights.ContainsKey(1)){col.g=(weights[1]/(float)total);}
             if(weights.ContainsKey(2)){col.b=(weights[2]/(float)total);}
             if(weights.ContainsKey(3)){col.a=(weights[3]/(float)total);}
             v2.color=col;
             container.TempVer[idx[j]]=v2;
            }
           }
          }
         }
        }
        #if UNITY_EDITOR
            void OnDrawGizmos(){
             //Logger.DrawBounds(worldBounds,Color.gray);
            }
        #endif
    }
}