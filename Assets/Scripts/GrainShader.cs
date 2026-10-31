using UnityEngine;
using UnityEngine.UI;


public class GrainShader : MonoBehaviour
{  

    public RawImage grainOverlay;

    [Range(0f, 1f)]

    public float intensity = 0.15f;

    public int textureSize = 256;

    public float refreshRate = 20f;

    private Texture2D noiseTexture;
    private Color32[] pixels;

    private float nextRefresh;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       noiseTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

       noiseTexture.filterMode = FilterMode.Point;
       noiseTexture.wrapMode = TextureWrapMode.Repeat;

       pixels = new Color32[textureSize * textureSize];

       grainOverlay.texture = noiseTexture;
       grainOverlay.color = Color.white; 
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.unscaledTime < nextRefresh)
        return;
        
        nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, refreshRate);

        for (int i = 0; i < pixels.Length; i++)
        {
            byte shade = (byte)Random.Range(0, 256);

            pixels[i] = new Color32(shade, shade, shade, (byte)(intensity * 255));
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply(false);

    }
    void OnDestroy()
    {
        if (noiseTexture != null)
        Destroy(noiseTexture);
    }
}
