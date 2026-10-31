
using UnityEngine;

public class PipeConnector : MonoBehaviour
{
    public Transform connectionA;
    public Transform connectionB;

    public Transform[] GetConnections()
    {
        return new Transform[]
        {
            connectionA, connectionB
        };
    }
}
