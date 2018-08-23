using System;
using System.Collections.Generic;
public struct NavMeshPoint{
	public NavMesh navigation;
	public int face;
	public float x;
	public float y;
	public float z;
	
	public NavMeshPoint(NavMesh navigation,int face,float x,float y,float z){
		this.navigation=navigation;
		this.face=face;
		this.x=x;
		this.y=y;
		this.z=z;
	}
	
	public UnityEngine.Vector3 ToVector3(){
		return new UnityEngine.Vector3(x,y,z);
	}
	
	public bool Walkable(NavMesh navigation){
		return this.navigation==navigation&&navigation!=null&&navigation.GetFaceWalkable(face);
	}
}
