using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class IndexedColorEncoder : EditorWindow
{
	private static string GetPathWithoutExtension(string path)
	{
		return Path.Combine( Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
	}
	
	private static string GetTmpPath(string path)
	{
		return GetPathWithoutExtension(path) + "_tmp.png";
	}
	
	private static string GetMaterialPath(string path)
	{
		return GetPathWithoutExtension(path) + "_indexed.mat";
	}
	
	private static string GetPalettePath(string path)
	{
		return GetPathWithoutExtension(path) + "_palette.png";
	}
	
	private static string GetEncodedPath(string path)
	{
		return GetPathWithoutExtension(path) + "_indexed.png";
	}

	// assumes a 16x8 palette
	private static byte GetIndex(LABColor[] palette, LABColor c)
	{
		byte best = 0; float dist = float.MaxValue;
		
		for (byte i = 0; i < 128; ++i)
		{
			var d = LABColor.Distance(palette[i], c);
			if (d < dist)
			{
				dist = d;
				best = i;
			}
		}
		return best;
	}
	
	private static float progress = .0f;
	private static string currentOperation = "";
	
	private struct WeightedColor { public float weight; public LABColor color; };
	
	private static List<WeightedColor> ComputeWeightedColors(Color[] sourcePixels, float tolerance)
	{
		currentOperation = "Computing palette colors";
		
		// retrieve pixels, keep the visible ones, convert them to the proper color space
		var pixels = Array.ConvertAll(Array.FindAll(sourcePixels, x => x.a > .8f), x => new LABColor(x));
		List<WeightedColor> uniqueColors = new List<WeightedColor> ();
		
		for (int i = 0; i < pixels.Length; ++i)
		{
			progress = (float)i / (float)(pixels.Length - 1);
			
			bool match = false;
			
			// attempt to match
			for (int j = 0; j < uniqueColors.Count; ++j)
			{
				float dist = LABColor.Distance(uniqueColors[j].color, pixels[i]);
				if (dist < tolerance)
				{
					var uc = uniqueColors[j];
					// balance unique color with its contributor
					uc.color = LABColor.Lerp(uc.color, pixels[i], 1.0f / (uc.weight + 1.0f));
					uc.weight += 1.0f;
					uniqueColors[j] = uc;
					match = true;
					break;
				}
			}
			
			// otherwise push
			if (match) continue;
			
			WeightedColor c;
			c.color = pixels[i];
			c.weight = 1.0f;
			uniqueColors.Add(c);
		}
		return uniqueColors;
	}
	
	public static Color[] GeneratePalette(Color[] source, float tolerance)
	{
		var uniqueColors = ComputeWeightedColors (source, tolerance);
		
		if (uniqueColors.Count < 128)
		{
			Debug.LogWarning("Palette is less than 128 colors, tolerance factor could be lowered");
		}
		
		uniqueColors.Sort ((c1, c2) => c2.weight.CompareTo (c1.weight));
		
		Color[] paletteColors = new Color[128];
		
		int k = 0, len = Math.Min (128, uniqueColors.Count);
		for (; k < len; ++k) paletteColors [k] = uniqueColors [k].color.ToColor ();
		return paletteColors;
	}
	
	public static byte[] GenerateIndexed(Color[] inputColors, int numPx, Color[] paletteColors, float alphaThreshold)
	{
		currentOperation = "Generating indexed image";
		
		var paletteColorsLAB = Array.ConvertAll(paletteColors, x => new LABColor(x));
		var pixelBuffer = new byte[numPx];
		
		for (int i = 0; i <  numPx; ++i)
		{
			progress = (float)i / (float)(numPx - 1);
			// 7 bits for color index
			var index = GetIndex(paletteColorsLAB, new LABColor(inputColors[i]));
			// 1 bits for alpha
			byte alpha = (byte)(inputColors[i].a >= alphaThreshold ? 128 : 0);
			pixelBuffer[i] = (byte)(index + alpha);
		}
		return pixelBuffer;
	}
	
	[MenuItem ("Assets/Generate Indexed Color Sprite")]
	public static void ShowWindow ()
	{
		EditorWindow.GetWindow(typeof(IndexedColorEncoder));
	}
	
	private Texture2D source = null, clone = null, palette = null;
	private float tolerance = 5.0f, alphaThreshold = .8f;
	private Vector2 scrollPosition = Vector2.zero;
	private byte[] encodedPixels;
	private Color[] paletteColors;
	private bool isProcessing = false, isRenderingDone = true, paletteShouldBeSaved = false, indexShouldBeSaved = false, useBilinearShader = false, needReset = false, shouldExit = false;
	
	private enum ScheduledAction {None, ComputePalette, ComputeIndexed};
	private ScheduledAction currentAction = ScheduledAction.None;
	
	private void SavePalette(string path)
	{
		var palette = new Texture2D (16, 8, TextureFormat.RGB24, false);
		palette.SetPixels (paletteColors);
		palette.Apply ();
		
		File.WriteAllBytes (path, palette.EncodeToPNG ());
		AssetDatabase.ImportAsset (path);
		
		// Parameterize palette
		TextureImporter importer = AssetImporter.GetAtPath (path) as TextureImporter;
		importer.textureType = TextureImporterType.Advanced;
		importer.isReadable = true;
		importer.mipmapEnabled = false;
		importer.filterMode = FilterMode.Point;
		importer.textureFormat = TextureImporterFormat.AutomaticTruecolor;
		importer.SaveAndReimport ();
		isRenderingDone = true;
	}
	
	private void SaveGenerated()
	{
		var encoded = new Texture2D(source.width, source.height, TextureFormat.Alpha8, false);
		encoded.LoadRawTextureData(encodedPixels);
		encoded.Apply();
		
		var path = GetEncodedPath (AssetDatabase.GetAssetPath(source));
		File.WriteAllBytes(path, encoded.EncodeToPNG());
		AssetDatabase.ImportAsset(path);
		
		// Parameterize encoded
		TextureImporter importer	= AssetImporter.GetAtPath( path ) as TextureImporter;
		importer.textureType        = TextureImporterType.Advanced;
		importer.mipmapEnabled      = false;
		importer.filterMode         = FilterMode.Point;
		importer.npotScale          = TextureImporterNPOTScale.None;
		importer.textureFormat      = TextureImporterFormat.Alpha8;
		importer.spriteImportMode   = SpriteImportMode.Single;
		
		// Copy source parameters
		TextureImporter sourceImporter 	= AssetImporter.GetAtPath( AssetDatabase.GetAssetPath(source) ) as TextureImporter;
		importer.spritePivot            = sourceImporter.spritePivot;
		importer.spriteBorder           = sourceImporter.spriteBorder;
		importer.spritePixelsPerUnit   	= sourceImporter.spritePixelsPerUnit;
		// Workaround, you have to use TextureImporterSettings to set sprite alignment
		TextureImporterSettings texSettings = new TextureImporterSettings();
		importer.ReadTextureSettings(texSettings);
		texSettings.spriteAlignment = (int)SpriteAlignment.Custom;
		importer.SetTextureSettings(texSettings);
		importer.SaveAndReimport ();
		
		// Generate Material
		var shaderName = useBilinearShader ? "Sprites/IndexedBilinear" : "Sprites/Indexed";
		var shader = Shader.Find(shaderName);
		if (shader == null)
		{
			Debug.Log("Could not find shader [" + shaderName + "], material generation failed.");
			return;
		}
		Material material = new Material (shader);
		material.SetTexture ("_Palette", palette);
		// Bilinear shader needs to know about texture size
		if (useBilinearShader)
		{
			material.SetFloat("_Width", (float)source.width);
			material.SetFloat("_Height", (float)source.height);
		}
		AssetDatabase.CreateAsset (material, GetMaterialPath(AssetDatabase.GetAssetPath(source)));
		isRenderingDone = true;
		shouldExit = true;
	}

	private void ReadPalette()
	{
		if (source == null) return;
		var palettePath = GetPalettePath (AssetDatabase.GetAssetPath(source));
		var obj = AssetDatabase.LoadAssetAtPath (palettePath, typeof(Texture2D));
		if (obj != null) palette = obj as Texture2D;
	}
	
	private void ReadSource()
	{
		source = Selection.activeObject != null && Selection.activeObject is Texture2D ? (Texture2D)Selection.activeObject : null;
		
		if (source == null) return;
		
		TextureImporterSettings settings = new TextureImporterSettings();
		
		// make sure we can read the texture
		var sourcePath = AssetDatabase.GetAssetPath (source);
		var importer = AssetImporter.GetAtPath (sourcePath) as TextureImporter;
		importer.textureType = TextureImporterType.Advanced;
		importer.isReadable = true;
		importer.SaveAndReimport ();
		importer.ReadTextureSettings(settings);
		
		source = AssetDatabase.LoadAssetAtPath(sourcePath, typeof(Texture2D)) as Texture2D;
		
		if (clone == null)
		{
			var cloneTmp = new Texture2D(source.width, source.height);
			cloneTmp.SetPixels(source.GetPixels());
			
			var scale = (float)(256 * 256) / (float)(source.width * source.height);
			if (scale < 1.0f)
			{
				TextureScale.Bilinear(cloneTmp, (int)(cloneTmp.width * scale), (int)(cloneTmp.height * scale));
			}
			cloneTmp.Apply();
			
			var clonePath = GetTmpPath(AssetDatabase.GetAssetPath(source));
			AssetDatabase.DeleteAsset(clonePath);
			File.WriteAllBytes(clonePath, cloneTmp.EncodeToPNG());
			AssetDatabase.ImportAsset(clonePath);
			
			// make sure we can read the texture
			TextureImporter cloneImporter = AssetImporter.GetAtPath (clonePath) as TextureImporter;
			cloneImporter.SetTextureSettings(settings);
			cloneImporter.SaveAndReimport();
			
			clone = AssetDatabase.LoadAssetAtPath(clonePath, typeof(Texture2D)) as Texture2D;
		}
	}
	
	private void Reset()
	{
		if (clone != null)
		{
			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(clone));
			clone = null;
		}
		palette = null;
		source = null;
		isProcessing = false;
		isRenderingDone = true;
		paletteShouldBeSaved = false;
		indexShouldBeSaved = false;
		useBilinearShader = false;
		currentAction = ScheduledAction.None;
		AssetDatabase.Refresh();
	}

	private void OnEnable() { needReset = true; }

	private void OnDisable() 
	{ 
		EditorUtility.ClearProgressBar();
		Reset ();
	}

	private void OnSelectionChange()
	{
		var tmpSource = Selection.activeObject != null && Selection.activeObject is Texture2D ? (Texture2D)Selection.activeObject : null;
		if (tmpSource == null || tmpSource != source) needReset = true;
	}
	
	public void Update()
	{
		if (isRenderingDone) isProcessing = false;
		
		if (isProcessing) EditorUtility.DisplayProgressBar("Indexed Color Encoder", currentOperation, progress);
		else EditorUtility.ClearProgressBar();

		if (shouldExit)
		{
			Close ();
			return;
		}

		if (needReset)
		{
			Reset();
			ReadSource();
			needReset = false;
			return;
		}
		
		if (paletteShouldBeSaved)
		{
			SavePalette(GetPalettePath(AssetDatabase.GetAssetPath(source)));
			paletteShouldBeSaved = false;
		}
		
		if (indexShouldBeSaved)
		{
			SaveGenerated();
			indexShouldBeSaved = false;
		}
		
		ReadPalette();
		
		if (currentAction == ScheduledAction.ComputePalette)
		{
			currentAction = ScheduledAction.None;
			
			var pixels = clone.GetPixels(0);
			ThreadPool.QueueUserWorkItem(GeneratePaletteTask =>
			                             {
				paletteColors = GeneratePalette(pixels, tolerance);
				paletteShouldBeSaved = true;
			});
		}
		else if (currentAction == ScheduledAction.ComputeIndexed)
		{
			currentAction = ScheduledAction.None;
			
			var paletteColors = palette.GetPixels(0);
			var inputColors = source.GetPixels (0);
			var numPx = source.width * source.height;
			
			ThreadPool.QueueUserWorkItem(GenerateIndexedTask =>
			                             {
				encodedPixels = GenerateIndexed(inputColors, numPx, paletteColors, alphaThreshold);
				indexShouldBeSaved = true;
			});
		}
	}
	
	private void OnGUI ()
	{
		GUILayout.Label ("Indexed Color Sprite Generation", EditorStyles.boldLabel);
		
		scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
		
		GUILayout.Label ("Tolerance Factor: " + tolerance);
		tolerance = GUILayout.HorizontalSlider(tolerance, .0f, 10.0f);
		
		GUILayout.Label ("Alpha Threshold: " + alphaThreshold);
		alphaThreshold = GUILayout.HorizontalSlider(alphaThreshold, .0f, 1.0f);
		
		useBilinearShader = GUILayout.Toggle(useBilinearShader, "Use Bilinear Filtering");
		
		if (GUILayout.Button ("Generate Palette") && clone != null && !isProcessing)
		{
			isProcessing = true;
			isRenderingDone = false;
			currentAction = ScheduledAction.ComputePalette;
		}
		
		if (palette != null)
		{
			GUILayout.Label("Palette");
			GUI.DrawTexture(GUILayoutUtility.GetRect(256, 128), palette);
			
			if (GUILayout.Button ("Generate Indexed") && source != null && !isProcessing)
			{
				isProcessing = true;
				isRenderingDone = false;
				currentAction = ScheduledAction.ComputeIndexed;
			}
		}
		GUILayout.EndScrollView();
	}
}