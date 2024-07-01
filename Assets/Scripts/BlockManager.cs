using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public BlockType[] blockTypes;
    public Dictionary<GameObject, Block> blocks; // Integer is the Id of the block
    private Dictionary<Transform, Rigidbody> activeBodies;

    public void Initialize()
    {
        blocks = new();
        activeBodies = new();
    }

    private void AttachBlocks(GameObject attach1, GameObject attach2, int attach1Id, int attach2Id)
    {
        SetFace(attach1, attach1Id, attach2);
        SetFace(attach2, attach2Id, attach1);
    }

    public void CombineAdjacentBlocks(GameObject checkObject, Collider[] surroundingObjects)
    {
        for (int i = 0; i < surroundingObjects.Length; i++)
        {
            Collider collider = surroundingObjects[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - checkObject.transform.position;
            int selfFaceId = PlacementUtils.CalculateLocalFaceId(objectDirection, checkObject.transform); // Check which face the object is near

            if (selfFaceId > 0) // Check if the object is directly on it
            {
                int attachFaceId = PlacementUtils.CalculateLocalFaceId(-objectDirection, colTransform);
                AttachBlocks(checkObject, colTransform.gameObject, selfFaceId, attachFaceId);

                if (colTransform.parent != checkObject.transform.parent)
                {
                    checkObject.transform.SetParent(colTransform.parent);
                    AddWeights(colTransform.parent, new List<GameObject> { checkObject });
                }
            }
        }

        if (checkObject.transform.parent == null)
        {
            CreateShip(new List<GameObject> { checkObject });
        }
    }
    
    private void ClearFaces(GameObject clearObject)
    {
        Block block = blocks[clearObject];
        // Remove the block from the objects it was attached to
        for (int i = 1; i <= 6; i++)
        {
            GameObject faceObject = block.GetFace(i);
            if (faceObject != null)
            {
                Block newBlock = blocks[faceObject];
                newBlock.DetachObject(clearObject);
                blocks[faceObject] = newBlock;
            }
        }

        for (int i = 1; i <= 6; i++)
        {
            block.InternalSetFace(i, null);
        }
        blocks[clearObject] = block; // Clear the block itself
        clearObject.transform.SetParent(null);
    }

    public void DetachBlock(GameObject removedObject)
    {
        RemoveWeights(removedObject.transform.parent, new List<GameObject>() { removedObject });
        Block block = blocks[removedObject];
        List<GameObject> attachedBlocks = new();
        // Remove the block from the objects it was attached to as well
        for (int i = 1; i <= 6; i++)
        {
            GameObject faceObject = block.GetFace(i);
            if (faceObject != null)
            {
                attachedBlocks.Add(faceObject);
            }
        }

        Transform baseParent = removedObject.transform.parent;
        ClearFaces(removedObject);
        List<List<GameObject>> ships = new();

        int biggestList = 0;
        int biggestListSize = 0;
        while (attachedBlocks.Count > 0)
        {
            GameObject baseBlock = attachedBlocks[0];
            attachedBlocks.Remove(baseBlock);
            List<GameObject> connectedBlocks = GraphTraversal(baseBlock, ref attachedBlocks); // Check if baseblock is attached to any of the pathfindblocks using clear position as a reference point
            ships.Add(connectedBlocks);
            if (connectedBlocks.Count > biggestListSize)
            {
                biggestList = ships.Count;
                biggestListSize = connectedBlocks.Count;
            }
        }

        if (biggestList != 0)
        {
            ships.RemoveAt(biggestList - 1);
        }

        foreach (List<GameObject> connectedBlocks in ships)
        {
            RemoveWeights(baseParent, connectedBlocks);
            CreateShip(connectedBlocks);
        }

        if (baseParent.childCount == 0)
        {
            DestroyShip(baseParent);
        }
    }

    private void SetFace(GameObject SetObject, int faceId, GameObject attachObject)
    {
        // Because a dictionary returns a copy of a struct you cant directly modify it
        Block block = blocks[SetObject];
        block.InternalSetFace(faceId, attachObject);
        blocks[SetObject] = block;
    }

    List<GameObject> GraphTraversal(GameObject baseBlock, ref List<GameObject> trackBlocks)
    { // Pathfind towards the referenceposition using while checking if the current block is part of trackblocks
        Queue<GameObject> traversalQueue = new();
        List<GameObject> connectedBlocks = new();

        traversalQueue.Enqueue(baseBlock);
        connectedBlocks.Add(baseBlock);

        while (traversalQueue.Count > 0)
        {
            GameObject blockObj = traversalQueue.Dequeue();
            Block block = blocks[blockObj];

            for (int i = 1; i <= 6; i++)
            {
                GameObject attachedBlock = block.GetFace(i);
                if (attachedBlock != null && !connectedBlocks.Contains(attachedBlock))
                {
                    traversalQueue.Enqueue(attachedBlock);
                    connectedBlocks.Add(attachedBlock);
                    if (trackBlocks.Contains(blockObj))
                    {
                        trackBlocks.Remove(blockObj);
                    }
                }
            }
        }

        return connectedBlocks;
    }

    private void RemoveWeights(Transform ship, List<GameObject> removeBlocks)
    {
        Rigidbody shipBody = activeBodies[ship];
        float totalMass = shipBody.mass;
        Vector3 weightedMass = shipBody.worldCenterOfMass * shipBody.mass;
        for (int i = 0; i < removeBlocks.Count; i++)
        {
            float weight = blockTypes[blocks[removeBlocks[i]].blockId].weight;
            totalMass -= weight;
            weightedMass -= removeBlocks[i].transform.position * weight;
        }

        shipBody.mass = totalMass;
        shipBody.centerOfMass = ship.InverseTransformPoint(weightedMass / totalMass);
        shipBody.WakeUp();
    }

    private void AddWeights(Transform ship, List<GameObject> addBlocks)
    {
        Rigidbody shipBody = activeBodies[ship];
        float totalMass = shipBody.mass;
        Vector3 weightedMass = shipBody.centerOfMass * totalMass;
        for (int i = 0; i < addBlocks.Count; i++)
        {
            float weight = blockTypes[blocks[addBlocks[i]].blockId].weight;
            totalMass += weight;
            weightedMass += addBlocks[i].transform.localPosition * weight;
        }

        shipBody.mass = totalMass;
        shipBody.centerOfMass = weightedMass / totalMass;
        shipBody.WakeUp();
    }

    private GameObject CreateShip(List<GameObject> airshipBlocks)
    {
        GameObject airshipObject = new GameObject("Airship");
        Rigidbody airshipBody = airshipObject.AddComponent<Rigidbody>();
        airshipBody.mass = 0;
        activeBodies.Add(airshipObject.transform, airshipBody);
        ReParentBlocks(airshipObject.transform, airshipBlocks);
        AddWeights(airshipObject.transform, airshipBlocks);
        return airshipObject;
    }

    private void ReParentBlocks(Transform newParent, List<GameObject> reparentBlocks)
    {
        for (int i = 0; i < reparentBlocks.Count; i++)
        {
            reparentBlocks[i].transform.SetParent(newParent);
        }
    }

    private void DestroyShip(Transform ship)
    {
        activeBodies.Remove(ship);
        Destroy(ship.gameObject);
    }

    private void DestroyBlock(GameObject destroyBlock)
    {
        DetachBlock(destroyBlock);
        blocks.Remove(destroyBlock);
        Destroy(destroyBlock);
    }

    public int GetBlockId(GameObject block)
    {
        return blocks[block].blockId;
    }

    public BlockType GetBlockType(GameObject block)
    {
        return blockTypes[blocks[block].blockId];
    }

    public BlockType GetBlockTypeFromId(int blockId)
    {
        return blockTypes[blockId];
    }

    public GameObject CreateBlock(int blockId, Vector3 position, Quaternion rotation, LayerMask layer)
    {
        GameObject blockObject = Instantiate(GetBlockTypeFromId(blockId).blockPrefab, position, rotation);
        blockObject.layer = layer;
        Block block = new Block() { blockId = blockId };
        blocks.Add(blockObject, block);
        return blockObject;
    }
}

public struct Block
{
    public int blockId; // Id of this block
    public GameObject front; // FaceId 1-6
    public GameObject back;
    public GameObject left;
    public GameObject right;
    public GameObject up;
    public GameObject down;

    public void InternalSetFace(int faceId, GameObject attachedBlock)
    {
        switch (faceId)
        {
            case 1: front = attachedBlock; break;
            case 2: back = attachedBlock; break;
            case 3: left = attachedBlock; break;
            case 4: right = attachedBlock; break;
            case 5: up = attachedBlock; break;
            case 6: down = attachedBlock; break;
        }
    }

    public GameObject GetFace(int faceId)
    {
        switch (faceId)
        {
            case 1: return front;
            case 2: return back;
            case 3: return left;
            case 4: return right;
            case 5: return up;
            case 6: return down;
            default: return null;
        }
    }

    public void DetachObject(GameObject detachObject)
    {
        for (int i = 1; i <= 6; i++)
        {
            if (GetFace(i) == detachObject)
            {
                InternalSetFace(i, null);
                return;
            }
        }
    }
}

[System.Serializable]
public struct BlockType
{
    public float weight; // Weight of the block
    public GameObject blockPrefab; // The prefab for the block
    public BoxCollider collider;
    public Material material;

    // If this block can attach on that face
    public bool front;
    public bool back;
    public bool left;
    public bool right;
    public bool up;
    public bool down;

    public int attachmentPointCount()
    {
        int count = 0;
        for (int i = 1; i <= 6; i++)
        {
            if (CanAttach(i)) count++;
        }

        return count;
    }

    public bool CanAttach(int faceId)
    {
        switch (faceId)
        {
            case 1: return front;
            case 2: return back;
            case 3: return left;
            case 4: return right;
            case 5: return up;
            case 6: return down;
            default: return false;
        }
    }
}