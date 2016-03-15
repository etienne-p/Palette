using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class IndexedBilinearParams : MonoBehaviour 
{
	private void Parameterize()
	{
		var renderer = GetComponent<SpriteRenderer>();

		MaterialPropertyBlock block = new MaterialPropertyBlock();
		renderer.GetPropertyBlock(block);

		var tex = block.GetTexture("_MainTex");

		renderer.material.SetFloat("_Width", tex.width);
		renderer.material.SetFloat("_Height", tex.height);
	}
	
	private void Awake() { Parameterize(); }
}
