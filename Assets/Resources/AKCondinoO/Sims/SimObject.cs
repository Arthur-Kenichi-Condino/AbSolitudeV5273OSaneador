using System;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class SimObject:MonoBehaviour{
        internal readonly object syn=new object(); 
        protected virtual void Awake(){
        }
        internal LinkedListNode<SimObject>pooled;       
        internal virtual void OnActivated(bool load){
        }
        internal void OnPoolRequest(){
         poolRequested=true;
        }
        internal void OnExitSave(List<(Type simType,ulong number)>unplacedIds){
        }
        internal(Type simType,ulong number)?id=null;
        bool poolRequested;
        internal virtual void ManualUpdate(){
         //Logger.Debug("ManualUpdate():"+id);
         if(poolRequested){
            poolRequested=false;
             SimObjectSpawner.Singleton.DespawnQueue.Enqueue(this);
         }
        }
        internal struct PersistentData{
         public Quaternion rotation;
         public Vector3    position;
         public Vector3    localScale;
        }
    }
}
