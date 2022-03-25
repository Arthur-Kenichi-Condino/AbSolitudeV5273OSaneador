#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class SimConstruction:SimObject{
        protected override void Awake(){
         base.Awake();
        }
        [SerializeField]Collider snapper;
        float snapDelay=0.25f;
        float snapTimer=0f;
        RaycastHit[]snappingRaycastHits=new RaycastHit[8];
        internal override void ManualUpdate(){         
         bool snap=false;
         if(!SimObjectSpawner.Singleton.disableSnappingToSlots&&transform.hasChanged){
          snapTimer=snapDelay;
         }else if(snapTimer>0f){
          snapTimer-=Time.deltaTime;
          if(snapTimer<=0f){
           snap=true;
          }
         }
         base.ManualUpdate();
         if(interactionsEnabled){
          if(snap){
           if(snapper is BoxCollider boxSnapper){
            SimConstruction snapped;
            if((snapped=TrySnap(Vector3.forward,null   ))!=null|
               (snapped=TrySnap(Vector3.back   ,snapped))!=null|
               (snapped=TrySnap(Vector3.right  ,snapped))!=null|
               (snapped=TrySnap(Vector3.left   ,snapped))!=null){
             SnapTo(snapped);
            }
            SimConstruction TrySnap(Vector3 dir,SimConstruction snapped){
             int raycastHitsLength=0;
             while(snappingRaycastHits.Length<=(raycastHitsLength=Physics.BoxCastNonAlloc(transform.position+boxSnapper.center,boxSnapper.bounds.extents-(Vector3.one*0.0005f),transform.rotation*dir,snappingRaycastHits,transform.rotation,1f))&&raycastHitsLength>0){
              Array.Resize(ref snappingRaycastHits,raycastHitsLength*2);
             }
             SimConstruction sC=snapped;
             for(int j=0;j<raycastHitsLength;++j){var raycastHit=snappingRaycastHits[j];
              if(raycastHit.transform.root!=transform.root){//  it's not myself
               SnapPrecedence(ref sC,raycastHit.transform.root.GetComponent<SimConstruction>());
              }
             }
             return sC;
            }
           }
          }
          nonOverlappingPosition=transform.position;
          nonOverlappingRotation=transform.rotation;
          nonOverlappingScale=transform.localScale;
         }
        }
        protected virtual void SnapPrecedence(ref SimConstruction toSnap,SimConstruction otherConstruction){
         if(otherConstruction!=null){
          toSnap=otherConstruction;
         }
        }
        Collider[]snappingOverlappedColliders=new Collider[8];
        protected virtual void SnapTo(SimConstruction otherConstruction){
         for(int i=0;i<volumeColliders.Count;++i){
          int overlappingsLength=0;
          if(volumeColliders[i]is CapsuleCollider capsule){
          }else if(volumeColliders[i]is BoxCollider box){
           while(snappingOverlappedColliders.Length<=(overlappingsLength=Physics.OverlapBoxNonAlloc(transform.position+box.center,box.bounds.extents-(Vector3.one*0.0005f),snappingOverlappedColliders,transform.rotation))&&overlappingsLength>0){
            Array.Resize(ref snappingOverlappedColliders,overlappingsLength*2);
           }
          }
         }
        }
        #if UNITY_EDITOR
        protected override void OnDrawGizmos(){
         DrawColliders();
        }
        void DrawColliders(){
         if(colliders!=null&&interactionsEnabled){
          foreach(Collider collider in colliders){
           if(collider.CompareTag("SimObjectVolume")){
            if(collider is BoxCollider box){
             Gizmos.color=Color.gray;
             Gizmos.matrix=Matrix4x4.TRS(transform.position+box.center,transform.rotation,transform.localScale);
             Gizmos.DrawCube(Vector3.zero,box.size);
            }
           }
          }
          Gizmos.matrix=Matrix4x4.identity;
         }
        }
        #endif
    }
}