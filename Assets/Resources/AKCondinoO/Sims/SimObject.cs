using System;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class SimObject:MonoBehaviour{
        internal readonly object synchronizer=new object();
        protected virtual void Awake(){
        }
        internal LinkedListNode<SimObject>pooled;       
        internal virtual void OnActivated(bool load){
        }
        internal void OnUnplaceRequest(){
         spawnerUnplaceRequest=true;
        }
        internal void OnPoolRequest(){
         spawnerPoolRequest=true;
        }
        internal void OnExitSave(List<(Type simType,ulong number)>unplacedIds){
        }
        internal(Type simType,ulong number)?id=null;
        bool spawnerUnplaceRequest;
        bool spawnerPoolRequest;
        internal virtual void ManualUpdate(){
         //Logger.Debug("ManualUpdate():"+id);
         if(spawnerUnplaceRequest){
            spawnerUnplaceRequest=false;
             spawnerPoolRequest=false;
             SimObjectSpawner.Singleton.DespawnReleaseIdQueue.Enqueue(this);
         }else{
          if(spawnerPoolRequest){
             spawnerPoolRequest=false;
              SimObjectSpawner.Singleton.DespawnQueue.Enqueue(this);
          }
         }
        }
        internal struct PersistentData{
         public Quaternion rotation;
         public Vector3    position;
         public Vector3    localScale;
         public override string ToString(){
          return string.Format("persistentData={{ position={0}, }}",position);
         }
        }
    }
}
