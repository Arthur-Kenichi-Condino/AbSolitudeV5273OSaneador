#if UNITY_EDITOR
    #define ENABLE_DEBUG_LOG
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AKCondinoO{
    internal class SimTime:MonoBehaviour{
     internal const int   _YEAR  =12;
     internal const int   _MONTH =28;
     internal       float _DAY      ;
     internal       float _HOUR     ;
     internal       float _MINUTE   ;
     internal const float _SECOND=1f;
     void Awake(){
      _MINUTE=60*_SECOND;
      _HOUR  =60*_MINUTE;
      _DAY   =24*_HOUR  ;
      simTimeOfDay=.5f*_DAY;
     }
     /// <summary>
     ///  Quantos minutos dura um ciclo dia-noite no jogo se UseRealTime==false
     /// </summary>
     [SerializeField]internal float simDayInRealMinutes=.1f;
     internal uint  simDay  =1;
     internal uint  simMonth=1;
     internal ulong simYear =1;
     internal float simTimeOfDay;//  Contagem de segundos total no dia
     internal float simDayOfYear;//  Contagem de dias total no ano
     internal float dayCourse;
     [SerializeField]internal Light mainSun;
      internal Vector3 mainSunEulerAngles=new Vector3(0f,-90f,0f);
       float mainSunLightIntensityClearDawn  =.5f;
       float mainSunLightIntensityClearDay   =1f ;
       float mainSunLightIntensityClearSunset=.5f;
       float mainSunLightIntensityClearNight =0f ;
        float mainSunLightIntensityLerp=0f;
     float skyThicknessClearDawn  =1.7f;
     float skyThicknessClearDay   =1f  ;
     float skyThicknessClearSunset=1.7f;
     float skyThicknessClearNight =1.7f;
      float skyThicknessLerp=0f;
     float ambientLightIntensityClearDawn  =.25f;
     float ambientLightIntensityClearDay   =1f  ;
     float ambientLightIntensityClearSunset=.25f;
     float ambientLightIntensityClearNight =0f  ;
      float ambientLightIntensityLerp=0f;
       float ambientReflectionsIntensityClearDawn  =.25f;
       float ambientReflectionsIntensityClearDay   =1f  ;
       float ambientReflectionsIntensityClearSunset=.25f;
       float ambientReflectionsIntensityClearNight =0f  ;
        float ambientReflectionsIntensityLerp=0f;
     void Update(){        
         simTimeOfDay+=Time.deltaTime*_DAY/(simDayInRealMinutes*_MINUTE);
      if(simTimeOfDay>=_DAY){
         simTimeOfDay-=_DAY;
          simDay++;
       if(simDay>_MONTH){
          simDay=1;
           simMonth++;
        if(simMonth>_YEAR){
           simMonth=1;
         simYear++;
         Logger.Debug("ano passou, SimYear:"+simYear);
        }
        Logger.Debug("m�s passou, SimMonth:"+simMonth);
       }
       Logger.Debug("dia passou, SimDay:"+simDay);
      }
      simDayOfYear=(simMonth-1)*_MONTH+simDay;
      dayCourse=(simTimeOfDay/_DAY);
      mainSunEulerAngles.x=dayCourse*360f-90f;
      mainSun.transform.rotation=Quaternion.Euler(mainSunEulerAngles);
      //  .5f: meio-dia
      //  .75f: 18 h
      //  0f: meia-noite
      //  .25f: 6 h
      #region skyThickness
       if(dayCourse>=0.75f||dayCourse<0.25f){
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",skyThicknessClearNight);
       }else if(dayCourse>=0.67f&&dayCourse<0.71f){
        float delta=0.71f-0.67f;
        skyThicknessLerp=(dayCourse-0.67f)/delta;
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",Mathf.Lerp(skyThicknessClearDay,skyThicknessClearSunset,skyThicknessLerp));
       }else if(dayCourse>=0.71f&&dayCourse<0.75f){
        float delta=0.75f-0.71f;
        skyThicknessLerp=(dayCourse-0.71f)/delta;
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",Mathf.Lerp(skyThicknessClearSunset,skyThicknessClearNight,skyThicknessLerp));
       }else if(dayCourse>=0.25f&&dayCourse<0.29f){
        float delta=0.29f-0.25f;
        skyThicknessLerp=(dayCourse-0.25f)/delta;
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",Mathf.Lerp(skyThicknessClearNight,skyThicknessClearDawn,skyThicknessLerp));
       }else if(dayCourse>=0.29f&&dayCourse<0.33f){
        float delta=0.33f-0.29f;
        skyThicknessLerp=(dayCourse-0.29f)/delta;
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",Mathf.Lerp(skyThicknessClearDawn,skyThicknessClearDay,skyThicknessLerp));
       }else{
        RenderSettings.skybox.SetFloat("_AtmosphereThickness",skyThicknessClearDay);
       }
      #endregion 
      #region ambientLightIntensity
       if(dayCourse>=0.75f||dayCourse<0.25f){
        RenderSettings.ambientIntensity=ambientLightIntensityClearNight;
       }else if(dayCourse>=0.67f&&dayCourse<0.71f){
        float delta=0.71f-0.67f;
        ambientLightIntensityLerp=(dayCourse-0.67f)/delta;
        RenderSettings.ambientIntensity=Mathf.Lerp(ambientLightIntensityClearDay,ambientLightIntensityClearSunset,ambientLightIntensityLerp);
       }else if(dayCourse>=0.71f&&dayCourse<0.75f){
        float delta=0.75f-0.71f;
        ambientLightIntensityLerp=(dayCourse-0.71f)/delta;
        RenderSettings.ambientIntensity=Mathf.Lerp(ambientLightIntensityClearSunset,ambientLightIntensityClearNight,ambientLightIntensityLerp);
       }else if(dayCourse>=0.25f&&dayCourse<0.29f){
        float delta=0.29f-0.25f;
        ambientLightIntensityLerp=(dayCourse-0.25f)/delta;
        RenderSettings.ambientIntensity=Mathf.Lerp(ambientLightIntensityClearNight,ambientLightIntensityClearDawn,ambientLightIntensityLerp);
       }else if(dayCourse>=0.29f&&dayCourse<0.33f){
        float delta=0.33f-0.29f;
        ambientLightIntensityLerp=(dayCourse-0.29f)/delta;
        RenderSettings.ambientIntensity=Mathf.Lerp(ambientLightIntensityClearDawn,ambientLightIntensityClearDay,ambientLightIntensityLerp);
       }else{
        RenderSettings.ambientIntensity=ambientLightIntensityClearDay;
       }
      #endregion 
      #region ambientReflectionsIntensity
       if(dayCourse>=0.75f||dayCourse<0.25f){
        RenderSettings.reflectionIntensity=ambientReflectionsIntensityClearNight;
       }else if(dayCourse>=0.67f&&dayCourse<0.71f){
        float delta=0.71f-0.67f;
        ambientReflectionsIntensityLerp=(dayCourse-0.67f)/delta;
        RenderSettings.reflectionIntensity=Mathf.Lerp(ambientReflectionsIntensityClearDay,ambientReflectionsIntensityClearSunset,ambientReflectionsIntensityLerp);
       }else if(dayCourse>=0.71f&&dayCourse<0.75f){
        float delta=0.75f-0.71f;
        ambientReflectionsIntensityLerp=(dayCourse-0.71f)/delta;
        RenderSettings.reflectionIntensity=Mathf.Lerp(ambientReflectionsIntensityClearSunset,ambientReflectionsIntensityClearNight,ambientReflectionsIntensityLerp);
       }else if(dayCourse>=0.25f&&dayCourse<0.29f){
        float delta=0.29f-0.25f;
        ambientReflectionsIntensityLerp=(dayCourse-0.25f)/delta;
        RenderSettings.reflectionIntensity=Mathf.Lerp(ambientReflectionsIntensityClearNight,ambientReflectionsIntensityClearDawn,ambientReflectionsIntensityLerp);
       }else if(dayCourse>=0.29f&&dayCourse<0.33f){
        float delta=0.33f-0.29f;
        ambientReflectionsIntensityLerp=(dayCourse-0.29f)/delta;
        RenderSettings.reflectionIntensity=Mathf.Lerp(ambientReflectionsIntensityClearDawn,ambientReflectionsIntensityClearDay,ambientReflectionsIntensityLerp);
       }else{
        RenderSettings.reflectionIntensity=ambientReflectionsIntensityClearDay;
       }
      #endregion 
      #region mainSunLightIntensity
       if(dayCourse>=0.75f||dayCourse<0.25f){
        mainSun.intensity=mainSunLightIntensityClearNight;
       }else if(dayCourse>=0.67f&&dayCourse<0.71f){
        float delta=0.71f-0.67f;
        mainSunLightIntensityLerp=(dayCourse-0.67f)/delta;
        mainSun.intensity=Mathf.Lerp(mainSunLightIntensityClearDay,mainSunLightIntensityClearSunset,mainSunLightIntensityLerp);
       }else if(dayCourse>=0.71f&&dayCourse<0.75f){
        float delta=0.75f-0.71f;
        mainSunLightIntensityLerp=(dayCourse-0.71f)/delta;
        mainSun.intensity=Mathf.Lerp(mainSunLightIntensityClearSunset,mainSunLightIntensityClearNight,mainSunLightIntensityLerp);
       }else if(dayCourse>=0.25f&&dayCourse<0.29f){
        float delta=0.29f-0.25f;
        mainSunLightIntensityLerp=(dayCourse-0.25f)/delta;
        mainSun.intensity=Mathf.Lerp(mainSunLightIntensityClearNight,mainSunLightIntensityClearDawn,mainSunLightIntensityLerp);
       }else if(dayCourse>=0.29f&&dayCourse<0.33f){
        float delta=0.33f-0.29f;
        mainSunLightIntensityLerp=(dayCourse-0.29f)/delta;
        mainSun.intensity=Mathf.Lerp(mainSunLightIntensityClearDawn,mainSunLightIntensityClearDay,mainSunLightIntensityLerp);
       }else{
        mainSun.intensity=mainSunLightIntensityClearDay;
       }
      #endregion 
     }
    }
}