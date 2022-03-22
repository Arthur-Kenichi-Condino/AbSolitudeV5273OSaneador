#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO.Sims{
    internal class Floor10x10x10:SimFloor{
     protected override void SnapPrecedence(ref SimConstruction toSnap,SimConstruction otherConstruction){
      if(otherConstruction is Floor10x10x10 otherFloor10x10x10){
       if(toSnap==null){
        base.SnapPrecedence(ref toSnap,otherConstruction);
       }
      }
     }
     protected override void SnapTo(SimConstruction otherConstruction){
      if(otherConstruction is Floor10x10x10 otherFloor10x10x10){
       Vector3 snapPos;
       Vector3 closestSnapPos=snapPos=GetSnapPos(otherFloor10x10x10.transform.forward);
       if(Vector3.Distance(transform.position,closestSnapPos)>Vector3.Distance(transform.position,snapPos=GetSnapPos(-otherFloor10x10x10.transform.forward))){
        closestSnapPos=snapPos;
       }
       if(Vector3.Distance(transform.position,closestSnapPos)>Vector3.Distance(transform.position,snapPos=GetSnapPos(otherFloor10x10x10.transform.right))){
        closestSnapPos=snapPos;
       }
       if(Vector3.Distance(transform.position,closestSnapPos)>Vector3.Distance(transform.position,snapPos=GetSnapPos(-otherFloor10x10x10.transform.right))){
        closestSnapPos=snapPos;
       }
       transform.position=closestSnapPos;
       transform.rotation=otherConstruction.transform.rotation;
       Logger.Debug("snapped");
       Vector3 GetSnapPos(Vector3 dir){
        return otherFloor10x10x10.transform.position+(dir*10f);
       }
      }
     }
    }
}