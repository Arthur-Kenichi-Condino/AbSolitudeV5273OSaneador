using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace AKCondinoO{
    internal static class Command{
     internal enum Modes{holdDelayAfterInRange,holdDelay,activeHeld,alternateDown,whenUp,}
     internal static float ROTATION_SENSITIVITY_X=360.0f;
     internal static float ROTATION_SENSITIVITY_Y=360.0f;
     internal static object[]PAUSE={KeyCode.Tab,Modes.alternateDown};
     internal static object[]FORWARD ={KeyCode.W,Modes.activeHeld};
     internal static object[]BACKWARD={KeyCode.S,Modes.activeHeld};
     internal static object[]RIGHT   ={KeyCode.D,Modes.activeHeld};
     internal static object[]LEFT    ={KeyCode.A,Modes.activeHeld};
    }
    internal static class Enabled{
     internal static readonly float[]MOUSE_ROTATION_DELTA_X={0,0};
     internal static readonly float[]MOUSE_ROTATION_DELTA_Y={0,0};
     internal static readonly object[]PAUSE={true,true};
     internal static readonly object[]FORWARD ={false,false};
     internal static readonly object[]BACKWARD={false,false};
     internal static readonly object[]RIGHT   ={false,false};
     internal static readonly object[]LEFT    ={false,false};
    }
    internal class InputHandler:MonoBehaviour{internal static InputHandler Singleton;
        #pragma warning disable IDE0051 //  Ignore "remover membros privados não utilizados"
        bool Get(Func<KeyCode,bool>  keyboardGet,KeyCode   key){return   keyboardGet(   key);}readonly Func<KeyCode,bool>[]  keyboardGets=new Func<KeyCode,bool>[3]{Input.GetKey        ,Input.GetKeyUp        ,Input.GetKeyDown        ,};
        bool Get(Func<int    ,bool>     mouseGet,int    button){return      mouseGet(button);}readonly Func<int    ,bool>[]     mouseGets=new Func<int    ,bool>[3]{Input.GetMouseButton,Input.GetMouseButtonUp,Input.GetMouseButtonDown,};
        bool Get(Func<string ,bool>controllerGet,string button){return controllerGet(button);}readonly Func<string ,bool>[]controllerGets=new Func<string ,bool>[3]{Input.GetButton     ,Input.GetButtonUp     ,Input.GetButtonDown     ,};
        #pragma warning restore IDE0051 
        readonly Dictionary<Type,Delegate>GetsDelegates=new Dictionary<Type,Delegate>();
         readonly Dictionary<Type,object[]>Gets=new Dictionary<Type,object[]>();
        internal readonly Dictionary<string,object[]>CommandDictionary=new Dictionary<string,object[]>();
        internal readonly Dictionary<string,object[]>EnabledDictionary=new Dictionary<string,object[]>();
        void Awake(){if(Singleton==null){Singleton=this;}else{DestroyImmediate(this);return;}
         foreach(MethodInfo method in GetType().GetMethods(BindingFlags.NonPublic|BindingFlags.Instance)){
          if(method.Name=="Get"){
           var inputType=method.GetParameters()[1].ParameterType;
           Delegate result;
           if(inputType==typeof(KeyCode))result=method.CreateDelegate(typeof(Func<Func<KeyCode,bool>,KeyCode,bool>),this);else
           if(inputType==typeof(int    ))result=method.CreateDelegate(typeof(Func<Func<int    ,bool>,int    ,bool>),this);else
                                         result=method.CreateDelegate(typeof(Func<Func<string ,bool>,string ,bool>),this);
           GetsDelegates[inputType]=result;
          }
         }
         Gets.Add(typeof(KeyCode),  keyboardGets);
         Gets.Add(typeof(int    ),     mouseGets);
         Gets.Add(typeof(string ),controllerGets);
         foreach(FieldInfo field in typeof(Command).GetFields(BindingFlags.NonPublic|BindingFlags.Static)){
          if(field.GetValue(null)is object[]command){
           CommandDictionary.Add(field.Name,command);
          }
         }
         foreach(FieldInfo field in typeof(Enabled).GetFields(BindingFlags.NonPublic|BindingFlags.Static)){
          if(field.GetValue(null)is object[]enabled){
           EnabledDictionary.Add(field.Name,enabled);
          }
         }
        }
        void OnDestroy(){
         if(Singleton==this){Singleton=null;}
        }
        internal bool Focus=true;
        void OnApplicationFocus(bool focus){
         Focus=focus;
        }
        internal bool Escape;
        void Update(){
         Escape=Input.GetKey(KeyCode.Escape)||Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyUp(KeyCode.Escape);
         foreach(var command in CommandDictionary){
          string        name=command.Key;
          Type          type=command.Value[0].GetType();
          Command.Modes mode=(Command.Modes)command.Value[1];
          object[]enabled=EnabledDictionary[name];
          enabled[1]=enabled[0];
           if(mode==Command.Modes.holdDelayAfterInRange){
            #region holdDelayAfterInRange
            enabled[0]=false;
            if((bool)command.Value[3]&&GetsDelegatesInvoke(0)){
             float heldTime=(float)enabled[2];
                   heldTime+=Time.deltaTime;
             if(heldTime>=(float)command.Value[2]){
              heldTime=0;
              enabled[0]=true;
             }
             enabled[2]=heldTime;
            }else{
             enabled[2]=0f;
            }
            command.Value[3]=false;
            #endregion
           }else if(mode==Command.Modes.holdDelay){
            #region holdDelay
            enabled[0]=false;
            if(GetsDelegatesInvoke(0)){
             float heldTime=(float)enabled[2];
                   heldTime+=Time.deltaTime;
             if(heldTime>=(float)command.Value[2]){
              heldTime=0;
              enabled[0]=true;
             }
             enabled[2]=heldTime;
            }else{
             enabled[2]=0f;
            }
            #endregion
           }else if(mode==Command.Modes.activeHeld){
            #region activeHeld
            enabled[0]=GetsDelegatesInvoke(0);
            #endregion
           }else if(mode==Command.Modes.alternateDown){
            #region alternateDown
            if(GetsDelegatesInvoke(2)){
             enabled[0]=!(bool)enabled[0];
            }
            #endregion
           }
           bool GetsDelegatesInvoke(int getsType){
            if(type==typeof(KeyCode))return((Func<Func<KeyCode,bool>,KeyCode,bool>)GetsDelegates[type]).Invoke((Func<KeyCode,bool>)Gets[type][getsType],(KeyCode)command.Value[0]);else
            if(type==typeof(int    ))return((Func<Func<int    ,bool>,int    ,bool>)GetsDelegates[type]).Invoke((Func<int    ,bool>)Gets[type][getsType],(int    )command.Value[0]);else
                                     return((Func<Func<string ,bool>,string ,bool>)GetsDelegates[type]).Invoke((Func<string ,bool>)Gets[type][getsType],(string )command.Value[0]);
           }
         }
         Enabled.PAUSE[0]=(bool)Enabled.PAUSE[0]||Escape||!Focus;
         if((bool)Enabled.PAUSE[0]!=(bool)Enabled.PAUSE[1]){
          if((bool)Enabled.PAUSE[0]){
           Cursor.visible=true;
           Cursor.lockState=CursorLockMode.None;
          }else{
           Cursor.visible=false;
           Cursor.lockState=CursorLockMode.Locked;
          }
         }
         Enabled.MOUSE_ROTATION_DELTA_X[1]=Enabled.MOUSE_ROTATION_DELTA_X[0];Enabled.MOUSE_ROTATION_DELTA_X[0]=Command.ROTATION_SENSITIVITY_X*Input.GetAxis("Mouse X");
         Enabled.MOUSE_ROTATION_DELTA_Y[1]=Enabled.MOUSE_ROTATION_DELTA_Y[0];Enabled.MOUSE_ROTATION_DELTA_Y[0]=Command.ROTATION_SENSITIVITY_Y*Input.GetAxis("Mouse Y");
        }
    }
}