using UnityEngine;

[System.Serializable]
public class GpuAnimData:ScriptableObject{
	public string[] bones;
	public GpuAnimClip[] clips;
}
