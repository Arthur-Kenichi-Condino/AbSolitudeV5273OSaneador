#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class SimConstruction:SimObject{
        [SerializeField]Collider snapper;
        RaycastHit[]snappingRaycastHits=new RaycastHit[8];
        internal override void ManualUpdate(){
         bool snap=(!SimObjectSpawner.Singleton.disableSnappingToSlots&&transform.hasChanged);
         base.ManualUpdate();
         if(snap){
          if(snapper is BoxCollider boxSnapper){
           SimConstruction snapped;
           if((snapped=TrySnap(Vector3.forward))!=null||
              (snapped=TrySnap(Vector3.back   ))!=null||
              (snapped=TrySnap(Vector3.right  ))!=null||
              (snapped=TrySnap(Vector3.left   ))!=null){
            SnapTo(snapped);
           }
           SimConstruction TrySnap(Vector3 dir){
            int raycastHitsLength=0;
            while(snappingRaycastHits.Length<=(raycastHitsLength=Physics.BoxCastNonAlloc(transform.position+boxSnapper.center,boxSnapper.bounds.extents,transform.rotation*dir,snappingRaycastHits,transform.rotation,1f))&&raycastHitsLength>0){
             Array.Resize(ref snappingRaycastHits,raycastHitsLength*2);
            }
            SimConstruction sC=null;
            for(int j=0;j<raycastHitsLength;++j){var raycastHit=snappingRaycastHits[j];
             if(raycastHit.transform.root!=transform.root){//  it's not myself
              SnapPrecedence(ref sC,raycastHit.transform.root.GetComponent<SimConstruction>());
             }
            }
            return sC;
           }
          }
         }
        }
        protected virtual void SnapPrecedence(ref SimConstruction toSnap,SimConstruction otherConstruction){
         if(otherConstruction!=null){
          toSnap=otherConstruction;
         }
        }
        protected virtual void SnapTo(SimConstruction otherConstruction){
        }
        #if UNITY_EDITOR
        protected override void OnDrawGizmos(){
         DrawColliders();
        }
        void DrawColliders(){
         if(colliders!=null){
          foreach(Collider collider in colliders){
           if(collider.CompareTag("SimObjectVolume")){
            if(collider is BoxCollider box){
             Gizmos.color=Color.gray;
             Gizmos.matrix=Matrix4x4.TRS(transform.position+box.center,transform.rotation,transform.lossyScale);
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