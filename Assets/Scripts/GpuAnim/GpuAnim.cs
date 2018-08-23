using UnityEngine;
using System.Collections.Generic;
public class GpuAnim:MonoBehaviour{
	
	public GpuAnimData data;
	public string clipName;
	public float time;
	public float speed;
	public bool paused;
	
	private int propertyStartPixelIndexID;
	private MaterialPropertyBlock propertyBlock;
	
	private int boneCount;
	private Dictionary<string,int> boneMap;
	
	private GpuAnimClip defaultClip;
	private Dictionary<string,GpuAnimClip> clipMap;
	
	void Awake(){
		propertyStartPixelIndexID=Shader.PropertyToID("_PixelStartIndex");
		propertyBlock=new MaterialPropertyBlock();
		boneCount=data.bones.Length;
		boneMap=new Dictionary<string,int>();
		for(int i=0;i<boneCount;i++){
			boneMap.Add(data.bones[i],i);
		}
		defaultClip=data.clips[0];
		clipMap=new Dictionary<string,GpuAnimClip>();
		foreach(GpuAnimClip clip in data.clips){
			clipMap.Add(clip.name,clip);
		}
	}
	
	void Update(){
		if(!paused){
			time+=Time.deltaTime*speed;
			Refresh();
		}
	}
	
	public void Play(string clipName,float time=0f){
		this.clipName=clipName;
		this.time=time;
		Refresh();
	}
	
	public void Refresh(){
		var clip=clipMap.ContainsKey(clipName)?clipMap[clipName]:defaultClip;
		var frame=(int)(time*clip.frameRate);
		if(frame>=clip.frameCount){
			frame-=clip.frameCount;
			if(!clip.isLooping){
				clip=defaultClip;
			}
			frame=(frame%(clip.frameCount-clip.loopStartFrame))+clip.loopStartFrame;
		}
		var pixelStartIndex=clip.pixelStartIndex+boneCount*3*frame;
		var childCount=transform.childCount;
		for(var i=0;i<childCount;i++){
			var boneTransform=transform.GetChild(i);
			var meshRenderer=boneTransform.GetComponent<MeshRenderer>();
			if(meshRenderer==null){
				if(boneMap.ContainsKey(boneTransform.name)&&boneTransform.childCount>0){
					var matrix=clip.bones[boneMap[boneTransform.name]].frames[frame];
					var forward=new Vector3(matrix.m02,matrix.m12,matrix.m22);
					var upwards=new Vector3(matrix.m01,matrix.m11,matrix.m21);
					boneTransform.localRotation=Quaternion.LookRotation(forward,upwards);
					boneTransform.localPosition=matrix.GetColumn(3);
				}
			}
			else{
				propertyBlock.SetFloat(propertyStartPixelIndexID,pixelStartIndex);
				meshRenderer.SetPropertyBlock(propertyBlock);
			}
		}
	}
}
