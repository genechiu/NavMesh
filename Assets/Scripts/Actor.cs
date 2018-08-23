using System.Collections.Generic;
using UnityEngine;

public class Actor{
	
	public float moveSpeed=5f;
	public int moveMode{get;private set;}
	public NavMesh navigation{get;private set;}
	public GpuAnim animation{get;private set;}
	public Transform transform{get;private set;}
	public NavMeshPoint position{get;private set;}
	public float targetRotation{get;private set;}
	public float rotation{get;private set;}
	
	private int pathLineIndex=0;
	private float pathLineDistance=0;
	private Transform pathLinesTransform=null;
	private List<NavMeshLine> pathLines=new List<NavMeshLine>();
	private NavMeshPoint pathEndPoint;
	
	public Actor(NavMeshPoint position,float rotation){
		this.position=position;
		this.rotation=rotation;
		this.targetRotation=rotation;
		navigation=position.navigation;
		transform=new GameObject("Actor").transform;
		RefreshTransformRotation();
		RefreshTransformPosition();
	}
	
	private string avatarName;
	private Transform headTransform;
	private Transform bodyTransform;
	private Transform handTransform;
	private Transform weaponTransform;
	public void SetAvatar(string name,int head,int body,int weapon){
		if(animation!=null){
			GameObject.Destroy(animation.gameObject);
		}
		avatarName=name;
		animation=LoadGameObject("Actors/"+avatarName+"/"+avatarName).GetComponent<GpuAnim>();
		animation.transform.SetParent(transform,false);
		SetAvatarHead(head);
		SetAvatarBody(body);
		SetAvatarWeapon(weapon);
	}
	
	public void SetAvatarHead(int head){
		if(animation==null){
			return;
		}
		if(headTransform!=null){
			GameObject.Destroy(headTransform.gameObject);
		}
		headTransform=LoadGameObject("Actors/"+avatarName+"/"+avatarName+"_head_"+head).transform;
		headTransform.SetParent(animation.transform,false);
	}
	
	public void SetAvatarBody(int body){
		if(animation==null){
			return;
		}
		if(bodyTransform!=null){
			GameObject.Destroy(bodyTransform.gameObject);
		}
		bodyTransform=LoadGameObject("Actors/"+avatarName+"/"+avatarName+"_body_"+body).transform;
		bodyTransform.SetParent(animation.transform,false);
	}
	
	public void SetAvatarWeapon(int weapon){
		if(animation==null){
			return;
		}
		if(weaponTransform!=null){
			GameObject.Destroy(weaponTransform.gameObject);
		}
		if(handTransform==null){
			handTransform=new GameObject("rweapon").transform;
			handTransform.SetParent(animation.transform,false);
		}
		weaponTransform=LoadGameObject("Actors/"+avatarName+"/"+avatarName+"_weapon_"+weapon).transform;
		weaponTransform.SetParent(handTransform,false);
	}
	
	private GameObject LoadGameObject(string path){
		return GameObject.Instantiate<GameObject>(Resources.Load<GameObject>(path));
	}
	
	public void PlayAnimation(string name){
		if(animation!=null){
			animation.Play(name);
		}
	}
	
	public void MoveTo(NavMeshPoint position){
		ClearPathLines();
		navigation.FindShortestPath(this.position,position,pathLines);
		pathEndPoint=position;
		if(pathLines.Count>0){
			RefreshTargetRotationFromPathLine();
		}
		StartMove(2);
	}
	
	public void MoveTowards(float rotation,float distance){
		ClearPathLines();
		targetRotation=NavMesh.NearestRotation(targetRotation,rotation);
		pathEndPoint=navigation.FindTowardsPath(position,rotation,distance,true,pathLines);
		StartMove(1);
	}
	
	public void DashTowards(float rotation,float distance){
		StopMove();
		this.rotation=rotation;
		targetRotation=rotation;
		RefreshTransformRotation();
		position=navigation.FindTowardsPath(position,rotation,distance,false);
		RefreshTransformPosition();
		PlayAnimation("dash");
	}
	
	public void AttackAt(float x,float z){
		StopMove();
		position=navigation.FindPositionFrom(position,x,z);
		RefreshTransformPosition();
		PlayAnimation("attack");
	}
	
	private void StartMove(int mode){
		if(pathLines.Count>0){
			if(moveMode==0){
				PlayAnimation("move");
			}
			moveMode=mode;
			DrawPathLines();
		}
		else{
			StopMove();
		}
	}
	
	public void StopMove(){
		if(moveMode>0){
			moveMode=0;
			ClearPathLines();
			PlayAnimation("idle");
		}
	}
	
	private void DrawPathLines(){
		if(pathLinesTransform!=null){
			GameObject.Destroy(pathLinesTransform.gameObject);
		}
		pathLinesTransform=new GameObject("PathLines").transform;
		foreach(var pathLine in pathLines){
			LineRenderer lineRenderer=new GameObject("Line").AddComponent<LineRenderer>();
			lineRenderer.transform.SetParent(pathLinesTransform,false);
			Material material=new Material(Shader.Find("Unlit/Color"));
			material.color=Color.yellow;
			lineRenderer.material=material;
			var start=pathLine.startPoint.ToVector3();
			var end=new Vector3(start.x+pathLine.deltaX,start.y+pathLine.deltaY,start.z+pathLine.deltaZ);
			var center=Vector3.Lerp(start,end,0.5f);
			lineRenderer.SetPosition(0,start-center);
			lineRenderer.SetPosition(1,end-center);
			lineRenderer.transform.position=center;
			lineRenderer.widthMultiplier=0.2f;
			lineRenderer.useWorldSpace=false;
		}
	}
	
	private void ClearPathLines(){
		if(pathLinesTransform!=null){
			GameObject.Destroy(pathLinesTransform.gameObject);
			pathLinesTransform=null;
		}
		pathLines.Clear();
		pathLineIndex=0;
		pathLineDistance=0;
	}
	
	public void Update(float deltaTime){
		if(rotation!=targetRotation){
			var deltaRotation=targetRotation-rotation;
			if(deltaRotation>-1f&&deltaRotation<1f){
				rotation=targetRotation;
			}
			else{
				rotation+=deltaRotation*0.2f;
			}
			RefreshTransformRotation();
		}
		if(moveMode>0){
			pathLineDistance+=moveSpeed*deltaTime;
			var lastLineIndex=pathLines.Count-1;
			while(pathLineDistance>=pathLines[pathLineIndex].distance){
				if(pathLineIndex<lastLineIndex){
					pathLineDistance-=pathLines[pathLineIndex].distance;
					pathLineIndex++;
					if(moveMode==2){
						RefreshTargetRotationFromPathLine();
					}
				}
				else{
					position=pathEndPoint;
					RefreshTransformPosition();
					StopMove();
					return;
				}
			}
			position=pathLines[pathLineIndex].GetPoint(pathLineDistance);
			RefreshTransformPosition();
		}
	}
	
	private void RefreshTargetRotationFromPathLine(){
		var deltaX=pathLines[pathLineIndex].deltaX;
		var deltaZ=pathLines[pathLineIndex].deltaZ;
		if(deltaX!=0||deltaZ!=0){
			targetRotation=NavMesh.NearestRotation(targetRotation,NavMesh.Rotation(deltaX,deltaZ));
		}
	}
	
	private void RefreshTransformRotation(){
		transform.transform.eulerAngles=new Vector3(0,rotation,0);
	}
	
	private void RefreshTransformPosition(){
		transform.transform.position=position.ToVector3();
	}
}
