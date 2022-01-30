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
        internal void OnPooling(){
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
        [Serializable]internal struct SerializableTransform{
         public SerializableQuaternion rotation;
         public SerializableVector3    position;
         public SerializableVector3    localScale;
        }
    }
}
