
using UnityEngine;

public class BlueprintLoader : MonoBehaviour
{

    [Header("Blueprint PRefabs")]
    public GameObject[] blockPrefabs;

    [Header("Navigation")]
    public Navigation navigation;

    [Header("Orientation")]
    public float rotationOffset = 90f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

           


        for (int i = 0; i < BlueprintData.blocks.Count; i++)
        {
            if (i >= BlueprintData.prefabIndices.Count)
            break;

            int prefabIndex = BlueprintData.prefabIndices[i];

            if (prefabIndex < 0 || prefabIndex >= blockPrefabs.Length)
            continue;

            GameObject prefab = blockPrefabs[prefabIndex];

            if (prefab == null)
                continue;
            
                Quaternion rotation = i < BlueprintData.rotations.Count ? BlueprintData.rotations[i] : Quaternion.identity;
                GameObject block = Instantiate(prefab, BlueprintData.blocks[i], rotation);
                int obstacleLayer = LayerMask.NameToLayer("Obstacles");
                

                if (obstacleLayer >= 0)
                SetLayerRecursively(block, obstacleLayer);
                
        }

        if (navigation != null)
        navigation.RecalculatePath();

    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        SetLayerRecursively(child.gameObject, layer);
    }
}
