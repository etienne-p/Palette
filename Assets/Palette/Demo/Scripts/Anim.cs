using UnityEngine;
using System.Collections;

public class Anim : MonoBehaviour 
{
	readonly Vector3 forward = new Vector3(0, 0, 1);
		
	void Update () 
	{
		transform.localRotation = Quaternion.AngleAxis(Time.time, forward);
	
	}
}
