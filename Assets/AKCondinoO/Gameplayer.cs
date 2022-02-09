using AKCondinoO.Sims;
using AKCondinoO.Voxels;
using UnityEngine;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO{
    internal class Gameplayer:MonoBehaviour{
        internal Vector2Int cCoord,cCoord_Pre;
        internal Vector2Int cnkRgn;
        internal Bounds worldBounds;
        void Awake(){
         cCoord_Pre=cCoord=vecPosTocCoord(transform.position);
                    cnkRgn=cCoordTocnkRgn(cCoord);
         worldBounds=new Bounds(Vector3.zero,
          new Vector3(
           (instantiationDistance.x*2+1)*Width,
           Height,
           (instantiationDistance.y*2+1)*Depth
          )
         );
         worldBounds.center=new Vector3(cnkRgn.x,0,cnkRgn.y);
         VoxelSystem.Singleton.generationStarters.Add(this);
        }
        [SerializeField]float reloadInterval=5f;
         float reloadTimer=0f;
        bool pendingCoordinatesUpdate=true;
        void Update(){
         transform.position=Camera.main.transform.position;
         if(transform.hasChanged){
            transform.hasChanged=false;
          pendingCoordinatesUpdate=true;
         }
         if(pendingCoordinatesUpdate){
            pendingCoordinatesUpdate=false;
          cCoord_Pre=cCoord;
          cCoord=vecPosTocCoord(transform.position);
          if(cCoord!=cCoord_Pre){
           cnkRgn=cCoordTocnkRgn(cCoord);
           VoxelSystem.Singleton.generationStarters.Add(this);
           worldBounds.center=new Vector3(cnkRgn.x,0,cnkRgn.y);
           SimObjectSpawner.Singleton.OnGameplayerWorldBoundsChange(this);
          }
         }
         if(reloadTimer>0f){
            reloadTimer-=Time.deltaTime;
         }else{
            reloadTimer=reloadInterval;
          SimObjectSpawner.Singleton.OnGameplayerLoadRequest(this);
         }
        }
        #if UNITY_EDITOR
        void OnDrawGizmos(){
         Logger.DrawBounds(worldBounds,Color.white);
        }
        #endif
    }
}