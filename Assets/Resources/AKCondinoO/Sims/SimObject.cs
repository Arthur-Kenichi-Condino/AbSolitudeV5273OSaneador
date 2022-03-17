#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using AKCondinoO.Sims.Actors;
using AKCondinoO.Voxels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static AKCondinoO.Sims.SimObjectSpawner;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO.Sims{
    internal class SimObject:MonoBehaviour{
        internal readonly SimObjectSpawner.SimObjectSync synchronizer=new SimObjectSpawner.SimObjectSync();
        internal(Type simType,ulong number)?id=null;
        internal static readonly CultureInfo en=CultureInfo.GetCultureInfo("en");
        internal PersistentData persistentData;
        internal struct PersistentData{
         public Quaternion rotation;
         public Vector3    position;
         public Vector3    localScale;
         internal void UpdateData(SimObject sO){
          rotation=sO.transform.rotation;
          position=sO.transform.position;
          localScale=sO.transform.localScale;
          if(SimObjectSpawner.Singleton!=null){
           SimObjectSpawner.Singleton.OnPersistentDataUpdated(sO);
          }
         }
         public override string ToString(){
          return string.Format(en,"persistentData={{ position={0}, rotation={1}, localScale={2}, }}",position,rotation,localScale);
         }
         internal static PersistentData Parse(string s){
          PersistentData persistentData=new PersistentData();
          int positionStringStart=s.IndexOf("position=(");
          if(positionStringStart>=0){
           positionStringStart+=10;
           int positionStringEnd=s.IndexOf("), ",positionStringStart);
           string positionString=s.Substring(positionStringStart,positionStringEnd-positionStringStart);
           string[]xyzString=positionString.Split(',');
           float x=float.Parse(xyzString[0].Replace(" ","").Replace(".",","));
           float y=float.Parse(xyzString[1].Replace(" ","").Replace(".",","));
           float z=float.Parse(xyzString[2].Replace(" ","").Replace(".",","));
           persistentData.position=new Vector3(x,y,z);
          }
          int rotationStringStart=s.IndexOf("rotation=(");
          if(rotationStringStart>=0){
           rotationStringStart+=10;
           int rotationStringEnd=s.IndexOf("), ",rotationStringStart);
           string rotationString=s.Substring(rotationStringStart,rotationStringEnd-rotationStringStart);
           string[]xyzwString=rotationString.Split(',');
           float x=float.Parse(xyzwString[0].Replace(" ","").Replace(".",","));
           float y=float.Parse(xyzwString[1].Replace(" ","").Replace(".",","));
           float z=float.Parse(xyzwString[2].Replace(" ","").Replace(".",","));
           float w=float.Parse(xyzwString[3].Replace(" ","").Replace(".",","));
           persistentData.rotation=new Quaternion(x,y,z,w);
          }
          int localScaleStringStart=s.IndexOf("localScale=(");
          if(localScaleStringStart>=0){
           localScaleStringStart+=12;
           int localScaleStringEnd=s.IndexOf("), ",localScaleStringStart);
           string localScaleString=s.Substring(localScaleStringStart,localScaleStringEnd-localScaleStringStart);
           string[]xyzString=localScaleString.Split(',');
           float x=float.Parse(xyzString[0].Replace(" ","").Replace(".",","));
           float y=float.Parse(xyzString[1].Replace(" ","").Replace(".",","));
           float z=float.Parse(xyzString[2].Replace(" ","").Replace(".",","));
           persistentData.localScale=new Vector3(x,y,z);
          }
          return persistentData;
         }
        }
        internal Bounds localBounds;
         protected readonly Vector3[]worldBoundsVertices=new Vector3[8];
        internal Collider[]colliders;
        internal Renderer[]renderers;
        internal readonly List<Collider>volumeColliders=new List<Collider>();
        protected virtual void Awake(){
         foreach(Collider collider in colliders=GetComponentsInChildren<Collider>()){
          if(collider.CompareTag("SimObjectVolume")){
           if(localBounds.extents==Vector3.zero){
            localBounds=collider.bounds;
           }else{
            localBounds.Encapsulate(collider.bounds);
           }
           volumeColliders.Add(collider);
          }
         }
         localBounds.center=transform.InverseTransformPoint(localBounds.center);
         renderers=GetComponentsInChildren<Renderer>();
        }
        internal LinkedListNode<SimObject>pooled;       
        internal virtual void OnActivated(){
         EnableInteractions();
         TransformBoundsVertices();
         safePosition=transform.position;
         safeRotation=transform.rotation;
         safeScale=transform.localScale;
         transform.hasChanged=false;
        }
        internal void OnUnplaceRequest(){
         spawnerUnplaceRequest=true;
        }
        internal void OnPoolRequest(){
         spawnerPoolRequest=true;
        }
        internal void OnExitSave(){
         if(this!=null){
          persistentData.UpdateData(this);
         }
        }
        void OnDestroy(){
         if(id!=null){
          persistentData.UpdateData(this);
         }
        }
        Vector3    safePosition;
        Quaternion safeRotation;
        Vector3    safeScale;
        bool isOverlapping;
        bool spawnerUnplaceRequest;
        bool spawnerPoolRequest;
        internal virtual void ManualUpdate(){
         //Logger.Debug("ManualUpdate():"+id);
         if(transform.hasChanged){
          persistentData.UpdateData(this);
          TransformBoundsVertices();
         }
         if(spawnerUnplaceRequest){
            spawnerUnplaceRequest=false;
             DisableInteractions();
             SimObjectSpawner.Singleton.DespawnReleaseIdQueue.Enqueue(this);
         }else if(isOverlapping){
                  isOverlapping=false;
             Logger.Debug("is overlapping");
             DisableInteractions();
             SimObjectSpawner.Singleton.DespawnReleaseIdQueue.Enqueue(this);
         }else{
          if(spawnerPoolRequest){
             spawnerPoolRequest=false;
              DisableInteractions();
              SimObjectSpawner.Singleton.DespawnQueue.Enqueue(this);
          }else if((transform.hasChanged||SimObjectSpawner.Singleton.anyPlayerBoundsChanged)&&
            worldBoundsVertices.Any(
             v=>{
              Vector2Int cCoord=vecPosTocCoord(v);
              int cnkIdx=GetcnkIdx(cCoord.x,cCoord.y);
              return!VoxelSystem.Singleton.terrainActive.TryGetValue(cnkIdx,out VoxelTerrain cnk)||!cnk.hasPhysics;
             }
            )
           ){
              transform.position=safePosition;
              transform.rotation=safeRotation;
              transform.localScale=safeScale;
              persistentData.UpdateData(this);
              TransformBoundsVertices();
              DisableInteractions();
              SimObjectSpawner.Singleton.DespawnQueue.Enqueue(this);
           }
         }
         safePosition=transform.position;
         safeRotation=transform.rotation;
         safeScale=transform.localScale;
         transform.hasChanged=false;
        }
        internal bool interactionsEnabled;
        void DisableInteractions(){
         interactionsEnabled=false;
         foreach(Collider collider in colliders){
          collider.enabled=false;
         }
         foreach(Renderer renderer in renderers){
          renderer.enabled=false;
         }
        }
        void EnableInteractions(){
         interactionsEnabled=true;
         foreach(Collider collider in colliders){
          collider.enabled=true;
         }
         foreach(Renderer renderer in renderers){
          renderer.enabled=true;
         }
         isOverlapping=IsOverlappingNonAlloc();
        }
        void TransformBoundsVertices(){
         worldBoundsVertices[0]=transform.TransformPoint(localBounds.min.x,localBounds.min.y,localBounds.min.z);
         worldBoundsVertices[1]=transform.TransformPoint(localBounds.max.x,localBounds.min.y,localBounds.min.z);
         worldBoundsVertices[2]=transform.TransformPoint(localBounds.max.x,localBounds.min.y,localBounds.max.z);
         worldBoundsVertices[3]=transform.TransformPoint(localBounds.min.x,localBounds.min.y,localBounds.max.z);
         worldBoundsVertices[4]=transform.TransformPoint(localBounds.min.x,localBounds.max.y,localBounds.min.z);
         worldBoundsVertices[5]=transform.TransformPoint(localBounds.max.x,localBounds.max.y,localBounds.min.z);
         worldBoundsVertices[6]=transform.TransformPoint(localBounds.max.x,localBounds.max.y,localBounds.max.z);
         worldBoundsVertices[7]=transform.TransformPoint(localBounds.min.x,localBounds.max.y,localBounds.max.z);
        }
        Collider[]overlappedColliders=new Collider[8];
        bool IsOverlappingNonAlloc(){
         bool result=false;
         for(int i=0;i<volumeColliders.Count;++i){
          int overlappingsLength=0;
          if(volumeColliders[i]is CapsuleCollider capsule){
           var direction=new Vector3{[capsule.direction]=1};
           var offset=capsule.height/2-capsule.radius;
           var localPoint0=capsule.center-direction*offset;
           var localPoint1=capsule.center+direction*offset;
           var point0=transform.TransformPoint(localPoint0);
           var point1=transform.TransformPoint(localPoint1);
           while(overlappedColliders.Length<=(overlappingsLength=Physics.OverlapCapsuleNonAlloc(point0,point1,capsule.radius,overlappedColliders))&&overlappingsLength>0){
            Array.Resize(ref overlappedColliders,overlappingsLength*2);
           }
           for(int j=0;j<overlappingsLength;++j){var overlapping=overlappedColliders[j];
            if(overlapping.transform.root!=transform.root){//  it's not myself
             SimObject sO;
             if((sO=overlapping.transform.root.GetComponent<SimObject>())!=null&&!(sO is SimActor)){
              result=true;
             }
            }
           }
          }
         }
         return result;
        }
        #if UNITY_EDITOR
        void OnDrawGizmos(){
         //Logger.DrawRotatedBounds(worldBoundsVertices,Color.white);
        }
        #endif
    }
}
