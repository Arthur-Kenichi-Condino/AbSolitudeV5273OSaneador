using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class SimObject:MonoBehaviour{
        internal readonly object synchronizer=new object();
        internal PersistentData persistentData;
        internal struct PersistentData{
         public Quaternion rotation;
         public Vector3    position;
         public Vector3    localScale;
         public void UpdateData(SimObject sO){
          rotation=sO.transform.rotation;
          position=sO.transform.position;
          localScale=sO.transform.localScale;
          if(SimObjectSpawner.Singleton!=null){
           SimObjectSpawner.Singleton.persistentDataCache[sO.id.Value]=this;
          }
         }
         static readonly CultureInfo en=CultureInfo.GetCultureInfo("en");
         public override string ToString(){
          return string.Format(en,"persistentData={{ position={0}, }}",position);
         }
         public static PersistentData Parse(string s){
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
          return persistentData;
         }
        }
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
         if(this!=null){
          persistentData.UpdateData(this);
         }
        }
        void OnDestroy(){
         if(id!=null){
          persistentData.UpdateData(this);
         }
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
    }
}
