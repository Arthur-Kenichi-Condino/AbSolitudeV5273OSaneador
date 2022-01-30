using AKCondinoO.Voxels;
using UnityEngine;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO{
    internal class Gameplayer:MonoBehaviour{
        internal Vector2Int cCoord,cCoord_Pre;
        void Awake(){
         cCoord_Pre=cCoord=vecPosTocCoord(transform.position);
         VoxelSystem.Singleton.generationStarters.Add(this);
        }
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
           VoxelSystem.Singleton.generationStarters.Add(this);
          }
         }
        }
    }
}