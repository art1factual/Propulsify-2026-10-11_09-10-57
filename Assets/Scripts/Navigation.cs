using System.Collections.Generic;
using UnityEngine;


public class Navigation : MonoBehaviour
{

    public Transform landingTarget;

    public Vector2 areaSize = new Vector2(100f, 60f);
    public float cellSize = 2f;
    public float fixedz = 0f;

    public LayerMask obstactleMask;
    public float obstacleClearance = 1.5f;

    [Header("Debug")]
    public bool showPath = true;
    public List<Vector3> Path { get; private set; } = new List<Vector3>();

    
    class Node
    {
        public int x, y;
        public float g, h;
        public Node parent;

        public float F => g + h;
        public Node(int x, int y)
        {
            this.x = x;
            this.y = y;
        
        }
        
    }
 
    bool[,] blocked;
    Vector3 origin;

    bool originInitialized;
    int width, height;


 
    void Start()
    {
        if (landingTarget == null)
        {
            Debug.LogError("Landing Target.", this);
            enabled = false;
            return;
        }

        BuildGrid();

        if(!FindPath(transform.position, landingTarget.position))
        {
            Debug.LogWarning("Empty Path.", this);
            
        }
    }

    public void RecalculatePath()
    {
        if(landingTarget == null)
        return;

        Path.Clear();

        BuildGrid();

        if (!FindPath(transform.position, landingTarget.position))
        {
            Debug.LogWarning("No Valid path was found", this);
        }
    }



    void BuildGrid()
    {
        width = Mathf.CeilToInt(areaSize.x / cellSize);
        height = Mathf.CeilToInt(areaSize.y / cellSize);

        if (!originInitialized)
        {
            origin = new Vector3(transform.position.x - areaSize.x / 2f, transform.position.y - areaSize.y / 2f, fixedz);
            originInitialized = true;
        }
        blocked = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 point = GridToWorld(x, y);
                blocked[x, y] = Physics.CheckSphere(point, obstacleClearance, obstactleMask, QueryTriggerInteraction.Ignore);
            }
        }
    
    }

    Vector3 GridToWorld(int x, int y)
    {
        return origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
    }

    Vector2Int WorldToGrid(Vector3 point)
    {
        return new Vector2Int(
            Mathf.FloorToInt((point.x - origin.x) / cellSize),
            Mathf.FloorToInt((point.y - origin.y) / cellSize)
        );
    }

    bool Inside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;

    }

    bool FindPath(Vector3 start, Vector3 goal)
    {
        Vector2Int s = WorldToGrid(start);
        Vector2Int t = WorldToGrid(goal);

        if (!Inside(s.x, s.y) || !Inside(t.x, t.y))
        {
            Debug.LogWarning("Outside Search Area");
            return false;

        }

        blocked[s.x, s.y] = false;
        if (blocked[t.x, t.y])
        {
            Debug.LogWarning("Target is Blocked.", this);
            return false;
        }


        var open = new List<Node>();
        var all = new Dictionary<Vector2Int, Node>();
        var closed = new HashSet<Vector2Int>();

        Node startNode = new Node(s.x, s.y);
        startNode.h = Heuristic(s.x, s.y, t.x, t.y);

        open.Add(startNode);
        all[new Vector2Int(s.x, s.y)] = startNode;

        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1, };
        int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        while (open.Count > 0)
        {
            open.Sort((a, b) => a.F.CompareTo(b.F));

            Node current = open[0];
            open.RemoveAt(0);

            Vector2Int currentKey = new Vector2Int(current.x, current.y);

            if (current.x == t.x && current.y == t.y)
            {
                ReconstructPath(current);
                return true;
            }

            closed.Add(currentKey);

            for (int i = 0; i < 8; i++)
            {
                int nx = current.x + dx[i];
                int ny = current.y + dy[i];

                if (!Inside(nx, ny) || blocked[nx, ny])
                continue;

                if (dx[i] != 0 && dy[i] != 0)
                {
                    if (blocked[current.x + dx[i],  current.y] || blocked[current.x, current.y + dy[i]])
                    continue;
                }

                Vector2Int key = new Vector2Int(nx, ny);

                if(closed.Contains(key))
                continue;

                float stepCost = dx[i] != 0 && dy[i] != 0 ? 1.4142f : 1f;

                float newG = current.g + stepCost;

                if(!all.TryGetValue(key, out Node neighbor))
                {
                    neighbor = new Node(nx, ny);
                    all[key] = neighbor;
                }
                else if (newG >= neighbor.g)
                {
                    continue;
                }

                neighbor.g = newG;
                neighbor.h = Heuristic(nx, ny, t.x, t.y);
                neighbor.parent = current;

                if (!open.Contains(neighbor))
                open.Add(neighbor);
            }
        }
        return false;
    
    }  

    float Heuristic(int x, int y, int tx, int ty)
    {
        return Mathf.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty)
        );
    }

    void ReconstructPath (Node node)
    {
        Path.Clear();
         
        while (node != null)
        {
            Path.Add(GridToWorld(node.x, node.y));
            node = node.parent;
        }
        Path.Reverse();
        SimplifyPath();
        
    }  

    void SimplifyPath()
    {
        if (Path.Count < 3)
        return;

        List<Vector3> originalPath = new List<Vector3>(Path);
        List<Vector3> simplifiedPath = new List<Vector3>();

        int current = 0;
        simplifiedPath.Add(originalPath[current]);

        while (current < originalPath.Count - 1);
        {
            int furthest = originalPath.Count - 1;

            while (furthest > current + 1 && !HasLineOfSight(originalPath[current], originalPath[furthest]))
            {
                furthest--;

            }
            simplifiedPath.Add(originalPath[furthest]);
            current = furthest;
        }

        Path.Clear();
        Path.AddRange(simplifiedPath);
    }

    bool HasLineOfSight(Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);

        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (cellSize * 0.25f)));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 point = Vector3.Lerp(start, end, t);

            Vector2Int cell = WorldToGrid(point);

            if (!Inside(cell.x, cell.y))
            return false;

            if (blocked[cell.x, cell.y])
            return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (!showPath || Path == null || Path.Count < 2)
        return;
        
        Gizmos.color = Color.cyan;

        for (int i = 0; i < Path.Count - 1; i++)
        {
            Gizmos.DrawLine(Path[i], Path[i + 1]);
            Gizmos.DrawSphere(Path[i], 0.25f);
        }

        Gizmos.DrawSphere(Path[Path.Count - 1], 0.5f);
    }

}