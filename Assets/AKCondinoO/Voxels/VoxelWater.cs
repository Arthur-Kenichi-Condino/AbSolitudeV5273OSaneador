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
        internal VoxelTerrain terrain;
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
          if      (container.result==2){
           container.result=1;
          }else if(container.result==1){
           container.result=0;
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
          if(container.water.absorbing.Count>0||
             container.water.spreading.Count>0
          ){
           container.result=2;
          }
          foreach(var voxel in container.water.absorbing){
          } 
          foreach(var voxel in container.water.spreading){
           Vector3Int vCoord2=voxel.Key;
           if(container.water.spreading.TryRemove(vCoord2,out double density)){
            Vector3Int d_vCoord=new Vector3Int(vCoord2.x,vCoord2.y-1,vCoord2.z);
            bool waterfall=VerticalSpread(d_vCoord,d_vCoord.y>=0);
            bool VerticalSpread(Vector3Int v_vCoord,bool insideAxisLength){
             if(insideAxisLength){
              int v_vxlIdx=GetvxlIdx(v_vCoord.x,v_vCoord.y,v_vCoord.z);
              return Spread(v_vxlIdx);
             }
             return false;
             bool Spread(int v_vxlIdx){
              bool spread=true;
              WaterVoxel newValue=new WaterVoxel(density,false,0d);
              if(newValue.Density<30d){
               return false;
              }
              container.water.voxels.AddOrUpdate(v_vxlIdx,newValue,
               (key,oldValue)=>{
                if(oldValue.Density>=newValue.Density){
                 return oldValue;
                }
                newValue.Absorbing=Math.Max(oldValue.Absorbing,newValue.Absorbing);
                return newValue;
               }
              );
              return spread;
             }
            }
            if(!waterfall){
             Vector3Int r_vCoord=new Vector3Int(vCoord2.x+1,vCoord2.y,vCoord2.z);HorizontalSpread(r_vCoord,r_vCoord.x<Width);
             Vector3Int l_vCoord=new Vector3Int(vCoord2.x-1,vCoord2.y,vCoord2.z);HorizontalSpread(l_vCoord,l_vCoord.x>=0   );
             Vector3Int f_vCoord=new Vector3Int(vCoord2.x,vCoord2.y,vCoord2.z+1);HorizontalSpread(f_vCoord,f_vCoord.z<Depth);
             Vector3Int b_vCoord=new Vector3Int(vCoord2.x,vCoord2.y,vCoord2.z-1);HorizontalSpread(b_vCoord,b_vCoord.z>=0   );
             bool HorizontalSpread(Vector3Int h_vCoord,bool insideAxisLength){
              if(insideAxisLength){
               int h_vxlIdx=GetvxlIdx(h_vCoord.x,h_vCoord.y,h_vCoord.z);
               return Spread(h_vxlIdx);
              }else{
               //  TO DO: passar pra outros chunks
               return true;
              }
              bool Spread(int h_vxlIdx){
               bool spread=true;
               WaterVoxel newValue=new WaterVoxel(density-5d,false,0d);
               if(newValue.Density<30d){
                return false;
               }
               container.water.voxels.AddOrUpdate(h_vxlIdx,newValue,
                (key,oldValue)=>{
                 if(oldValue.Density>=newValue.Density){
                  spread=false;
                  return oldValue;
                 }
                 newValue.Absorbing=Math.Max(oldValue.Absorbing,newValue.Absorbing);
                 return newValue;
                }
               );
               return spread;
              }
             }
            }
           }
          }
         }
        }
        #if UNITY_EDITOR
            void OnDrawGizmos(){
             DrawVoxelsDensity();
            }
            void DrawVoxelsDensity(){
             if(voxels.Count<=0){
              return;
             }
             Vector3Int vCoord1;
             for(vCoord1=new Vector3Int();vCoord1.y<Height;vCoord1.y++){
             for(vCoord1.x=0             ;vCoord1.x<Width ;vCoord1.x++){
             for(vCoord1.z=0             ;vCoord1.z<Depth ;vCoord1.z++){
              Vector3Int vCoord2=vCoord1;
              int vxlIdx2=GetvxlIdx(vCoord2.x,vCoord2.y,vCoord2.z);
              if(voxels.TryGetValue(vxlIdx2,out WaterVoxel voxel)){double density=voxel.Density;
               if(-density<IsoLevel){
                Gizmos.color=Color.white;
               }else{
                Gizmos.color=Color.black;
               }
               Gizmos.DrawCube(transform.position+vCoord2-trianglePosAdj-(Vector3.one*.5f),Vector3.one*(float)(density*.01d));
              }
             }}}
            }
        #endif
    }
}