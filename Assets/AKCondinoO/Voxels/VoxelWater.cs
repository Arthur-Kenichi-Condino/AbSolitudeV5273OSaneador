#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO.Voxels{
    internal class VoxelWater:MonoBehaviour{
        internal readonly object syn=new object(); 
        internal readonly ConcurrentDictionary<int,WaterVoxel>voxels=new ConcurrentDictionary<int,WaterVoxel>();
         internal readonly ConcurrentDictionary<Vector3Int,double>absorbing=new ConcurrentDictionary<Vector3Int,double>();
         internal readonly ConcurrentDictionary<Vector3Int,double>spreading=new ConcurrentDictionary<Vector3Int,double>();
        internal Vector2Int cCoord;
        internal Vector2Int cnkRgn;
        internal        int cnkIdx;
        void Awake(){
         flowingBG=new WaterMarchingCubesBackgroundContainer(this);
        }
        internal WaterMarchingCubesBackgroundContainer flowingBG;
        internal class WaterMarchingCubesBackgroundContainer:BackgroundContainer{
         internal readonly VoxelWater water;
         internal bool firstCall=true;
         internal Vector2Int cCoord;
         internal Vector2Int cnkRgn;
         internal        int cnkIdx;
         internal int result=0;
         internal WaterMarchingCubesBackgroundContainer(VoxelWater water){
          this.water=water;
         }
         [StructLayout(LayoutKind.Sequential)]internal struct Vertex{
          internal Vector3 pos;
          internal Vector3 normal;
          internal Vertex(Vector3 p,Vector3 n){
           pos=p;
           normal=n;
          }
         }
         internal NativeList<Vertex>TempVer;
         internal NativeList<UInt32>TempTri;
        }
        internal class WaterMarchingCubesMultithreaded:BaseMultithreaded<WaterMarchingCubesBackgroundContainer>{
         internal WaterMarchingCubesMultithreaded(){
         }
         protected override void Cleanup(){
         }
         protected override void Execute(){
          Logger.Debug("water flow Execute");
          if(container.firstCall||container.cnkIdx!=container.water.cnkIdx){
           lock(container.water.syn){
            container.firstCall=false;
            container.cCoord=container.water.cCoord;
            container.cnkRgn=container.water.cnkRgn;
            container.cnkIdx=container.water.cnkIdx;
            container.water.voxels.Clear();
            container.water.absorbing.Clear();
            container.water.spreading.Clear();
           }
          }
          Vector3Int vCoord1;
          for(vCoord1=new Vector3Int();vCoord1.y<Height;vCoord1.y++){
          for(vCoord1.x=0             ;vCoord1.x<Width ;vCoord1.x++){
          for(vCoord1.z=0             ;vCoord1.z<Depth ;vCoord1.z++){
           Vector3Int vCoord2=vCoord1;
           int vxlIdx2=GetvxlIdx(vCoord2.x,vCoord2.y,vCoord2.z); 
           if(container.water.voxels.TryGetValue(vxlIdx2,out WaterVoxel vxl2)){
            if(vxl2.Absorbing>0d){
             // TO DO: remover água
            }
            if(!vxl2.Sleeping){
             container.water.spreading.AddOrUpdate(vCoord2,vxl2.Density,
              (key,oldValue)=>{
               return Math.Max(oldValue,vxl2.Density);
              }
             );
             WaterVoxel newValue=vxl2;
                        newValue.Sleeping=true;
             container.water.voxels.TryUpdate(vxlIdx2,newValue,vxl2);
            }
            if(vxl2.Density<=0d){//  remove
             // TO DO: limpar dicionário
            }
           }
          }}}
          foreach(var voxel in container.water.absorbing){
          } 
          foreach(var voxel in container.water.spreading){
          }
         }
        }
    }
}