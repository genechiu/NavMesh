using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class NavMeshExporter:Editor{
	
	public const byte VERSION=1;
	
	private class Vert{
		public int id;
		public float x;
		public float y;
		public float z;
		public UnityEngine.Vector3 ToVector3(){
			return new UnityEngine.Vector3(x,y,z);
		}
	}
	
	private class Face{
		public int id;
		public int area;
		public float centerX;
		public float centerZ;
		public float normalX;
		public float normalZ;
		public double normalA;
		public double normalB;
		public double normalC;
		public double normalD;
		public uint sortValue;
		public List<Vert> verts=new List<Vert>();
	}
	
	private class Pair{
		public float centerX;
		public float centerZ;
		public float distance;
		public Face firstEdgeFace;
		public int firstEdgeIndex;
		public Face secondEdgeFace;
		public int secondEdgeIndex;
	}
	
	private static List<Vert> vertList=new List<Vert>();
	private static List<Face> faceList=new List<Face>();
	private static List<Pair> pairList=new List<Pair>();
	private static Dictionary<Vert,Face> vertFaceDict=new Dictionary<Vert,Face>();
	private static Dictionary<Vert,Dictionary<Vert,Pair>> vertPairDict=new Dictionary<Vert,Dictionary<Vert,Pair>>();
	private static Dictionary<float,Dictionary<float,Vert>> pointVertDict=new Dictionary<float,Dictionary<float,Vert>>();
	private static Dictionary<int,Vert> indexVertDict=new Dictionary<int,Vert>();
	private static string outputFolder="Assets/Resources/Scenes/";
	
	[MenuItem("NavMesh/Export Scene")]
	public static void ExportScene(){
		var triangulation=UnityEngine.AI.NavMesh.CalculateTriangulation();
		if(triangulation.indices.Length<3){
			return;
		}
		vertList.Clear();
		faceList.Clear();
		pairList.Clear();
		vertFaceDict.Clear();
		vertPairDict.Clear();
		pointVertDict.Clear();
		indexVertDict.Clear();
		InputVertices(triangulation.vertices);
		InputTriangles(triangulation.indices,triangulation.areas);
		IndexVertsAndFaces();
		WriteFile();
	}
	
	private static void InputVertices(Vector3[] vertices){
		for(int i=0,n=vertices.Length-1;i<=n;i++){
			var point=vertices[i];
			var x=(float)Math.Round(point.x,2);
			var y=(float)Math.Round(point.y,2);
			var z=(float)Math.Round(point.z,2);
			if(!pointVertDict.ContainsKey(x)){
				pointVertDict.Add(x,new Dictionary<float,Vert>());
			}
			Vert vert;
			if(pointVertDict[x].ContainsKey(z)){
				vert=pointVertDict[x][z];
			}
			else{
				vert=new Vert();
				vert.x=x;
				vert.y=y;
				vert.z=z;
				pointVertDict[x][z]=vert;
			}
			indexVertDict.Add(i,vert);
		}
	}
	
	private static void InputTriangles(int[] indices,int[] areas){
		Face face=null;
		var faceIndices=new HashSet<int>();
		for(int i=0,n=areas.Length;i<n;i++){
			var triangleIndexList=new int[3];
			var triangleVertList=new Vert[3];
			for(var j=0;j<3;j++){
				triangleIndexList[j]=indices[i*3+j];
				triangleVertList[j]=indexVertDict[triangleIndexList[j]];
			}
			var vert0=triangleVertList[0];
			var vert1=triangleVertList[1];
			var vert2=triangleVertList[2];
			if(vert0==vert1||vert1==vert2||vert2==vert0){
				continue;
			}
			var newFace=true;
			var area=areas[i]>=3?areas[i]-2:0;
			if(face!=null&&face.area==area){
				for(var j=0;j<3;j++){
					if(faceIndices.Contains(triangleIndexList[j])){
						newFace=false;
						break;
					}
				}
			}
			if(newFace){
				if(face!=null){
					InitFace(face);
					faceIndices.Clear();
				}
				face=new Face();
				face.area=area;
			}
			double x1=vert1.x-vert0.x;
			double y1=vert1.y-vert0.y;
			double z1=vert1.z-vert0.z;
			double x2=vert2.x-vert0.x;
			double y2=vert2.y-vert0.y;
			double z2=vert2.z-vert0.z;
			double normalA=y1*z2-z1*y2;
			double normalB=z1*x2-x1*z2;
			double normalC=x1*y2-y1*x2;
			if(normalB<-0.000001||0.000001<normalB){
				var normalD=normalA+normalB+normalC;
				if(normalD>face.normalD){
					face.normalA=normalA;
					face.normalB=normalB;
					face.normalC=normalC;
					face.normalD=normalD;
				}
			}
			for(var j=0;j<3;j++){
				if(!faceIndices.Contains(triangleIndexList[j])){
					faceIndices.Add(triangleIndexList[j]);
					face.verts.Add(triangleVertList[j]);
				}
			}
		}
		if(face!=null){
			InitFace(face);
		}
		foreach(var pair in pairList){
			var firstFace=pair.firstEdgeFace;
			var secondFace=pair.secondEdgeFace;
			var firstDistance=GetDistance(firstFace.centerX-pair.centerX,firstFace.centerZ-pair.centerZ);
			var secondDistance=GetDistance(secondFace.centerX-pair.centerX,secondFace.centerZ-pair.centerZ);
			pair.distance=firstDistance+secondDistance;
		}
	}
	
	private static float GetDistance(float deltaX,float deltaZ){
		return (float)Math.Round(Math.Sqrt((double)deltaX*(double)deltaX+(double)deltaZ*(double)deltaZ),2);
	}
	
	private static void InitFace(Face face){
		face.centerX=0;
		face.centerZ=0;
		var vertCount=face.verts.Count;
		foreach(var vert in face.verts){
			face.centerX+=vert.x;
			face.centerZ+=vert.z;
			if(!vertFaceDict.ContainsKey(vert)){
				vertFaceDict.Add(vert,face);
				vertList.Add(vert);
			}
		}
		face.centerX/=vertCount;
		face.centerZ/=vertCount;
		if(face.normalB!=0){
			face.normalX=(float)Math.Round(face.normalA/face.normalB,6);
			face.normalZ=(float)Math.Round(face.normalC/face.normalB,6);
		}
		for(int i=0,n=vertCount-1;i<=n;i++){
			var firstVert=face.verts[i];
			var secondVert=face.verts[i==n?0:i+1];
			if(!vertPairDict.ContainsKey(firstVert)){
				vertPairDict.Add(firstVert,new Dictionary<Vert,Pair>());
			}
			if(!vertPairDict.ContainsKey(secondVert)){
				vertPairDict.Add(secondVert,new Dictionary<Vert,Pair>());
			}
			if(!vertPairDict[secondVert].ContainsKey(firstVert)){
				var pair=new Pair();
				pair.firstEdgeFace=face;
				pair.firstEdgeIndex=i;
				vertPairDict[firstVert][secondVert]=pair;
			}
			else{
				var pair=vertPairDict[secondVert][firstVert];
				pair.centerX=(firstVert.x+secondVert.x)/2;
				pair.centerZ=(firstVert.z+secondVert.z)/2;
				pair.secondEdgeFace=face;
				pair.secondEdgeIndex=i;
				pairList.Add(pair);
			}
		}
		faceList.Add(face);
	}
	
	private static void IndexVertsAndFaces(){
		var minX=float.MaxValue;
		var maxX=float.MinValue;
		var minZ=float.MaxValue;
		var maxZ=float.MinValue;
		foreach(var vert in vertList){
			if(minX>vert.x){
				minX=vert.x;
			}
			if(maxX<vert.x){
				maxX=vert.x;
			}
			if(minZ>vert.z){
				minZ=vert.z;
			}
			if(maxZ<vert.x){
				maxZ=vert.x;
			}
		}
		var hilbertX=65535f/(maxX-minX);
		var hilbertZ=65535f/(maxZ-minZ);
		foreach(var face in faceList){
			var X=(uint)Math.Round((face.centerX-minX)*hilbertX);
			var Z=(uint)Math.Round((face.centerZ-minZ)*hilbertZ);
			var a=X^Z;
			var b=0xFFFF^a;
			var c=0xFFFF^(X|Z);
			var d=X&(Z^0xFFFF);
			var A=a|(b>>1);
			var B=(a>>1)^a;
			var C=((c>>1)^(b&(d>>1)))^c;
			var D=((a&(c>>1))^(d>>1))^d;
			a=A;
			b=B;
			c=C;
			d=D;
			A=(a&(a>>2))^(b&(b>>2));
			B=(a&(b>>2))^(b&((a^b)>>2));
			C^=(a&(c>>2))^(b&(d>>2));
			D^=(b&(c>>2))^((a^b)&(d>>2));
			a=A;
			b=B;
			c=C;
			d=D;
			A=(a&(a>>4))^(b&(b>>4));
			B=(a&(b>>4))^(b&((a^b)>>4));
			C^=(a&(c>>4))^(b&(d>>4));
			D^=(b&(c>>4))^((a^b)&(d>>4));
			a=A;
			b=B;
			c=C;
			d=D;
			C^=(a&(c>>8))^(b&(d>>8));
			D^=(b&(c>>8))^((a^b)&(d>>8));
			C^=C>>1;
			D^=D>>1;
			c=X^Z;
			d=D|(0xFFFF^(c|C));
			c=(c|(c<<8))&0x00FF00FF;
			c=(c|(c<<4))&0x0F0F0F0F;
			c=(c|(c<<2))&0x33333333;
			c=(c|(c<<1))&0x55555555;
			d=(d|(d<<8))&0x00FF00FF;
			d=(d|(d<<4))&0x0F0F0F0F;
			d=(d|(d<<2))&0x33333333;
			d=(d|(d<<1))&0x55555555;
			face.sortValue=(d<<1)|c;
		}
		faceList.Sort(SortComparison);
		for(int i=0,n=vertList.Count;i<n;i++){
			vertList[i].id=i;
		}
		for(int i=0,n=faceList.Count;i<n;i++){
			faceList[i].id=i;
		}
	}
	
	private static int SortComparison(Face a,Face b){
		return a.sortValue.CompareTo(b.sortValue);
	}
	
	private static void WriteFile(){
		var path=outputFolder+SceneManager.GetActiveScene().name+".bytes";
		var writer=new BinaryWriter(new FileStream(path,FileMode.Create));
		writer.Write('N');
		writer.Write('a');
		writer.Write('v');
		writer.Write('M');
		writer.Write('e');
		writer.Write('s');
		writer.Write('h');
		writer.Write(VERSION);
		writer.Write(vertList.Count);
		foreach(var vert in vertList){
			writer.Write(vert.x);
			writer.Write(vert.y);
			writer.Write(vert.z);
		}
		writer.Write(faceList.Count);
		foreach(var face in faceList){
			writer.Write(face.area);
			writer.Write(face.normalX);
			writer.Write(face.normalZ);
			writer.Write(face.verts.Count);
			foreach(var vert in face.verts){
				writer.Write(vert.id);
			}
		}
		writer.Write(pairList.Count);
		foreach(var pair in pairList){
			writer.Write(pair.distance);
			writer.Write(pair.firstEdgeFace.id);
			writer.Write(pair.firstEdgeIndex);
			writer.Write(pair.secondEdgeFace.id);
			writer.Write(pair.secondEdgeIndex);
		}
		writer.Flush();
		writer.Close();
		AssetDatabase.Refresh();
	}
}
