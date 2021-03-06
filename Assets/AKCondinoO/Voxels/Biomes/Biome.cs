using AKCondinoO.Sims;
using LibNoise;
using LibNoise.Generator;
using LibNoise.Operator;
using System;
using System.Collections.Generic;
using UnityEngine;
using static AKCondinoO.Voxels.VoxelSystem;
namespace AKCondinoO.Voxels.Biomes{
    internal class Biome{
     protected readonly System.Random[]random=new System.Random[2];
     int seed_v;
     internal int Seed{
      get{return seed_v;}
      set{       seed_v=value;
       random[0]=new System.Random(seed_v);
       random[1]=new System.Random(random[0].Next());
       SetModules();
      }
     }
     internal void DisposeModules(){
      foreach(var module in modules){
       module.Dispose();
      }
      modules.Clear();
     }
     protected virtual int rndIdx{get{return 1;}}
     protected readonly List<ModuleBase>modules=new List<ModuleBase>();
     protected virtual void SetModules(){
      modules.Add(new Const( 0));
      modules.Add(new Const( 1));
      modules.Add(new Const(-1));
      modules.Add(new Const(.5));
      modules.Add(new Const(128));
      ModuleBase module1=new Const(5);
       // 2
       ModuleBase module2a=new RidgedMultifractal(frequency:Mathf.Pow(2,-8),lacunarity:2.0,octaves:6,seed:random[rndIdx].Next(),quality:QualityMode.Low);
       ModuleBase module2b=new Turbulence(input:module2a); 
       ((Turbulence)module2b).Seed=random[rndIdx].Next();
       ((Turbulence)module2b).Frequency=Mathf.Pow(2,-2);
       ((Turbulence)module2b).Power=1;
       ModuleBase module2c=new ScaleBias(scale:1.0,bias:30.0,input:module2b);  
        // 3
        ModuleBase module3a=new Billow(frequency:Mathf.Pow(2,-7)*1.6,lacunarity:2.0,persistence:0.5,octaves:8,seed:random[rndIdx].Next(),quality:QualityMode.Low);
        ModuleBase module3b=new Turbulence(input:module3a);
        ((Turbulence)module3b).Seed=random[rndIdx].Next();
        ((Turbulence)module3b).Frequency=Mathf.Pow(2,-2);  
        ((Turbulence)module3b).Power=1.8;
        ModuleBase module3c=new ScaleBias(scale:1.0,bias:31.0,input:module3b);
         // 4
         ModuleBase module4a=new Perlin(frequency:Mathf.Pow(2,-6),lacunarity:2.0,persistence:0.5,octaves:6,seed:random[rndIdx].Next(),quality:QualityMode.Low);
         ModuleBase module4b=new Select(inputA:module2c,inputB:module3c,controller:module4a);
         ((Select)module4b).SetBounds(min:-.2,max:.2);
         ((Select)module4b).FallOff=.25;
         ModuleBase module4c=new Multiply(lhs:module4b,rhs:module1);
      modules.Add(module4c);
      selectors[0]=(Select)module4b;
      modules.Add(simTypeSpawnChancePerlin=new Perlin(frequency:Mathf.Pow(2,-2),lacunarity:2.0,persistence:0.5,octaves:6,seed:seed_v,quality:QualityMode.Low));
     }
     readonly protected Select[]selectors=new Select[1];
     protected virtual int SelectAt(Vector3 noiseInput){
      double min=selectors[0].Minimum;
      double max=selectors[0].Maximum;
      double fallOff=selectors[0].FallOff*.5;
      var selectValue=selectors[0].Controller.GetValue(noiseInput.z,noiseInput.x,0);
      if(selectValue<=min-fallOff||selectValue>=max+fallOff){
       return 1;
      }else{
       return 0;
      }
     }
     protected virtual int hgtIdx1{get{return 5;}}//  Base Height Result Module
     internal virtual int heightsCacheLength{get{return 1;}}
     protected Vector3 deround{get;}=new Vector3(.5f,.5f,.5f);
     internal void Setvxl(Vector3Int noiseInputRounded,double[][][]noiseForHeightCache,MaterialId[][][]materialIdPerHeightNoiseCache,int oftIdx,int noiseIndex,ref Voxel vxl){
                  Vector3 noiseInput=noiseInputRounded+deround;
      if(noiseForHeightCache!=null&&noiseForHeightCache[0][oftIdx]==null)noiseForHeightCache[0][oftIdx]=new double[FlattenOffset];
      double noiseValue=(noiseForHeightCache!=null&&noiseForHeightCache[0][oftIdx][noiseIndex]!=0)?
       noiseForHeightCache[0][oftIdx][noiseIndex]:
        (noiseForHeightCache!=null?
         (noiseForHeightCache[0][oftIdx][noiseIndex]=Noise()):
          Noise());
      double Noise(){return modules[hgtIdx1].GetValue(noiseInput.z,noiseInput.x,0);}
      if(materialIdPerHeightNoiseCache!=null&&materialIdPerHeightNoiseCache[0][oftIdx]==null)materialIdPerHeightNoiseCache[0][oftIdx]=new MaterialId[FlattenOffset];
      if(noiseInput.y<=noiseValue){
       double d;
       vxl=new Voxel(d=Density(100,noiseInput,noiseValue),Vector3.zero,Material(d,noiseInput,materialIdPerHeightNoiseCache,oftIdx,noiseIndex));
       return;
      }
      vxl=Voxel.Air;
     }
     protected virtual double Density(double density,Vector3 noiseInput,double noiseValue,float smoothing=3f){
      double value=density;
      double delta=noiseValue-noiseInput.y;//  noiseInput.y sempre ser? menor ou igual a noiseValue
      if(delta<=smoothing){
       double smoothingValue=(smoothing-delta)/smoothing;
       value*=1d-smoothingValue;
       if(value<0)
          value=0;
       else if(value>100)
               value=100;
      }
      return value;
     }
     readonly protected MaterialId[]materialIdPicking=new MaterialId[2]{
      MaterialId.Rock,
      MaterialId.Dirt,
     };
     protected virtual MaterialId Material(double density,Vector3 noiseInput,MaterialId[][][]materialIdPerHeightNoiseCache,int oftIdx,int noiseIndex){
      if(-density>=IsoLevel){
       return MaterialId.Air;
      }
      if(materialIdPerHeightNoiseCache!=null&&materialIdPerHeightNoiseCache[0][oftIdx][noiseIndex]!=0){
       return materialIdPerHeightNoiseCache[0][oftIdx][noiseIndex];
      }
      MaterialId m;
      m=materialIdPicking[SelectAt(noiseInput)];
      return materialIdPerHeightNoiseCache!=null?materialIdPerHeightNoiseCache[0][oftIdx][noiseIndex]=m:m;
     }
     internal struct SimTypeSpawnSettings{
      internal float chance;
      internal float verticalRotationFactor;
      internal Vector3 minScale;
      internal Vector3 maxScale;
      internal float rootsDepth;
      internal Vector3 spacing;
      internal Vector3 spacingAll;
     }
     readonly protected Dictionary<Type,SimTypeSpawnSettings[]>simTypeSpawnSettings=new Dictionary<Type,SimTypeSpawnSettings[]>(){
      {
       typeof(Pinus_elliottii_1),
       new SimTypeSpawnSettings[]{
        new SimTypeSpawnSettings{
         chance=.125f,
         verticalRotationFactor=.125f,
         minScale=Vector3.one*.5f,
         maxScale=Vector3.one*1.5f,
         rootsDepth=1.2f,
         spacing=Vector3.one*4.8f,
         spacingAll=Vector3.one*2.4f,
        },
       }
      },
     };
     readonly protected Dictionary<int,Type[]>simTypePicking=new Dictionary<int,Type[]>{
      {
       1,
       new Type[]{
        typeof(Pinus_elliottii_1),
       }
      },
     };
     protected Perlin simTypeSpawnChancePerlin;
     internal(Type simType,SimTypeSpawnSettings simTypeSpawnSettings)?SimType(Vector3Int noiseInputRounded){
                                                                      Vector3 noiseInput=noiseInputRounded+deround;
      if(simTypePicking.TryGetValue(SelectAt(noiseInput),out Type[]simTypesPicked)){
       foreach(Type simType in simTypesPicked){SimTypeSpawnSettings simTypeSpawnSettings=this.simTypeSpawnSettings[simType][0];
        float chance=simTypeSpawnSettings.chance/simTypesPicked.Length;
        float dicing=((float)simTypeSpawnChancePerlin.GetValue(noiseInput.z,noiseInput.x,0)+1f)/2f;
        if(dicing<=chance){
         return(simType,simTypeSpawnSettings);
        }
       }
      }
      return null;
     }
     internal struct SimTypeSpawnModifiers{
      internal float rotation;
      internal Vector3 scale;
     }
     internal virtual SimTypeSpawnModifiers SpawnModifiers(Vector3Int noiseInputRounded,SimTypeSpawnSettings spawnSettings,Perlin rotationModifierPerlin,Perlin scaleModifierPerlin){
                                                   Vector3 noiseInput=noiseInputRounded+deround;
      float rotation=(float)rotationModifierPerlin.GetValue(noiseInput.z,noiseInput.x,0)*720f;
      Vector3 scale=Vector3.Lerp(spawnSettings.minScale,spawnSettings.maxScale,Mathf.Clamp01(((float)scaleModifierPerlin.GetValue(noiseInput.z,noiseInput.x,0)+1f)/2f));
      return new SimTypeSpawnModifiers{
       rotation=rotation,
       scale=scale,
      };
     }
    }
}