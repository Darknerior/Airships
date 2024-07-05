using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WiringManager : MonoBehaviour
{
    public WireType[] wireTypes;

    public void Initialize()
    {

    }

    public void ConnectWires()
    {

    }

    public WireType GetWireTypeFromId(int id)
    {
        return wireTypes[id];
    }
}

public struct Wire
{

};

[System.Serializable]
public struct WireType
{
    public GameObject wirePrefab;
    public Material material;
};
