using UnityEngine;

public class WorldCreator : MonoBehaviour
{

    public LayerMask obstacleMask = 1 << 6;
    
    public Transform rocket;
    public Rigidbody rocketBody;
    public Navigation navigation;
    public Autonomy autonomy;
    public Propulsion propulsion;
    public Camera sceneCamera;

    public float fixedZ = 0f;
    public Vector3 blockSize = new Vector3(2f, 2f, 1f);
    public float gridSnap = 1f;

    [Header("Block Placement")]
    public int maxBlocks = 5;
    public float placementCooldown = 1f;

    private int blocksPlaced = 0;
    private float PlacementInterval = 0f;
     

    public Color[] blockColors =
    {
        Color.gray,
        new Color( 0.2f, 0.65f, 1f), new Color (1f, .45f, .15f)
    };

        private enum Mode { Build, Simulation }
        private Mode mode = Mode.Build;

        private int selectedAppearance;
        private Vector3 launchPosition;
        private Quaternion launchRotation;

        void Start()
    {
        if (sceneCamera == null)
        sceneCamera = Camera.main;

        if (navigation != null)
        {
            navigation.obstacleMask = obstacleMask;
        }
        if (rocket != null)
        {
            launchPosition = rocket.position;
            launchRotation = rocket.rotation;

            if (rocketBody == null)
            rocketBody = rocket.GetComponent<Rigidbody>();
        
        }
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (mode != Mode.Build) return;
        if (sceneCamera == null) return;

        if (Input.GetMouseButton(0) && Input.mousePosition.x > 230f && Input.mousePosition.y < Screen.height - 160f && blocksPlaced < maxBlocks && Time.unscaledTime >= PlacementInterval)
    
        {
            PlaceBlock();

            blocksPlaced++;
            PlacementInterval = Time.unscaledTime + placementCooldown;
        }
    }

    void PlaceBlock()
    {
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);

        Plane buildPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, fixedZ));

        if(!buildPlane.Raycast(ray, out float distance))
        return;

        Vector3 position = ray.GetPoint(distance);

    if (gridSnap > 0f)
        {
            position.x = Mathf.Round(position.x / gridSnap) * gridSnap; position.y = Mathf.Round(position.y / gridSnap) * gridSnap;
            position.z = fixedZ;
        }
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);

        block.name = "Obstacle";
        block.transform.position = position;
        block.transform.localScale = blockSize;

        Renderer blockRenderer = block.GetComponent<Renderer>();

        if (blockColors != null && blockColors.Length > 0)
        {
            blockRenderer.material.color = blockColors[selectedAppearance];
            
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacles");
        if (obstacleLayer >= 0)
        
        {
            block.layer = obstacleLayer;
        }
        else
        {
            Debug.LogError("Create the Layer of Obstacles in Settings.");
        }
        
    }        

    void StartSimulation()
    {
        if (rocket == null || navigation == null || autonomy == null)
        {
            Debug.LogError("Assign Rocket, Navigation and Autonomy,");
            return;

        }

        mode = Mode.Simulation;

        Time.timeScale = 1f;

        if (propulsion != null)
        propulsion.autopilotEnabled = true;

        autonomy.enabled = true;
        navigation.RecalculatePath();

    }

    void RestartSimulation()
    {
        if (rocket == null) return;
        if (rocketBody != null)
        {
            rocketBody.linearVelocity = Vector3.zero;
            rocketBody.angularVelocity = Vector3.zero;
            rocketBody.position = launchPosition;
            rocketBody.rotation = launchRotation;


    }
    else
        
            rocket.SetPositionAndRotation(launchPosition, launchRotation);
            
        

        if (autonomy != null)
        {
            autonomy.initialized = false;

        }
        navigation?.RecalculatePath();
        mode = Mode.Simulation;
    
    }
    
    void ReturnToBuildMode()
    {
        mode = Mode.Build;

        Time.timeScale = 0f;

    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 20, 210, 300), GUI.skin.box);

        GUILayout.Label("WORLD CREATOR");

        GUILayout.Label("Mode: " + mode);

        if (mode == Mode.Build)
        {
            GUILayout.Label("Select block:");

            if (blockColors != null)
            {
                for (int i = 0; i < blockColors.Length; i++)
                {
                    GUI.backgroundColor = blockColors[i];

                    if (GUILayout.Button(
                    "Appearance " + (i + 1)))
                    {
                        selectedAppearance = i;

                    }
                }
                GUI.backgroundColor = Color.white;
            }
            GUILayout.Space(8);
            GUILayout.Label("Click in the world to place the blocks.");

            if (GUILayout.Button("COMMENCE FLIGHT"))
            StartSimulation();

        }
        else
        {
            if (GUILayout.Button("RESTART SIMULATION"))
            RestartSimulation();

            if (GUILayout.Button("RETURN TO BUILD MODE"))
            ReturnToBuildMode();
        }
        GUILayout.EndArea();       
    }
}