using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO{
    internal class MainCamera:MonoBehaviour{internal static MainCamera Singleton;
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         QualitySettings.vSyncCount=0;
         Application.targetFrameRate=200;
         Camera.main.transparencySortMode=TransparencySortMode.Perspective;
         tgtRot=tgtRot_Pre=transform.eulerAngles;
         tgtPos=tgtPos_Pre=transform.position;
         safePosition=transform.position;
        }
        Vector3 safePosition;
        Vector3 tgtRot,tgtRot_Pre;
         float tgtRotLerpTime;
          float tgtRotLerpMaxTime=.025f;
           float tgtRotLerpVal;
            float tgtRotLerpSpeed=18.75f;
             Quaternion tgtRotLerpA,tgtRotLerpB;
              Vector3 inputViewRotationEuler;
               [SerializeField]float ViewRotationSmoothValue=.025f;
        Vector3 tgtPos,tgtPos_Pre;
         float tgtPosLerpTime;
          float tgtPosLerpMaxTime=.05f;
           float tgtPosLerpVal;
            float tgtPosLerpSpeed=25f;
             Vector3 tgtPosLerpA,tgtPosLerpB;
              Vector3 inputMoveSpeed;
               [SerializeField]Vector3 MoveAcceleration=new Vector3(.02f,.02f,.02f);
                [SerializeField]Vector3 MaxMoveSpeed=new Vector3(.2f,.2f,.2f);
        // Update is called once per frame
        void Update(){
         if(!(bool)Enabled.PAUSE[0]){
          inputViewRotationEuler.x+=-Enabled.MOUSE_ROTATION_DELTA_Y[0]*ViewRotationSmoothValue;
          inputViewRotationEuler.y+= Enabled.MOUSE_ROTATION_DELTA_X[0]*ViewRotationSmoothValue;
          inputViewRotationEuler.x=inputViewRotationEuler.x%360;
          inputViewRotationEuler.y=inputViewRotationEuler.y%360;
          if(inputViewRotationEuler!=Vector3.zero){
           tgtRot+=inputViewRotationEuler;
           inputViewRotationEuler=Vector3.zero;
          }
          if(tgtRotLerpTime==0){
           if(tgtRot!=tgtRot_Pre){
            tgtRotLerpVal=0;
            tgtRotLerpA=transform.rotation;
            tgtRotLerpB=Quaternion.Euler(tgtRot);
            tgtRotLerpTime+=Time.deltaTime;
            tgtRot_Pre=tgtRot;
           }
          }else{
           tgtRotLerpTime+=Time.deltaTime;
          }
          if(tgtRotLerpTime!=0){
           tgtRotLerpVal+=tgtRotLerpSpeed*Time.deltaTime;
           if(tgtRotLerpVal>=1){
            tgtRotLerpVal=1;
            tgtRotLerpTime=0;
           }
           transform.rotation=Quaternion.Lerp(tgtRotLerpA,tgtRotLerpB,tgtRotLerpVal);
           transform.hasChanged=false;//  transform has changed until here, but flag as false so any changes not made by this Update are detected below
           if(tgtRotLerpTime>tgtRotLerpMaxTime){
            if(tgtRot!=tgtRot_Pre){
             tgtRotLerpTime=0;
            }
           }
          }
          if((bool)Enabled.FORWARD [0]){inputMoveSpeed.z+=MoveAcceleration.z;} 
          if((bool)Enabled.BACKWARD[0]){inputMoveSpeed.z-=MoveAcceleration.z;}
           if(!(bool)Enabled.FORWARD[0]&&!(bool)Enabled.BACKWARD[0]){inputMoveSpeed.z=0;}
            if( inputMoveSpeed.z>MaxMoveSpeed.z){inputMoveSpeed.z= MaxMoveSpeed.z;}
            if(-inputMoveSpeed.z>MaxMoveSpeed.z){inputMoveSpeed.z=-MaxMoveSpeed.z;}
          if((bool)Enabled.RIGHT   [0]){inputMoveSpeed.x+=MoveAcceleration.x;} 
          if((bool)Enabled.LEFT    [0]){inputMoveSpeed.x-=MoveAcceleration.x;}
           if(!(bool)Enabled.RIGHT[0]&&!(bool)Enabled.LEFT[0]){inputMoveSpeed.x=0;}
            if( inputMoveSpeed.x>MaxMoveSpeed.x){inputMoveSpeed.x= MaxMoveSpeed.x;}
            if(-inputMoveSpeed.x>MaxMoveSpeed.x){inputMoveSpeed.x=-MaxMoveSpeed.x;}
          if(inputMoveSpeed!=Vector3.zero){
           tgtPos+=transform.rotation*(inputMoveSpeed/Mathf.Max(1f,(inputMoveSpeed.z!=0?1f:0f)+(inputMoveSpeed.x!=0?1f:0f)+(inputMoveSpeed.y!=0?1f:0f)));
          }
          if(tgtPosLerpTime==0){
           if(tgtPos!=tgtPos_Pre){
            tgtPosLerpVal=0;
            tgtPosLerpA=transform.position;
            tgtPosLerpB=tgtPos;
            tgtPosLerpTime+=Time.deltaTime;
            tgtPos_Pre=tgtPos;
           }
          }else{
           tgtPosLerpTime+=Time.deltaTime;
          }
          if(tgtPosLerpTime!=0){
           tgtPosLerpVal+=tgtPosLerpSpeed*Time.deltaTime;
           if(tgtPosLerpVal>=1){
            tgtPosLerpVal=1;
            tgtPosLerpTime=0;
           }
           transform.position=Vector3.Lerp(tgtPosLerpA,tgtPosLerpB,tgtPosLerpVal);
           transform.hasChanged=false;//  transform has changed until here, but flag as false so any changes not made by this Update are detected below
           if(tgtPosLerpTime>tgtPosLerpMaxTime){
            if(tgtPos!=tgtPos_Pre){
             tgtPosLerpTime=0;
            }
           }
          }
         }else{
          inputViewRotationEuler=Vector3.zero;
          inputMoveSpeed=Vector3.zero;
         }
         Vector2Int cCoord=vecPosTocCoord(transform.position);
         if(Math.Abs(cCoord.x)>=MaxcCoordx||
            Math.Abs(cCoord.y)>=MaxcCoordy){
          transform.position=tgtPos=tgtPos_Pre=safePosition;
         }
         if(transform.hasChanged){
          Logger.Debug("camera transform changed outside Update");
          tgtRot=tgtRot_Pre=transform.eulerAngles;
          tgtPos=tgtPos_Pre=transform.position;
          transform.hasChanged=false;
         }
         safePosition=transform.position;
        }
    }
}