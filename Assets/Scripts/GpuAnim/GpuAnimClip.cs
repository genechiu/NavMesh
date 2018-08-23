using UnityEngine;

[System.Serializable]
public class GpuAnimClip{
	public string name;
	public bool isLooping;
	public int loopStartFrame;
	public int pixelStartIndex;
	public int frameCount;
	public int frameRate;
	public float length;
	public Bone[] bones;
	
	[System.Serializable]
	public class Bone{
		public Matrix4x4[] frames;
	}
}
