using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using System.Diagnostics;

public class ParticleExporter : MonoBehaviour
{
    // Default folder name where you want the animations to be output

    public string folder = "PNG_Animations";

    // Framerate at which you want to play the animation

    // export frame rate 导出帧率，设置Time.captureFramerate会忽略真实时间，直接使用此帧率
    public int frameRate = 15;

    // export frame count 导出帧的数目，100帧则相当于导出5秒钟的光效时间。由于导出每一帧的时间很长，所以导出时间会远远长于直观的光效播放时间
    public float frameCount = 5;

    // public int screenWidth = 960;               // not use 暂时没用，希望可以直接设置屏幕的大小（即光效画布的大小）

    // public int screenHeight = 640;

    public Vector3 cameraPosition = Vector3.zero;

    public Vector3 cameraRotation = Vector3.zero;

    public Color RefColor = Color.black;

    private string realFolder = ""; // real folder where the output files will be

    private float originaltimescaleTime; // track the original time scale so we can freeze the animation between frames

    private float currentTime = 0;

    private bool over = false;

    private int currentIndex = 0;

    [SerializeField]
    private Camera exportCamera; // camera for export 导出光效的摄像机，使用RenderTexture

    public GameObject goCamera;

    public GraphicsFormat format;
    public TextureFormat tex2dFormat;
    private ParticleExporter scriptsPe;
    public int rtDep = 0;

    public void Awake()
    {
        init();
    }

    public void Start()
    {
    }

    void OnEnable()
    {
        init();
    }

    void init()
    {
        StopAllCoroutines();
        if (format == GraphicsFormat.None)
        {
            format = GraphicsFormat.R8G8B8A8_SRGB;
        }
        if (tex2dFormat == TextureFormat.Alpha8)
        {
            tex2dFormat = TextureFormat.BGRA32;
        }
        if (exportCamera == null)
        {
            GameObject go = Instantiate(goCamera) as GameObject;
            exportCamera = go.GetComponent<Camera>();
        }
        this.scriptsPe = this.gameObject.GetComponent<ParticleExporter>();

        // set frame rate
        Time.captureFramerate = frameRate;
        // Create a folder that doesn't exist yet. Append number if necessary.
        realFolder = Path.Combine(folder, name);
        // Create the folder
        if (!Directory.Exists(realFolder))
        {
            Directory.CreateDirectory(realFolder);
        }
        originaltimescaleTime = Time.timeScale;
        ///GameObject goCamera = Camera.main.gameObject;
        if (cameraPosition != Vector3.zero)
        {
            goCamera.transform.position = cameraPosition;
        }
        if (cameraRotation != Vector3.zero)
        {
            goCamera.transform.rotation = Quaternion.Euler(cameraRotation);
        }
        currentTime = 0;
    }

    void Update()
    {
        // cameraPosition=goCamera.transform.position;
        // cameraRotation=goCamera.transform.localEulerAngles;
        // GameObject go = Instantiate(goCamera) as GameObject;
        // exportCamera = go.GetComponent<Camera>();
        currentTime += Time.deltaTime;
        if (!over && currentIndex >= frameCount)
        {
            over = true;
            Cleanup();
            Debug.Log("Finish");
            return;
        }
        // 每帧截屏
        StartCoroutine(CaptureFrame());
    }

    void Cleanup()
    {
        DestroyImmediate(exportCamera.gameObject);
        // DestroyImmediate(gameObject);
        this.scriptsPe.enabled = false;
        this.exportCamera = null;
    }

    IEnumerator CaptureFrame()
    {
        // Stop time
        Time.timeScale = 0;
        // Yield to next frame and then start the rendering
        // this is important, otherwise will have error
        yield return new WaitForEndOfFrame();
        string filename = String.Format("{0}/{1:D04}.png", realFolder, ++currentIndex);
        Debug.Log(filename);
        int width = Screen.width;
        int height = Screen.height;
        //Initialize and render textures
        RenderTexture blackCamRenderTexture = RenderTexture.GetTemporary(
            width,
            height,
            rtDep,
            format
        );
        RenderTexture whiteCamRenderTexture = RenderTexture.GetTemporary(
            width,
            height,
            rtDep,
            format
        );
        RenderTexture refCamRenderTexture = RenderTexture.GetTemporary(
            width,
            height,
            rtDep,
            format
        );
        exportCamera.targetTexture = blackCamRenderTexture;
        exportCamera.backgroundColor = Color.black;
        exportCamera.Render();
        RenderTexture.active = blackCamRenderTexture;
        Texture2D texb = GetTex2D();

        //Now do it for Alpha Camera
        exportCamera.targetTexture = whiteCamRenderTexture;
        exportCamera.backgroundColor = Color.white;
        exportCamera.Render();
        RenderTexture.active = whiteCamRenderTexture;
        Texture2D texw = GetTex2D();
        exportCamera.targetTexture = refCamRenderTexture;
        exportCamera.backgroundColor = RefColor;
        exportCamera.Render();
        RenderTexture.active = refCamRenderTexture;
        Texture2D texr = GetTex2D();

        // If we have both textures then create final output texture
        if (texw && texb)
        {
            Texture2D outputtex = new Texture2D(width, height, tex2dFormat, false);

            // we need to check alpha ourselves,because particle use additive shader

            // Create Alpha from the difference between black and white camera renders
            for (int y = 0; y < outputtex.height; ++y)
            {
                // each row
                for (int x = 0; x < outputtex.width; ++x)
                {
                    // each column
                    float alpha;
                    alpha = texw.GetPixel(x, y).r - texb.GetPixel(x, y).r;
                    alpha = 1.0f - alpha;
                    Color color;
                    if (alpha == 0)
                    {
                        color = Color.clear;
                    }
                    else
                    {
                        color = texr.GetPixel(x, y);
                    }
                    color.a = alpha;
                    outputtex.SetPixel(x, y, color);
                }
            }

            // Encode the resulting output texture to a byte array then write to the file
            byte[] pngShot = outputtex.EncodeToPNG();
            File.WriteAllBytes(filename, pngShot);

            // cleanup, otherwise will memory leak
            pngShot = null;
            RenderTexture.active = null;
            DestroyImmediate(outputtex);
            outputtex = null;

            //DestroyImmediate(blackCamRenderTexture);
            blackCamRenderTexture = null;

            //DestroyImmediate(whiteCamRenderTexture);
            whiteCamRenderTexture = null;
            refCamRenderTexture = null;
            DestroyImmediate(texb);
            texb = null;
            DestroyImmediate(texw);
            texw = null;
            DestroyImmediate(texr);
            texr = null;
            System.GC.Collect();

            // Reset the time scale, then move on to the next frame.
            Time.timeScale = originaltimescaleTime;
        }
    }

    // Get the texture from the screen, render all or only half of the camera

    private Texture2D GetTex2D()
    {
        // Create a texture the size of the screen, RGB24 format
        int width = Screen.width;
        int height = Screen.height;
        Texture2D tex = new Texture2D(width, height, tex2dFormat, false);

        // Read screen contents into the texture
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        return tex;
    }
}