﻿﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class NavMesh{
	
	private class Vert{
		public float x;
		public float y;
		public float z;
		public LinkedList<Edge> edges;
		
		public NavMeshPoint GetPosition(NavMesh navigation,int face){
			return new NavMeshPoint(navigation,face,x,y,z);
		}
	}
	
	private class Face{
		public int id;
		public int area;
		public float normalX;
		public float normalZ;
		public Edge[] edges;
		
		public bool Contains(float x,float z){
			foreach(var edge in edges){
				if(!edge.Right(x,z)){
					return false;
				}
			}
			return true;
		}

		public bool Walkable(NavMesh navigation){
			return area<=navigation.area;
		}
		
		public float GetY(float x,float z){
			var vert=edges[0].vert;
			var y=vert.y;
			if(normalX!=0f){
				y+=(vert.x-x)*normalX;
			}
			if(normalZ!=0f){
				y+=(vert.z-z)*normalZ;
			}
			return y;
		}
		
		public Rect GetRect(){
			Rect rect=null;
			foreach(var edge in edges){
				var x=edge.vert.x;
				var z=edge.vert.z;
				if(rect==null){
					rect=new Rect(x,x,z,z,this);
				}
				else{
					rect.Union(x,x,z,z);
				}
			}
			return rect;
		}
	}
	
	private class Pair{
		public float faceDistance;
		public Edge firstEdge;
		public Edge secondEdge;
	}
	
	private class Edge{
		public float deltaX;
		public float deltaZ;
		public Vert vert;
		public Face face;
		public Edge next;
		public Edge pair;
		
		public bool Right(float x,float z){
			return MinZeroEpsilon<Cross(x-vert.x,z-vert.z,deltaX,deltaZ);
		}
		
		public bool Walkable(NavMesh navigation){
			return pair!=null&&pair.face.area<=navigation.area;
		}
		
		public NavMeshPoint GetPoint(NavMesh navigation,float ratio){
			var a=vert;
			var b=next.vert;
			var x=ratio*(b.x-a.x)+a.x;
			var y=ratio*(b.y-a.y)+a.y;
			var z=ratio*(b.z-a.z)+a.z;
			return new NavMeshPoint(navigation,face.id,x,y,z);
		}
	}
	
	private class Rect{
		public float minX;
		public float maxX;
		public float minZ;
		public float maxZ;
		public Face face;
		
		public Rect(float minX,float maxX,float minZ,float maxZ,Face face=null){
			this.minX=minX;
			this.maxX=maxX;
			this.minZ=minZ;
			this.maxZ=maxZ;
			this.face=face;
		}
		
		public void Union(float minX,float maxX,float minZ,float maxZ){
			if(this.minX>minX){
				this.minX=minX;
			}
			if(this.maxX<maxX){
				this.maxX=maxX;
			}
			if(this.minZ>minZ){
				this.minZ=minZ;
			}
			if(this.maxZ<maxZ){
				this.maxZ=maxZ;
			}
		}
		
		public bool Contains(Rect rect){
			return rect.maxX>=minX&&rect.minX<=maxX&&rect.maxZ>=minZ&&rect.minZ<=maxZ;
		}
	}

	private const float MinZeroEpsilon=-0.000001f;
	private const float MaxZeroEpsilon=0.000001f;
	private const float MinOneEpsilon=0.999999f;
	private const float MaxOneEpsilon=1.000001f;
	
	public static float Dot(float firstDeltaX,float firstDeltaZ,float secondDeltaX,float secondDeltaZ){
		return firstDeltaX*secondDeltaX+firstDeltaZ*secondDeltaZ;
	}
	
	public static float Cross(float firstDeltaX,float firstDeltaZ,float secondDeltaX,float secondDeltaZ){
		return firstDeltaX*secondDeltaZ-secondDeltaX*firstDeltaZ;
	}
	
	public static float Projection(float startX,float startZ,float deltaX,float deltaZ,float x,float z){
		return Dot(x-startX,z-startZ,deltaX,deltaZ)/(deltaX*deltaX+deltaZ*deltaZ);
	}
	
	public static float Distance(float deltaX,float deltaZ){
		return (float)Math.Sqrt(deltaX*deltaX+deltaZ*deltaZ);
	}
	
	public static float Rotation(float deltaX,float deltaZ){
		const double RadianToRotation=180.0/System.Math.PI;
		return (float)(90.0-RadianToRotation*Math.Atan2(deltaZ,deltaX));
	}

	public static float NearestRotation(float startRotation,float endRotation){
		var deltaRotation=(endRotation-startRotation)%360f;
		if(deltaRotation>180f){
			deltaRotation-=360f;
		}
		else if(deltaRotation<-180f){
			deltaRotation+=360f;
		}
		return startRotation+deltaRotation;
	}
	
	public NavMesh(byte[] bytes){
		ParseData(bytes);
		BuildPathMatrix();
		BuildRectTree();
	}
	
	private Vert[] verts;
	private Face[] faces;
	private Pair[] pairs;
	private void ParseData(byte[] bytes){
		var reader=new BinaryReader(new MemoryStream(bytes));
		if(new string(reader.ReadChars(7))!="NavMesh"){
			throw new Exception("Not a NavMesh File.");
		}
		if(reader.ReadByte()!=1){
			throw new Exception("Unsupported Version");
		}
		var vertCount=reader.ReadInt32();
		verts=new Vert[vertCount];
		for(var i=0;i<vertCount;i++){
			var vert=new Vert();
			vert.edges=new LinkedList<Edge>();
			vert.x=reader.ReadSingle();
			vert.y=reader.ReadSingle();
			vert.z=reader.ReadSingle();
			verts[i]=vert;
		}
		
		var faceCount=reader.ReadInt32();
		faces=new Face[faceCount];
		for(var i=0;i<faceCount;i++){
			var face=new Face();
			face.id=i;
			face.area=reader.ReadInt32();
			if(lastArea<face.area){
				lastArea=face.area;
			}
			face.normalX=reader.ReadSingle();
			face.normalZ=reader.ReadSingle();
			var edgeCount=reader.ReadInt32();
			var edges=new Edge[edgeCount];
			for(var j=0;j<edgeCount;j++){
				var edge=new Edge();
				edge.vert=verts[reader.ReadInt32()];
				edge.face=face;
				edges[j]=edge;
			}
			var lastEdgeIndex=edgeCount-1;
			for(var j=0;j<=lastEdgeIndex;j++){
				var edge=edges[j];
				edge.next=edges[j<lastEdgeIndex?(j+1):0];
				edge.deltaX=edge.next.vert.x-edge.vert.x;
				edge.deltaZ=edge.next.vert.z-edge.vert.z;
				edge.next.vert.edges.AddLast(edge);
			}
			face.edges=edges;
			faces[i]=face;
		}
		var pairCount=reader.ReadInt32();
		pairs=new Pair[pairCount];
		for(var i=0;i<pairCount;i++){
			var pair=new Pair();
			pair.faceDistance=reader.ReadSingle();
			pair.firstEdge=faces[reader.ReadInt32()].edges[reader.ReadInt32()];
			pair.secondEdge=faces[reader.ReadInt32()].edges[reader.ReadInt32()];
			pair.firstEdge.pair=pair.secondEdge;
			pair.secondEdge.pair=pair.firstEdge;
			pairs[i]=pair;
		}
	}
	
	private Edge[,,] pathMatrix;
	private void BuildPathMatrix(){
		const float MaxValue=999999f;
		var faceCount=faces.Length;
		pathMatrix=new Edge[lastArea+1,faceCount,faceCount];
		var matrix=new float[faceCount,faceCount];
		for(int area=0;area<=lastArea;area++){
			for(int i=0;i<faceCount;i++){
				for(int j=0;j<faceCount;j++){
					matrix[i,j]=i==j?0:MaxValue;
				}
			}
			foreach(var pair in pairs){
				var firstEdge=pair.firstEdge;
				var secondEdge=pair.secondEdge;
				if(firstEdge.face.area<=area&&secondEdge.face.area<=area){
					var distance=pair.faceDistance;
					var firstFace=firstEdge.face.id;
					var secondFace=secondEdge.face.id;
					matrix[firstFace,secondFace]=distance;
					matrix[secondFace,firstFace]=distance;
					pathMatrix[area,firstFace,secondFace]=secondEdge;
					pathMatrix[area,secondFace,firstFace]=firstEdge;
				}
			}
			for(int k=0;k<faceCount;k++){
				for(int i=0;i<faceCount;i++){
					for(int j=0;j<faceCount;j++){
						var a=matrix[i,k];
						if(a!=MaxValue){
							var b=matrix[k,j];
							if(b!=MaxValue){
								var c=a+b;
								if(matrix[i,j]>c){
									matrix[i,j]=c;
									pathMatrix[area,i,j]=pathMatrix[area,i,k];
								}
							}
						}
					}
				}
			}
		}
	}
	
	private Rect[] treeRects;
	private int treeLineCount;
	private int treeRootCount;
	private int treeLeafCount;
	private int faceLeafCount;
	private void BuildRectTree(int leafCount=6){
		var faceCount=faces.Length;
		if(faceCount<=leafCount||leafCount<=1){
			treeLineCount=1;
			treeRootCount=1;
			treeLeafCount=faceCount;
			faceLeafCount=faceCount;
			treeRects=new Rect[faceCount+1];
			Rect rootRect=null;
			for(var i=0;i<faceCount;i++){
				var faceRect=faces[i].GetRect();
				treeRects[i+1]=faceRect;
				if(rootRect==null){
					rootRect=new Rect(faceRect.minX,faceRect.maxX,faceRect.minZ,faceRect.maxZ);
				}
				else{
					rootRect.Union(faceRect.minX,faceRect.maxX,faceRect.minZ,faceRect.maxZ);
				}
			}
			treeRects[0]=rootRect;
			return;
		}
		treeLineCount=1;
		var rectCount=1;
		var lastLineLeafCount=leafCount;
		for(var i=lastLineLeafCount*leafCount;i<faceCount;i=lastLineLeafCount*leafCount){
			rectCount+=lastLineLeafCount;
			lastLineLeafCount=i;
			treeLineCount++;
		}
		treeLeafCount=leafCount;
		treeRootCount=(faceCount+lastLineLeafCount-1)/lastLineLeafCount;
		lastLineLeafCount=lastLineLeafCount*treeRootCount/leafCount;
		faceLeafCount=(faceCount+lastLineLeafCount-1)/lastLineLeafCount;
		var faceRectCount=faceLeafCount*lastLineLeafCount;
		var faceRectIndex=rectCount*treeRootCount;
		var rectIndex=faceRectIndex-lastLineLeafCount;
		rectCount=faceRectIndex+faceRectCount;
		treeRects=new Rect[rectCount];
		var lessLeafCount=faceLeafCount-1;
		var lessFaceCount=faceRectCount-faceCount;
		for(int i=0,j=0;i<lastLineLeafCount;i++){
			Rect rect=null;
			var n=i<lessFaceCount?lessLeafCount:faceLeafCount;
			for(int k=0;k<n;k++){
				var faceRect=faces[j++].GetRect();
				if(rect==null){
					rect=new Rect(faceRect.minX,faceRect.maxX,faceRect.minZ,faceRect.maxZ);
				}
				else{
					rect.Union(faceRect.minX,faceRect.maxX,faceRect.minZ,faceRect.maxZ);
				}
				treeRects[faceRectIndex+k]=faceRect;
			}
			treeRects[rectIndex+i]=rect;
			faceRectIndex+=faceLeafCount;
		}
		for(int n=treeLineCount;--n>0;){
			var lastRectIndex=rectIndex;
			lastLineLeafCount/=leafCount;
			rectIndex-=lastLineLeafCount;
			for(int i=0;i<lastLineLeafCount;i++){
				Rect rect=null;
				for(int j=0;j<leafCount;j++){
					var childRect=treeRects[lastRectIndex++];
					if(rect==null){
						rect=new Rect(childRect.minX,childRect.maxX,childRect.minZ,childRect.maxZ);
					}
					else{
						rect.Union(childRect.minX,childRect.maxX,childRect.minZ,childRect.maxZ);
					}
				}
				treeRects[rectIndex+i]=rect;
			}
		}
	}
	
	private Rect searchRect=new Rect(0,0,0,0);
	private List<Face> foundFaces=new List<Face>();
	private void FindFaces(float minX,float maxX,float minZ,float maxZ){
		foundFaces.Clear();
		searchRect.minX=minX;
		searchRect.maxX=maxX;
		searchRect.minZ=minZ;
		searchRect.maxZ=maxZ;
		for(int i=0,n=treeRootCount;i<n;i++){
			SearchTree(i,0,i,n,n);
		}
	}
	
	private void SearchTree(int rectIndex,int lineIndex,int leafIndex,int leafCount,int rectCount){
		var rect=treeRects[rectIndex];
		if(rect!=null&&rect.Contains(searchRect)){
			if(lineIndex>=treeLineCount){
				if(rect.face!=null){
					foundFaces.Add(rect.face);
				}
			}
			else{
				lineIndex++;
				if(lineIndex<treeLineCount){
					leafIndex*=treeLeafCount;
					rectIndex=rectCount+leafIndex;
					leafCount*=treeLeafCount;
					rectCount+=leafCount;
					for(int j=0;j<treeLeafCount;j++){
						SearchTree(rectIndex+j,lineIndex,leafIndex+j,leafCount,rectCount);
					}
				}
				else{
					leafIndex*=faceLeafCount;
					rectIndex=rectCount+leafIndex;
					for(int j=0;j<faceLeafCount;j++){
						SearchTree(rectIndex+j,lineIndex,leafIndex+j,0,0);
					}
				}
			}
		}
	}
	
	public int area{get;private set;}
	public int lastArea{get;private set;}
	public void EnableNextArea(){
		if(area<lastArea){
			area++;
		}
	}
	
	public int FindFaceAt(float x,float z){
		FindFaces(x,x,z,z);
		foreach(var foundFace in foundFaces){
			if(foundFace.Contains(x,z)){
				return foundFace.id;
			}
		}
		return -1;
	}
	
	public float GetFaceY(int face,float x,float z){
		if(face>=0&&face<faces.Length){
			return faces[face].GetY(x,z);
		}
		return 0f;
	}
	
	public bool GetFaceContains(int face,float x,float z){
		if(face>=0&&face<faces.Length){
			return faces[face].Contains(x,z);
		}
		return false;
	}
	
	public bool GetFaceWalkable(int face){
		if(face>=0&&face<faces.Length){
			return faces[face].area<=area;
		}
		return false;
	}
	
	public NavMeshPoint GetPosition(int face,float x,float z){
		return new NavMeshPoint(this,face,x,GetFaceY(face,x,z),z);
	}
	
	private NavMeshPoint startPosition;
	private NavMeshPoint endPosition;
	private List<NavMeshLine> pathLines;
	private List<Edge> pathEdges=new List<Edge>();
	public bool FindShortestPath(NavMeshPoint startPosition,NavMeshPoint endPosition,List<NavMeshLine> pathLines=null){
		if(!startPosition.Walkable(this)||!endPosition.Walkable(this)){
			return false;
		}
		var startFace=startPosition.face;
		var endFace=endPosition.face;
		if(startFace!=endFace&&pathMatrix[area,startFace,endFace]==null){
			return false;
		}
		if(pathLines!=null){
			pathEdges.Clear();
			while(startFace!=endFace){
				var pathEdge=pathMatrix[area,startFace,endFace];
				pathEdges.Add(pathEdge);
				startFace=pathEdge.face.id;
			}
			this.startPosition=startPosition;
			this.endPosition=endPosition;
			this.pathLines=pathLines;
			FindPathCorners(0,pathEdges.Count);
			this.pathLines=null;
		}
		return true;
	}
	
	private void FindPathCorners(int pathIndex,int pathCount){
		if(pathIndex>=pathCount){
			MoveTo(endPosition);
			return;
		}
		var prevIndex=pathIndex;
		var prevEdge=pathEdges[prevIndex];
		if(startPosition.x==prevEdge.vert.x&&startPosition.z==prevEdge.vert.z){
			var prevPosition=prevEdge.vert.GetPosition(this,prevEdge.face.id);
			FindPathCrosses(prevPosition,prevIndex,pathIndex);
			FindPathCorners(prevIndex+1,pathCount);
			return;
		}
		var nextIndex=pathIndex;
		var nextEdge=pathEdges[nextIndex];
		if(nextEdge.next.vert.x==startPosition.x&&nextEdge.next.vert.z==startPosition.z){
			var nextPosition=nextEdge.next.vert.GetPosition(this,nextEdge.face.id);
			FindPathCrosses(nextPosition,nextIndex,pathIndex);
			FindPathCorners(nextIndex+1,pathCount);
			return;
		}
		var prevDeltaX=startPosition.x-prevEdge.vert.x;
		var prevDeltaZ=startPosition.z-prevEdge.vert.z;
		var nextDeltaX=nextEdge.next.vert.x-startPosition.x;
		var nextDeltaZ=nextEdge.next.vert.z-startPosition.z;
		for(var i=pathIndex+1;i<pathCount;i++){
			var edge=pathEdges[i];
			var prevStartDeltaX=edge.vert.x-startPosition.x;
			var prevStartDeltaZ=edge.vert.z-startPosition.z;
			var nextStartDeltaX=edge.next.vert.x-startPosition.x;
			var nextStartDeltaZ=edge.next.vert.z-startPosition.z;
			var prevOutOfPrev=Cross(prevStartDeltaX,prevStartDeltaZ,prevDeltaX,prevDeltaZ)<MaxZeroEpsilon;
			var nextOutOfPrev=Cross(nextStartDeltaX,nextStartDeltaZ,prevDeltaX,prevDeltaZ)<MaxZeroEpsilon;
			if(prevOutOfPrev&&nextOutOfPrev){
				var prevPosition=prevEdge.vert.GetPosition(this,prevEdge.face.id);
				FindPathCrosses(prevPosition,prevIndex,pathIndex);
				FindPathCorners(prevIndex+1,pathCount);
				return;
			}
			var prevOutOfNext=Cross(prevStartDeltaX,prevStartDeltaZ,nextDeltaX,nextDeltaZ)<MaxZeroEpsilon;
			var nextOutOfNext=Cross(nextStartDeltaX,nextStartDeltaZ,nextDeltaX,nextDeltaZ)<MaxZeroEpsilon;
			if(prevOutOfNext&&nextOutOfNext){
				var nextPosition=nextEdge.next.vert.GetPosition(this,nextEdge.face.id);
				FindPathCrosses(nextPosition,nextIndex,pathIndex);
				FindPathCorners(nextIndex+1,pathCount);
				return;
			}
			if(!prevOutOfPrev&&!prevOutOfNext){
				prevIndex=i;
				prevEdge=pathEdges[prevIndex];
				prevDeltaX=startPosition.x-prevEdge.vert.x;
				prevDeltaZ=startPosition.z-prevEdge.vert.z;
			}
			if(!nextOutOfPrev&&!nextOutOfNext){
				nextIndex=i;
				nextEdge=pathEdges[nextIndex];
				nextDeltaX=nextEdge.next.vert.x-startPosition.x;
				nextDeltaZ=nextEdge.next.vert.z-startPosition.z;
			}
		}
		var deltaX=endPosition.x-startPosition.x;
		var deltaZ=endPosition.z-startPosition.z;
		if(Cross(deltaX,deltaZ,prevDeltaX,prevDeltaZ)<MaxZeroEpsilon){
			var prevPosition=prevEdge.vert.GetPosition(this,prevEdge.face.id);
			FindPathCrosses(prevPosition,prevIndex,pathIndex);
			FindPathCorners(prevIndex+1,pathCount);
			return;
		}
		if(Cross(deltaX,deltaZ,nextDeltaX,nextDeltaZ)<MaxZeroEpsilon){
			var nextPosition=nextEdge.next.vert.GetPosition(this,nextEdge.face.id);
			FindPathCrosses(nextPosition,nextIndex,pathIndex);
			FindPathCorners(nextIndex+1,pathCount);
			return;
		}
		FindPathCrosses(endPosition,pathCount,pathIndex);
	}
	
	private void FindPathCrosses(NavMeshPoint position,int pathIndex,int pathStart){
		for(;pathStart<pathIndex;pathStart++){
			var edge=pathEdges[pathStart];
			var deltaX=position.x-this.startPosition.x;
			var deltaZ=position.z-this.startPosition.z;
			var cross=Cross(edge.deltaX,edge.deltaZ,deltaX,deltaZ);
			NavMeshPoint crossPosition;
			if(MinZeroEpsilon<cross&&cross<MaxZeroEpsilon){
				crossPosition=edge.vert.GetPosition(this,edge.face.id);
			}
			else{
				var startDeltaX=this.startPosition.x-edge.vert.x;
				var startDeltaZ=this.startPosition.z-edge.vert.z;
				var startCross=Cross(startDeltaX,startDeltaZ,deltaX,deltaZ);
				crossPosition=edge.GetPoint(this,startCross/cross);
			}
			MoveTo(crossPosition);
		}
		MoveTo(position);
	}
	
	private void MoveTo(NavMeshPoint position){
		var deltaX=position.x-startPosition.x;
		var deltaY=position.y-startPosition.y;
		var deltaZ=position.z-startPosition.z;
		if(deltaX!=0||deltaZ!=0){
			var distance=NavMesh.Distance(deltaX,deltaZ);
			pathLines.Add(new NavMeshLine(startPosition,distance,deltaX,deltaY,deltaZ));
		}
		startPosition=position;
	}
	
	private float endDistance;
	private float sinRotation;
	private float cosRotation;
	public NavMeshPoint FindTowardsPath(NavMeshPoint position,float rotation,float distance,bool slope,List<NavMeshLine> pathLines=null){
		if(!position.Walkable(this)){
			return position;
		}
		Vert pathVert=null;
		Edge pathEdge=null;
		var pathEdgeRatio=0f;
		var pathFace=faces[position.face];
		NormalizeRotation(rotation);
		startPosition=position;
		endPosition=position;
		endDistance=distance;
		this.pathLines=pathLines;
		while(endDistance>MaxZeroEpsilon){
			var deltaX=sinRotation*endDistance;
			var deltaZ=cosRotation*endDistance;
			var endX=startPosition.x+deltaX;
			var endZ=startPosition.z+deltaZ;
			if(pathVert!=null){
				foreach(var edge in pathVert.edges){
					if(edge.face.Walkable(this)){
						var next=edge.next;
						var edgeRight=edge.Right(endX,endZ);
						var nextRight=next.Right(endX,endZ);
						if(edgeRight&&nextRight){
							pathFace=edge.face;
							startPosition.face=pathFace.id;
							pathVert=null;
							break;
						}
						if(!edgeRight&&!edge.Walkable(this)){
							if(Dot(next.vert.x-endX,next.vert.z-endZ,edge.deltaX,edge.deltaZ)>MaxZeroEpsilon){
								pathEdge=edge;
								pathEdgeRatio=1;
							}
						}
						else if(!nextRight&&!next.Walkable(this)){
							if(Dot(endX-next.vert.x,endZ-next.vert.z,next.deltaX,next.deltaZ)>MaxZeroEpsilon){
								pathEdge=next;
								pathEdgeRatio=0;
							}
						}
						if(pathEdge!=null){
							pathFace=pathEdge.face;
							startPosition.face=pathFace.id;
							pathVert=null;
							break;
						}
					}
				}
				if(pathVert!=null){
					if(slope&&pathLines!=null){
						pathLines.Add(new NavMeshLine(endPosition,endDistance,0,0,0));
					}
					break;
				}
			}
			if(pathEdge!=null){
				if(!slope){
					break;
				}
				var ratio=Projection(pathEdge.vert.x,pathEdge.vert.z,pathEdge.deltaX,pathEdge.deltaZ,endX,endZ);
				if(ratio<MaxZeroEpsilon){
					pathVert=pathEdge.vert;
					ratio=pathEdgeRatio/(pathEdgeRatio-ratio);
				}
				else if(MinOneEpsilon<ratio){
					pathVert=pathEdge.next.vert;
					ratio=(1-pathEdgeRatio)/(ratio-pathEdgeRatio);
				}
				else{
					MoveTowards(pathEdge.GetPoint(this,ratio),endDistance);
					break;
				}
				MoveTowards(pathVert.GetPosition(this,pathFace.id),endDistance*ratio);
				pathEdge=null;
				continue;
			}
			if(pathFace.Contains(endX,endZ)){
				MoveTowards(new NavMeshPoint(this,pathFace.id,endX,pathFace.GetY(endX,endZ),endZ),endDistance);
				break;
			}
			maxCrossEdge=null;
			maxCrossEdgeRatio=0;
			maxCrossStartRatio=0;
			foreach(var edge in pathFace.edges){
				FindMaxCross(edge,startPosition.x,startPosition.z,deltaX,deltaZ);
			}
			if(maxCrossEdge==null){
				break;
			}
			if(maxCrossEdgeRatio<MaxZeroEpsilon){
				pathVert=maxCrossEdge.vert;
				MoveTowards(pathVert.GetPosition(this,pathFace.id),endDistance*maxCrossStartRatio);
			}
			else if(MinOneEpsilon<maxCrossEdgeRatio){
				pathVert=maxCrossEdge.next.vert;
				MoveTowards(pathVert.GetPosition(this,pathFace.id),endDistance*maxCrossStartRatio);
			}
			else{
				MoveTowards(maxCrossEdge.GetPoint(this,maxCrossEdgeRatio),endDistance*maxCrossStartRatio);
				if(maxCrossEdge.Walkable(this)){
					pathFace=maxCrossEdge.pair.face;
					startPosition.face=pathFace.id;
				}
				else{
					pathEdge=maxCrossEdge;
					pathEdgeRatio=maxCrossEdgeRatio;
				}
			}
		}
		this.pathLines=null;
		return endPosition;
	}
	
	private void MoveTowards(NavMeshPoint position,float distance){
		if(pathLines!=null){
			var deltaX=position.x-startPosition.x;
			var deltaY=position.y-startPosition.y;
			var deltaZ=position.z-startPosition.z;
			pathLines.Add(new NavMeshLine(startPosition,distance,deltaX,deltaY,deltaZ));
		}
		endDistance-=distance;
		startPosition=position;
		endPosition=position;
	}
	
	Edge maxCrossEdge=null;
	float maxCrossEdgeRatio=0f;
	float maxCrossStartRatio=0f;
	private void FindMaxCross(Edge edge,float startX,float startZ,float deltaX,float deltaZ){
		var startDeltaX=startX-edge.vert.x;
		var startDeltaZ=startZ-edge.vert.z;
		var startCross=Cross(startDeltaX,startDeltaZ,edge.deltaX,edge.deltaZ);
		var cross=Cross(edge.deltaX,edge.deltaZ,deltaX,deltaZ);
		if(MinZeroEpsilon<cross&&cross<MaxZeroEpsilon){
			if(MinZeroEpsilon<startCross&&startCross<MaxZeroEpsilon){
				var ratio=Projection(startX,startZ,deltaX,deltaZ,edge.vert.x,edge.vert.z);
				if(MinZeroEpsilon<ratio&&ratio<MaxOneEpsilon){
					if(maxCrossEdge==null||maxCrossStartRatio<ratio){
						maxCrossEdge=edge;
						maxCrossEdgeRatio=0f;
						maxCrossStartRatio=ratio;
					}
				}
				ratio=Projection(startX,startZ,deltaX,deltaZ,edge.next.vert.x,edge.next.vert.z);
				if(MinZeroEpsilon<ratio&&ratio<MaxOneEpsilon){
					if(maxCrossEdge==null||maxCrossStartRatio<ratio){
						maxCrossEdge=edge;
						maxCrossEdgeRatio=1f;
						maxCrossStartRatio=ratio;
					}
				}
			}
		}
		else{
			var startRatio=startCross/cross;
			if(MinZeroEpsilon<startRatio&&startRatio<MaxOneEpsilon){
				var edgeRatio=Cross(startDeltaX,startDeltaZ,deltaX,deltaZ)/cross;
				if(MinZeroEpsilon<edgeRatio&&edgeRatio<MaxOneEpsilon){
					if(maxCrossEdge==null||maxCrossStartRatio<startRatio){
						maxCrossEdge=edge;
						maxCrossEdgeRatio=edgeRatio;
						maxCrossStartRatio=startRatio;
					}
				}
			}
		}
	}
	
	private void NormalizeRotation(float rotation){
		const double RotationToRadian=Math.PI/180.0;
		var radian=RotationToRadian*rotation;
		sinRotation=(float)Math.Round(Math.Sin(radian),6);
		cosRotation=(float)Math.Round(Math.Cos(radian),6);
	}
	
	public NavMeshPoint FindPositionFrom(NavMeshPoint position,float x,float z){
		FindFaces(x,x,z,z);
		foreach(var face in foundFaces){
			if(face.Walkable(this)){
				if(face.Contains(x,z)){
					return GetPosition(face.id,x,z);
				}
			}
		}
		var minX=position.x<x?position.x:x;
		var maxX=position.x>x?position.x:x;
		var minZ=position.z<z?position.z:z;
		var maxZ=position.z>z?position.z:z;
		FindFaces(minX,maxX,minZ,maxZ);
		maxCrossEdge=null;
		maxCrossEdgeRatio=0;
		maxCrossStartRatio=0;
		var startX=position.x;
		var startZ=position.z;
		var deltaX=x-startX;
		var deltaZ=z-startZ;
		foreach(var face in foundFaces){
			if(face.Walkable(this)){
				foreach(var edge in face.edges){
					if(!edge.Walkable(this)){
						FindMaxCross(edge,startX,startZ,deltaX,deltaZ);
					}
				}
			}
		}
		if(maxCrossEdge!=null){
			return maxCrossEdge.GetPoint(this,maxCrossEdgeRatio);
		}
		return position;
	}
}
