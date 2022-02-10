using AKCondinoO.Sims;
using AKCondinoO.Voxels;
using UnityEngine;
using UnityEngine.AI;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO{
    internal class Gameplayer:MonoBehaviour{
        internal Vector2Int cCoord,cCoord_Pre;
        internal Vector2Int cnkRgn;
        internal Bounds worldBounds;
        //  Medium size agent: 0
        //  Small  size agent: 1
        //  Large  size agent: 2
        internal readonly NavMeshData[]navMeshData=new NavMeshData[3];
         internal readonly NavMeshDataInstance[]navMeshInstance=new NavMeshDataInstance[3];
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
         for(int agentType=0;agentType<Core.Singleton.navMeshBuildSettings.Length;++agentType){
          string[]navMeshValidation=Core.Singleton.navMeshBuildSettings[agentType].ValidationReport(worldBounds);
          foreach(string s in navMeshValidation){Logger.Error(s);}
          navMeshData[agentType]=new NavMeshData(agentType){
           hideFlags=HideFlags.None,
          };
          navMeshInstance[agentType]=NavMesh.AddNavMeshData(navMeshData[agentType]);
         }
         worldBounds.center=new Vector3(cnkRgn.x,0,cnkRgn.y);
         VoxelSystem.Singleton.generationStarters.Add(this);
        }
        void OnDestroy(){
         for(int agentType=0;agentType<navMeshData.Length;++agentType){
          if(navMeshData[agentType]!=null){
           NavMesh.RemoveNavMeshData(navMeshInstance[agentType]);
          }
         }
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