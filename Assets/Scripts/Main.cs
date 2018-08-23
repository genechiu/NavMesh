﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Main:MonoBehaviour{
	
	private Camera cameraObject;
	private Transform cameraTransform;
	private bool cameraDragEnabled;
	private float cameraDragMouseX;
	private float cameraDragMouseY;
	
	private Vector3 targetRotation=new Vector3(45f,0f,0f);
	private Vector3 cameraRotation=new Vector3();
	private Vector3 targetPosition=new Vector3();
	private Vector3 cameraPosition=new Vector3();
	private float targetDistance=15f;
	private float cameraDistance;
	
	private GameObject[] areaGameObjects=new GameObject[4];
	private Transform mousePointTransform;
	private Material mousePointMaterial;
	private LineRenderer mouseTowardsLine;
	private Material mouseTowardsLineMaterial;
	
	private Camera miniCameraObject;
	private Transform miniCameraTransform;
	private float miniCameraSize=150f;
	private float miniMapSize=100;
	private float miniMinX=0;
	private float miniMaxX=110;
	private float miniMinZ=-5;
	private float miniMaxZ=100;
	
	private NavMesh navigation;
	private Actor heroActor;
	
	private void Awake(){
		cameraObject=GameObject.Find("Camera").GetComponent<Camera>();
		cameraTransform=cameraObject.transform;
		
		var mapTransform=GameObject.Find("Map").transform;
		for(int i=0;i<4;i++){
			areaGameObjects[i]=mapTransform.Find("Area_"+i).gameObject;
		}
		
		mousePointTransform=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
		mousePointMaterial=mousePointTransform.GetComponent<Renderer>().material;
		mousePointMaterial.shader=Shader.Find("Unlit/Color");
		
		mouseTowardsLine=new GameObject("MouseTowards").AddComponent<LineRenderer>();
		mouseTowardsLineMaterial=new Material(Shader.Find("Unlit/Color"));
		mouseTowardsLineMaterial.color=Color.blue;
		mouseTowardsLine.material=mouseTowardsLineMaterial;
		mouseTowardsLine.startWidth=0.5f;
		mouseTowardsLine.endWidth=0.1f;
		mouseTowardsLine.positionCount=2;
		
		miniCameraObject=new GameObject("MiniCamera").AddComponent<Camera>();
		miniCameraObject.orthographic=true;
		miniCameraObject.orthographicSize=miniCameraSize;
		miniCameraObject.clearFlags=CameraClearFlags.Depth;
		miniCameraObject.depth=1;
		miniCameraTransform=miniCameraObject.transform;
		miniCameraTransform.eulerAngles=new Vector3(90,0,0);
		miniCameraTransform.position=new Vector3(0,999,0);
		
		navigation=new NavMesh(Resources.Load<TextAsset>("Scenes/game").bytes);
		areaGameObjects[navigation.area].SetActive(false);
	}
	
	void Update(){
		if(heroActor!=null){
			heroActor.Update(Time.deltaTime);
		}
		if(Input.GetKeyDown(KeyCode.Space)){
			if(navigation.area<navigation.lastArea){
				navigation.EnableNextArea();
				areaGameObjects[navigation.area].SetActive(false);
			}
		}
		var miniCameraScale=miniCameraSize/Screen.height;
		var miniCameraZ=miniMapSize-miniCameraSize;
		if(miniCameraTransform!=null){
			var miniCameraX=Screen.width*miniCameraScale;
			var position=miniCameraTransform.position;
			if(position.x!=miniCameraX||position.z!=miniCameraZ){
				position.x=miniCameraX;
				position.z=miniCameraZ;
				miniCameraTransform.position=position;
			}
		}
		var mouseTowardsActive=false;
		var mouseX=Input.mousePosition.x;
		var mouseY=Input.mousePosition.y;
		var x=mouseX*miniCameraScale*2;
		var z=mouseY*miniCameraScale*2+miniCameraZ-miniCameraSize;
		if(x>=miniMinX&&x<=miniMaxX&&z>=miniMinZ&&z<=miniMaxZ){
			if(!mousePointTransform.gameObject.activeSelf){
				mousePointTransform.gameObject.SetActive(true);
			}
			mousePointTransform.position=new Vector3(x,50,z);
			if(heroActor!=null){
				if(Input.GetMouseButtonDown(1)){
					heroActor.AttackAt(x,z);
				}
			}
			var face=navigation.FindFaceAt(x,z);
			if(navigation.GetFaceWalkable(face)){
				var position=navigation.GetPosition(face,x,z);
				if(heroActor==null){
					mousePointMaterial.color=Color.cyan;
					if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)){
						heroActor=new Actor(position,0);
						heroActor.SetAvatar("yihu",0,0,2);
						heroActor.PlayAnimation("win");
						cameraDistance=Vector3.Distance(heroActor.transform.position,cameraTransform.position);
						cameraPosition=cameraTransform.position+cameraTransform.forward*cameraDistance;
						cameraRotation=cameraTransform.eulerAngles;
						if(navigation.area<navigation.lastArea){
							navigation.EnableNextArea();
							areaGameObjects[navigation.area].SetActive(false);
						}
					}
				}
				else{
					if(navigation.FindShortestPath(heroActor.position,position)){
						mousePointMaterial.color=Color.green;
					}
					else{
						mousePointMaterial.color=Color.magenta;
					}
					if(Input.GetMouseButtonDown(0)){
						heroActor.MoveTo(position);
					}
				}
			}
			else{
				mousePointMaterial.color=Color.red;
			}
		}
		else{
			if(mousePointTransform.gameObject.activeSelf){
				mousePointTransform.gameObject.SetActive(false);
			}
			if(heroActor!=null){
				var ray=cameraObject.ScreenPointToRay(Input.mousePosition);
				var plane=new Plane(Vector3.up,heroActor.transform.position);
				float enter;
				if(plane.Raycast(ray,out enter)){
					var position0=heroActor.transform.position;
					var position1=ray.GetPoint(enter);
					var deltaX=position1.x-position0.x;
					var deltaZ=position1.z-position0.z;
					if(deltaX!=0||deltaZ!=0){
						mouseTowardsActive=true;
						mouseTowardsLine.SetPosition(0,position0);
						mouseTowardsLine.SetPosition(1,position1);
						var distance=Vector3.Distance(position0,position1);
						var rotation=90.0f-Mathf.Rad2Deg*Mathf.Atan2(deltaZ,deltaX);
						if(Input.GetMouseButtonDown(0)){
							heroActor.MoveTowards(rotation,distance);
						}
						if(Input.GetMouseButtonDown(1)){
							heroActor.DashTowards(rotation,distance);
						}
					}
				}
			}
		}
		if(mouseTowardsLine.gameObject.activeSelf!=mouseTowardsActive){
			mouseTowardsLine.gameObject.SetActive(mouseTowardsActive);
		}
		if(heroActor!=null){
			if(Input.GetMouseButtonDown(2)){
				cameraDragEnabled=true;
				cameraDragMouseX=mouseX;
				cameraDragMouseY=mouseY;
			}
			if(Input.GetKeyDown(KeyCode.Alpha1)){
				heroActor.SetAvatarBody(1);
			}
			else if(Input.GetKeyDown(KeyCode.Alpha2)){
				heroActor.SetAvatarBody(2);
			}
			else if(Input.GetKeyDown(KeyCode.Alpha3)){
				heroActor.SetAvatarBody(0);
			}
			else if(Input.GetKeyDown(KeyCode.Alpha4)){
				heroActor.SetAvatarWeapon(1);
			}
			else if(Input.GetKeyDown(KeyCode.Alpha5)){
				heroActor.SetAvatarWeapon(2);
			}
			else if(Input.GetKeyDown(KeyCode.Alpha6)){
				heroActor.SetAvatarWeapon(0);
			}
			var moveRotation=-1;
			if(Input.GetKey(KeyCode.W)&&Input.GetKey(KeyCode.D)){
				moveRotation=45;
			}
			else if(Input.GetKey(KeyCode.D)&&Input.GetKey(KeyCode.S)){
				moveRotation=135;
			}
			else if(Input.GetKey(KeyCode.S)&&Input.GetKey(KeyCode.A)){
				moveRotation=225;
			}
			else if(Input.GetKey(KeyCode.A)&&Input.GetKey(KeyCode.W)){
				moveRotation=315;
			}
			else if(Input.GetKey(KeyCode.W)){
				moveRotation=0;
			}
			else if(Input.GetKey(KeyCode.D)){
				moveRotation=90;
			}
			else if(Input.GetKey(KeyCode.S)){
				moveRotation=180;
			}
			else if(Input.GetKey(KeyCode.A)){
				moveRotation=270;
			}
			if(moveRotation>=0){
				heroActor.MoveTowards(moveRotation,heroActor.moveSpeed*0.1f);
			}
		}
		if(cameraDragEnabled){
			if(Input.GetMouseButton(2)){
				targetRotation.y+=(mouseX-cameraDragMouseX)*0.5f;
				targetRotation.x-=(mouseY-cameraDragMouseY)*0.1f;
				if(targetRotation.x<0){
					targetRotation.x=0;
				}
				else if(targetRotation.x>90){
					targetRotation.x=90;
				}
				cameraDragMouseX=mouseX;
				cameraDragMouseY=mouseY;
			}
			else{
				cameraDragEnabled=false;
			}
		}
		if(Input.mouseScrollDelta.y<0){
			if(targetDistance<50){
				targetDistance++;
			}
		}
		else if(Input.mouseScrollDelta.y>0){
			if(targetDistance>5){
				targetDistance--;
			}
		}
		if(heroActor!=null){
			targetPosition=heroActor.transform.position;
			var dirtyRotation=cameraRotation!=targetRotation;
			var dirtyPosition=cameraPosition!=targetPosition;
			var dirtyDistance=cameraDistance!=targetDistance;
			if(dirtyRotation){
				var deltaRotation=targetRotation-cameraRotation;
				if(deltaRotation.sqrMagnitude>1f){
					cameraRotation+=deltaRotation*0.2f;
				}
				else{
					cameraRotation=targetRotation;
				}
				cameraTransform.eulerAngles=cameraRotation;
			}
			if(dirtyPosition){
				var deltaPosition=targetPosition-cameraPosition;
				if(deltaPosition.sqrMagnitude>0.1f){
					cameraPosition+=deltaPosition*0.2f;
				}
				else{
					cameraPosition=targetPosition;
				}
			}
			if(dirtyDistance){
				var deltaDistance=targetDistance-cameraDistance;
				if(deltaDistance<-0.1f||deltaDistance>0.1f){
					cameraDistance+=deltaDistance*0.2f;
				}
				else{
					cameraDistance=targetDistance;
				}
			}
			if(dirtyRotation||dirtyPosition||dirtyDistance){
				cameraTransform.position=cameraPosition-cameraTransform.forward*cameraDistance;
			}
		}
	}
}
