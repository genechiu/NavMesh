using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class GpuAnimExporter:Editor {
	
    [MenuItem("Assets/Export Actor",false,0)]
	public static void ExportSelections(){
		foreach(var selection in Selection.objects){
			if(selection is DefaultAsset){
				var path=AssetDatabase.GetAssetPath(selection);
				var prefab=LoadPrefabOrFBX(path+"/"+selection.name);
				if(prefab.transform.childCount>0){
					ExportAnimation(prefab);
				}
			}
		}
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	public static void ExportAnimation(GameObject prefab){
		var prefabPath=AssetDatabase.GetAssetPath(prefab);
		var rawFolderPath=Path.GetDirectoryName(prefabPath);
		var outFolderPath=rawFolderPath.Replace("Actors","Resources/Actors");
		var materialsFolderPath=outFolderPath+"/Materials";
		if(!Directory.Exists(materialsFolderPath)){
			Directory.CreateDirectory(materialsFolderPath);
		}
		
		var animData=ScriptableObject.CreateInstance<GpuAnimData>();
		var gameObject=GameObject.Instantiate<GameObject>(prefab);
		var children=gameObject.transform.GetChild(0).GetComponentsInChildren<Transform>();
		var indexMap=new Dictionary<string,int>();
		var boneCount=children.Length;
		var bonePoses=new Matrix4x4[boneCount];
		var bindPoses=new Matrix4x4[boneCount];
		var bones=new string[boneCount];
		animData.bones=bones;
		for(var i=0;i<boneCount;i++){
			var child=children[i];
			indexMap.Add(child.name,i);
			bonePoses[i]=child.transform.localToWorldMatrix;
			bindPoses[i]=child.transform.worldToLocalMatrix;
			bones[i]=child.name;
		}
		var widgetPaths=new HashSet<string>();
		var animationNames=new HashSet<string>();
		var animationClips=new List<AnimationClip>();
		var animation=gameObject.GetComponent<Animation>();
		animationNames.Add(animation.clip.name);
		animationClips.Add(animation.clip);
		foreach(AnimationState animationState in animation){
			if(!animationNames.Contains(animationState.name)){
				animationNames.Add(animationState.name);
				animationClips.Add(animationState.clip);
			}
		}
		
		foreach(var rawFile in Directory.GetFiles(rawFolderPath)){
			if(rawFile.IndexOf('@')<0){
				if(AssetDatabase.LoadAssetAtPath<MeshRenderer>(rawFile)!=null){
					var path=rawFolderPath+"/"+Path.GetFileNameWithoutExtension(rawFile);
					if(!widgetPaths.Contains(path)){
						widgetPaths.Add(path);
					}
				}
			}
		}
		foreach(var path in widgetPaths){
			var linkPrefab=LoadPrefabOrFBX(path);
			if(linkPrefab!=null){
				var linkGameObject=GameObject.Instantiate<GameObject>(linkPrefab);
				var linkMeshFilter=linkGameObject.GetComponent<MeshFilter>();
				var linkMeshRenderer=linkGameObject.GetComponent<MeshRenderer>();
				var sharedMaterial=linkMeshRenderer.sharedMaterial;
				var newMaterial=new Material(Shader.Find("Toon/Default"));
				newMaterial.mainTexture=sharedMaterial.mainTexture;
				newMaterial.enableInstancing=true;
				linkMeshRenderer.sharedMaterial=newMaterial;
				var sharedMesh=linkMeshFilter.sharedMesh;
				var newMesh=new Mesh();
				newMesh.vertices=sharedMesh.vertices;
				newMesh.normals=sharedMesh.normals;
				newMesh.uv=sharedMesh.uv;
				newMesh.triangles=sharedMesh.triangles;
				AssetDatabase.CreateAsset(newMesh,materialsFolderPath+"/"+linkPrefab.name+".asset");
				AssetDatabase.CreateAsset(newMaterial,materialsFolderPath+"/"+linkPrefab.name+".mat");
				PrefabUtility.CreatePrefab(outFolderPath+"/"+linkPrefab.name+".prefab",linkGameObject);
				GameObject.DestroyImmediate(linkGameObject);
			}
		}

		var pixelStartIndex=0;
		var clipCount=animationClips.Count;
		var clips=new GpuAnimClip[clipCount];
		animData.clips=clips;
		for(var i=0;i<clipCount;i++){
			var animationClip=animationClips[i];
			var clip=new GpuAnimClip();
			clips[i]=clip;
			clip.name=animationClip.name;
			clip.frameRate=Mathf.RoundToInt(animationClip.frameRate);
			clip.frameCount=Mathf.RoundToInt(animationClip.length*clip.frameRate);
			if(animationClip.wrapMode==WrapMode.ClampForever){
				clip.isLooping=true;
				clip.loopStartFrame=clip.frameCount-1;
			}
			else if(animationClip.wrapMode==WrapMode.Loop){
				clip.isLooping=true;
			}
			clip.length=(float)clip.frameCount/clip.frameRate;
			clip.pixelStartIndex=pixelStartIndex;
			pixelStartIndex+=clip.frameCount*boneCount*3;
			var clipBones=new GpuAnimClip.Bone[boneCount];
			clip.bones=clipBones;
			for(var b=0;b<boneCount;b++){
				clipBones[b]=new GpuAnimClip.Bone();
				clipBones[b].frames=new Matrix4x4[clip.frameCount];
			}
		}
		var textureSize=2;
		while(textureSize*textureSize<pixelStartIndex){
			textureSize=textureSize<<1;
		}
		var texture=new Texture2D(textureSize,textureSize,TextureFormat.RGBAHalf,false,true);
		texture.filterMode=FilterMode.Point;
		var pixelIndex=0;
		var pixels=texture.GetPixels();
		var matrix=Matrix4x4.identity;
		
		for(var c=0;c<clipCount;c++){
			var clip=clips[c];
			var animationClip=animationClips[c];
			var curveBindings=AnimationUtility.GetCurveBindings(animationClip);
			var positionPathHash=new HashSet<string>();
			var rotationPathHash=new HashSet<string>();
			foreach(var curveBinding in curveBindings){
				var path=curveBinding.path;
				var propertyName=curveBinding.propertyName;
				if(propertyName.Length==17){
					var propertyPrefix=propertyName.Substring(0,15);
					if(propertyPrefix=="m_LocalPosition"){
						if(!positionPathHash.Contains(path)){
							positionPathHash.Add(path);
						}
					}
					else if(propertyPrefix=="m_LocalRotation"){
						if(!rotationPathHash.Contains(path)){
							rotationPathHash.Add(path);
						}
					}
				}
			}

			for(var f=0;f<clip.frameCount;f++){
				var time=(float)f/clip.frameRate;
				
				foreach(var path in positionPathHash){
					var boneName=path.Substring(path.LastIndexOf('/')+1);
					if(indexMap.ContainsKey(boneName)){
						var child=children[indexMap[boneName]];
						var positionX=GetCurveValue(animationClip,path,"m_LocalPosition.x",time);
						var positionY=GetCurveValue(animationClip,path,"m_LocalPosition.y",time);
						var positionZ=GetCurveValue(animationClip,path,"m_LocalPosition.z",time);
						child.localPosition=new Vector3(positionX,positionY,positionZ);
					}
				}
				
				foreach(var path in rotationPathHash){
					var boneName=path.Substring(path.LastIndexOf('/')+1);
					if(indexMap.ContainsKey(boneName)){
						var child=children[indexMap[boneName]];
						var rotationX=GetCurveValue(animationClip,path,"m_LocalRotation.x",time);
						var rotationY=GetCurveValue(animationClip,path,"m_LocalRotation.y",time);
						var rotationZ=GetCurveValue(animationClip,path,"m_LocalRotation.z",time);
						var rotationW=GetCurveValue(animationClip,path,"m_LocalRotation.w",time);
						var rotation=new Quaternion(rotationX,rotationY,rotationZ,rotationW);
						var r=rotation.x*rotation.x;
						r+=rotation.y*rotation.y;
						r+=rotation.z*rotation.z;
						r+=rotation.w*rotation.w;
						if(r>0.1f){
							r=1.0f/Mathf.Sqrt(r);
							rotation.x*=r;
							rotation.y*=r;
							rotation.z*=r;
							rotation.w*=r;
						}
						child.localRotation=rotation;
					}
				}
				for(var b=0;b<boneCount;b++){
					matrix=children[b].transform.localToWorldMatrix;
					clip.bones[b].frames[f]=matrix;
					matrix=matrix*bindPoses[b];
					pixels[pixelIndex++]=new Color(matrix.m00,matrix.m01,matrix.m02,matrix.m03);
					pixels[pixelIndex++]=new Color(matrix.m10,matrix.m11,matrix.m12,matrix.m13);
					pixels[pixelIndex++]=new Color(matrix.m20,matrix.m21,matrix.m22,matrix.m23);
				}
			}
		}
		
		texture.SetPixels(pixels);
		texture.Apply();
		AssetDatabase.CreateAsset(texture,materialsFolderPath+"/"+prefab.name+"_skinning.asset");
		AssetDatabase.CreateAsset(animData,materialsFolderPath+"/"+prefab.name+"_data.asset");
		
		var anim=new GameObject().AddComponent<GpuAnim>();
		anim.data=animData;
		anim.clipName=animation.clip.name;
		anim.time=0f;
		anim.speed=1f;
		anim.paused=false;
		
		var meshMap=new Dictionary<Mesh,Mesh>();
		var materialMap=new Dictionary<Material,Material>();
		var skinnedMeshRenderers=gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
		for(var p=0;p<skinnedMeshRenderers.Length;p++){
			var skinnedMeshRenderer=skinnedMeshRenderers[p];
			var sharedMesh=skinnedMeshRenderer.sharedMesh;
			Mesh newMesh;
			if(meshMap.ContainsKey(sharedMesh)){
				newMesh=meshMap[sharedMesh];
			}
			else{
				var transforms=skinnedMeshRenderer.bones;
				var vertexCount=sharedMesh.vertexCount;
				var indices=new List<Vector4>();
				var weights=new List<Vector4>();
				var vertices=new Vector3[vertexCount];
				var normals=new Vector3[vertexCount];
				var meshMatrix=bonePoses[indexMap[transforms[0].name]]*sharedMesh.bindposes[0];
				var boneWeights=sharedMesh.boneWeights;
				for(var v=0;v<vertexCount;v++){
					var weight=boneWeights[v];
					var weight0=weight.weight0;
					var weight1=weight.weight1;
					var weight2=weight.weight2;
					var weight3=weight.weight3;
					var boneIndex0=indexMap[transforms[weight.boneIndex0].name];
					var boneIndex1=indexMap[transforms[weight.boneIndex1].name];
					var boneIndex2=indexMap[transforms[weight.boneIndex2].name];
					var boneIndex3=indexMap[transforms[weight.boneIndex3].name];
					indices.Add(new Vector4(boneIndex0,boneIndex1,boneIndex2,boneIndex3));
					weights.Add(new Vector4(weight0,weight1,weight2,weight3));
					vertices[v]=meshMatrix*sharedMesh.vertices[v];
					normals[v]=meshMatrix*sharedMesh.normals[v];
					weight.boneIndex0=boneIndex0;
					weight.boneIndex1=boneIndex1;
					weight.boneIndex2=boneIndex2;
					weight.boneIndex3=boneIndex3;
					boneWeights[v]=weight;
				}
				newMesh=new Mesh();
				newMesh.vertices=vertices;
				newMesh.normals=normals;
				newMesh.triangles=sharedMesh.triangles;
				newMesh.uv=sharedMesh.uv;
				newMesh.SetUVs(1,indices);
				newMesh.SetUVs(2,weights);
				skinnedMeshRenderer.bones=children;
				skinnedMeshRenderer.sharedMesh=newMesh;
				AssetDatabase.CreateAsset(newMesh,materialsFolderPath+"/"+sharedMesh.name+".asset");
				meshMap.Add(sharedMesh,newMesh);
			}

			var sharedMaterial=skinnedMeshRenderer.sharedMaterial;
			Material newMaterial;
			if(materialMap.ContainsKey(sharedMaterial)){
				newMaterial=materialMap[sharedMaterial];
			}
			else{
				newMaterial=new Material(Shader.Find("Toon/Animation"));
				newMaterial.mainTexture=sharedMaterial.mainTexture;
				newMaterial.SetTexture("_SkinningTex",texture);
				newMaterial.SetFloat("_SkinningTexSize",textureSize);
				newMaterial.enableInstancing=true;
				AssetDatabase.CreateAsset(newMaterial,materialsFolderPath+"/"+sharedMaterial.name+".mat");
				materialMap.Add(sharedMaterial,newMaterial);
			}
			
			var partGameObject=new GameObject(skinnedMeshRenderer.name);
			var meshFilter=partGameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh=newMesh;
			var meshRenderer=partGameObject.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial=newMaterial;
			meshRenderer.lightProbeUsage=skinnedMeshRenderer.lightProbeUsage;
			meshRenderer.reflectionProbeUsage=skinnedMeshRenderer.reflectionProbeUsage;
			meshRenderer.shadowCastingMode=skinnedMeshRenderer.shadowCastingMode;
			meshRenderer.receiveShadows=skinnedMeshRenderer.receiveShadows;
			PrefabUtility.CreatePrefab(outFolderPath+"/"+skinnedMeshRenderer.name+".prefab",partGameObject);
			GameObject.DestroyImmediate(partGameObject);
		}
		
		PrefabUtility.CreatePrefab(outFolderPath+"/"+prefab.name+".prefab",anim.gameObject);
		GameObject.DestroyImmediate(anim.gameObject);
		GameObject.DestroyImmediate(gameObject);
	}
	
	private static float GetCurveValue(AnimationClip clip,string path,string prop,float time){
		var binding=EditorCurveBinding.FloatCurve(path,typeof(Transform),prop);
		return AnimationUtility.GetEditorCurve(clip,binding).Evaluate(time);
	}
	
	public static GameObject LoadPrefabOrFBX(string path){
		var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(string.Format("{0}.prefab",path));
		if(prefab==null){
			prefab=AssetDatabase.LoadAssetAtPath<GameObject>(string.Format("{0}.FBX",path));
		}
		return prefab;
	}
}
