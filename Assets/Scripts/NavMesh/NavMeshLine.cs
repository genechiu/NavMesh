using System;
using System.Collections.Generic;
public struct NavMeshLine{
	public NavMeshPoint startPoint;
	public float distance;
	public float deltaX;
	public float deltaY;
	public float deltaZ;
	
	public NavMeshLine(NavMeshPoint startPoint,float distance,float deltaX,float deltaY,float deltaZ){
		this.startPoint=startPoint;
		this.distance=distance;
		this.deltaX=deltaX;
		this.deltaY=deltaY;
		this.deltaZ=deltaZ;
	}
	
	public NavMeshPoint GetPoint(float distance){
		var point=startPoint;
		if(this.distance>0){
			var ratio=distance/this.distance;
			point.x+=deltaX*ratio;
			point.y+=deltaY*ratio;
			point.z+=deltaZ*ratio;
		}
		return point;
	}
}
