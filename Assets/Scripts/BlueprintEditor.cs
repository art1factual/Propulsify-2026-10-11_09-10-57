using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;



public class BlueprintEditor : MonoBehaviour
{

    [Header("Grid Settings")]
    public int gridWidth = 9;
    public int gridHeight = 7;
    public float cellSize = 2f;
    public Vector2 gridOrigin = new Vector2(-8f, -6f);
    public float placementZ = 0f;

    [Header("Prefab Settings")]
    public GameObject[] blockPrefabs;
    public int selectedPrefabIndex = 0;
    public int maxBlocks = 5;

    [Header("UI")]
    public GameObject mainPanel;
    public GameObject prefabPanel;
    public GameObject settingsPanel;

    [Header("UI Buttons")]
    public Button commenceFlightButton;
    public Button clearButton;
    public Button nextPrefabButton;

    [Header("UI Images")]
    public Image selectedPrefabIcon;
    public Sprite[] prefabIcons;

    private List<GameObject> placedBlocks = new();
    private List<int> placedPrefabIndices = new();

    [Header("Scene Settings")]
    public string simulationScene = "DroneTest";
    public Camera editorCamera;

    [Header("UI Text")]
    public TMP_Text blockCountText;
    public TMP_Text selectedPrefabText;
    public TMP_Text statusText;

    [Header("UI Buttons")]
    public Button previousPrefabButton;    

    [Header("Prefab Rotation")]
    public float currentRotation = 0f;
    public float rotationStep = 90f;

    [Header("Pipe Connector")]
    public float snapDistance = 1f;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(editorCamera == null)
        editorCamera = Camera.main;

        if(commenceFlightButton != null)
        commenceFlightButton.onClick.AddListener(CommenceFlight);

        if(clearButton != null)
        clearButton.onClick.AddListener(ClearBlueprint);

        if(nextPrefabButton != null)
        nextPrefabButton.onClick.AddListener(NextPrefab);

        if(previousPrefabButton != null)
        previousPrefabButton.onClick.AddListener(PreviousPrefab);

        UpdateUI();
    }
    public void RotatePipe()
    {
        currentRotation = (currentRotation + 90f) % 360f;
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.R))
        RotatePipe();
        if (cellSize <= 0f)
        return;
        if (!Input.GetMouseButtonDown(0) || editorCamera == null)
        return;

        if(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        return;

        Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, placementZ));

        if (!plane.Raycast(ray, out float distance))
        return;
        
        Vector3 point = ray.GetPoint(distance);

        int x = Mathf.RoundToInt((point.x - gridOrigin.x) / cellSize);

        int y = Mathf.RoundToInt((point.y - gridOrigin.y) / cellSize);
        

        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
        return;

        Vector3 position = new Vector3(gridOrigin.x + x * cellSize, gridOrigin.y + y * cellSize, placementZ);

        for (int i = 0; i < placedBlocks.Count; i++)
        {
            if (Vector3.Distance(placedBlocks[i].transform.position, position) < 0.01f)
            {
                Destroy(placedBlocks[i]);
                placedBlocks.RemoveAt(i);
                placedPrefabIndices.RemoveAt(i);
                UpdateUI();
                return;
            }
        }

        if (placedBlocks.Count >= maxBlocks)
        return;

        if(blockPrefabs == null || blockPrefabs.Length == 0) return;

        if (selectedPrefabIndex < 0 || selectedPrefabIndex >= blockPrefabs.Length)
        return;
        GameObject prefab = blockPrefabs[selectedPrefabIndex];

        if (prefab == null)
        return;

        GameObject block = Instantiate(prefab, position, Quaternion.Euler(currentRotation, 90f, 0f));

        SnapPipe(block);
        placedBlocks.Add(block);
        placedPrefabIndices.Add(selectedPrefabIndex);
        
        UpdateUI();

    }
    
    public void NextPrefab()
    {
        if (blockPrefabs == null || blockPrefabs.Length == 0)
        return;

        selectedPrefabIndex = (selectedPrefabIndex + 1) % blockPrefabs.Length;

        UpdateUI();
    }
    
    public void ClearBlueprint()
    {
        foreach (GameObject block in placedBlocks)
        Destroy(block);

        placedBlocks.Clear();
        placedPrefabIndices.Clear();

        UpdateUI();


    }
    
    public void PreviousPrefab()
    {
        if (blockPrefabs == null || blockPrefabs.Length == 0)
        return;

        selectedPrefabIndex = (selectedPrefabIndex - 1 + blockPrefabs.Length)
        % blockPrefabs.Length;

        UpdateUI();
    }
    
        public void CommenceFlight()
    {
        BlueprintData.blocks.Clear();
        BlueprintData.prefabIndices.Clear();
        BlueprintData.rotations.Clear();

        for (int i = 0; i < placedBlocks.Count; i++)
        {
            BlueprintData.blocks.Add(placedBlocks[i].transform.position);
            BlueprintData.prefabIndices.Add(placedPrefabIndices[i]);
            BlueprintData.rotations.Add(placedBlocks[i].transform.rotation);
        }

        SceneManager.LoadScene(simulationScene);
    }
    
    void UpdateUI()
    {
        if (blockCountText != null)
            blockCountText.text = $"{placedBlocks.Count} / {maxBlocks}";
        

        if (blockPrefabs != null && blockPrefabs.Length > 0 && selectedPrefabIndex >= 0 && selectedPrefabIndex < blockPrefabs.Length)
        {
            if (selectedPrefabText != null)
            selectedPrefabText.text = blockPrefabs[selectedPrefabIndex] != null ? blockPrefabs[selectedPrefabIndex].name : "None";

            
        

        if (selectedPrefabIcon != null && prefabIcons != null && selectedPrefabIndex < prefabIcons.Length)
        {
            selectedPrefabIcon.sprite = prefabIcons[selectedPrefabIndex];

        }
        if (statusText != null)
            statusText.text = "BLUEPRINT MODE";
    
        }
    }

    void SnapPipe(GameObject newPipe)
    {
        PipeConnector newConnector = newPipe.GetComponent<PipeConnector>();

        if(newConnector == null) return;

        float closestDistance = snapDistance;
        Vector3 bestOffset = Vector3.zero;
        bool foundConnection = false;

        foreach (GameObject existingPipe in placedBlocks)
        {
            if (existingPipe == null)
            continue;

            PipeConnector existingConnector = existingPipe.GetComponent<PipeConnector>();

            if (existingConnector == null)
            continue;

            foreach (Transform newPoint in newConnector.GetConnections())
            {
                if (newPoint == null)
                continue;

                foreach (Transform existingPoint in existingConnector.GetConnections())
                {
                    if (existingPoint == null) 
                    continue;
                    
                    float facing = Vector3.Dot(newPoint.up, existingPoint.up);

                    if (facing > -0.95f)
                    continue;

                    float distance = Vector3.Distance(newPoint.position, existingPoint.position);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;

                        bestOffset = existingPoint.position - newPoint.position;

                        foundConnection = true;
                    }
                }
                
            }
            
        }

        if (foundConnection)
        {
            newPipe.transform.position += bestOffset;
        }
    }

    void OnDrawGizmos()
    {
        if (cellSize <= 0f)
        return;

        Gizmos.color = Color.cyan;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 position = new Vector3(gridOrigin.x + x * cellSize, gridOrigin.y + y * cellSize, placementZ);

                Gizmos.DrawWireCube(position, new Vector3(cellSize, cellSize, 0f));
            }
        }
    }
}
