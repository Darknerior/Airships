using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PlacementUtils
{
    public readonly static Vector3 collisionPadding = new Vector3(-0.002f, -0.002f, -0.002f);
    public readonly static Vector3 overlapPadding = new Vector3(0.002f, 0.002f, 0.002f);

    public static Vector3 GetOffset(BoxCollider collider, Vector3 hitNormal)
    {
        var center = collider.center * 2f; // If a collider is offset we adjust the offset accordingly (*2f because the added height is the offset *2)

        var extents = (collider.size - center) * 0.5f;

        // Adjust the placement position by half the placed objects size in the hits normal direction 
        var sideOffset = new Vector3(
            hitNormal.x * extents.x,
            hitNormal.y * extents.y,
            hitNormal.z * extents.z
        );

        return sideOffset;
    }

    public static RaycastHit GetPaddedHit(Vector3 from, Vector3 direction, float paddingRadius, float length, LayerMask layerMask)
    {
        if (!Physics.Raycast(from, direction, out RaycastHit hit, Mathf.Infinity, layerMask))
        {
            Collider[] overlaps = Physics.OverlapCapsule(from + direction * paddingRadius, from + direction * (length - paddingRadius), paddingRadius, layerMask);
            if (overlaps.Length > 0)
            {
                float closestDistance = float.MaxValue;

                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider block = overlaps[i];
                    Vector3 blockPosition = block.bounds.center;
                    Vector3 camToBlock = blockPosition - from;
                    Vector3 projectedOnRay = Vector3.Project(camToBlock, direction);
                    Debug.DrawRay(from, projectedOnRay, Color.yellow);
                    Vector3 lineIntersection = from + projectedOnRay;
                    float distanceFromRay = Vector3.Distance(lineIntersection, blockPosition);
                    float closer = Mathf.Min(closestDistance, distanceFromRay);
                    if (closer < closestDistance && Physics.Raycast(lineIntersection, block.transform.position - lineIntersection, out RaycastHit newHit, Mathf.Infinity, layerMask))
                    {
                        closestDistance = closer;
                        hit = newHit;
                    }
                }
            }
        }

        return hit;
    }

    public static int CalculateLocalFaceIdFromRotation(Quaternion rotation, Vector3 normal)
    {
        return FaceIdFromVector(Quaternion.Inverse(rotation) * normal);
    }

    public static int LayerMaskToLayer(LayerMask layerMask)
    {
        // Convert the LayerMask to the corresponding layer number
        int layerMaskValue = layerMask.value;
        int layer = 0;

        while (layerMaskValue > 1)
        {
            layerMaskValue >>= 1;
            layer++;
        }

        return layer;
    }

    public static Vector4 GetClosestEdgeData(RaycastHit hit, BoxCollider collider)
    {
        Vector3 localNormal = hit.collider.transform.InverseTransformDirection(hit.normal);
        Vector3 localPoint = hit.collider.transform.InverseTransformPoint(hit.point);

        Vector3 size = collider.size;
        Vector3 offset = GetOffset(collider, localNormal);

        Vector3 toPoint = localPoint - offset;

        toPoint = new Vector3(
            toPoint.x / size.x,
            toPoint.y / size.y,
            toPoint.z / size.z
        );

        float maxValue = Mathf.Max(Mathf.Abs(toPoint.x), Mathf.Max(Mathf.Abs(toPoint.y), Mathf.Abs(toPoint.z)));

        Vector3 inverseEdgeDistance = new Vector3(
            maxValue == Mathf.Abs(toPoint.x) ? toPoint.x : 0,
            maxValue == Mathf.Abs(toPoint.y) ? toPoint.y : 0,
            maxValue == Mathf.Abs(toPoint.z) ? toPoint.z : 0
        );

        return new Vector4(inverseEdgeDistance.x, inverseEdgeDistance.y, inverseEdgeDistance.z, maxValue);
    }

    public static int CalculateLocalFaceId(Vector3 normal, Transform refObject)
    {
        return FaceIdFromVector(refObject.InverseTransformDirection(normal));
    }

    public static int FaceIdFromVector(Vector3 normal)
    {
        Vector3[] vectors = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0) };

        for (int i = 0; i < 6; i++)
        {
            if (vectors[i] == normal) return i + 1;
        }

        return 0;
    }

    public static Vector3[] GetAdjacentVectors(Vector3 localNormal)
    {
        if (localNormal.magnitude != 1) return null;

        Vector3[] surroundingVectors = new Vector3[4];
        int counter = 0;
        for (int i = 1; i <= 6; i++)
        {
            Vector3 vector = FaceIdToVector(i);
            if (vector != localNormal && vector != -localNormal)
            {
                surroundingVectors[counter] = vector;
                counter++;
            }
        }

        return surroundingVectors;
    }

    public static Vector3 FaceIdToVector(int faceId)
    {
        Vector3[] vectors = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0) };
        return vectors[faceId - 1];
    }

    public static Vector3 LocalFaceIdToVector(int faceId, Transform refObject)
    {
        Vector3[] vectors = { refObject.forward, -refObject.forward, -refObject.right, refObject.right, refObject.up, -refObject.up };
        return vectors[faceId - 1];
    }

    public static Quaternion SafeFromToRotation(Vector3 fromVector, Vector3 toVector)
    {
        if (Vector3.Dot(fromVector, toVector) < -0.99999f)
        {
            Vector3 axis = Vector3.Cross(toVector, fromVector).normalized;
            if (Mathf.Approximately(axis.magnitude, 0)) return Quaternion.identity;
            return Quaternion.AngleAxis(180f, axis);
        }
        else
        {
            return Quaternion.FromToRotation(fromVector, toVector);
        }
    }

    public static Collider[] GetCollisionsFromPoint(Vector3 position, Vector3 extents, Vector3 padding, Quaternion rotation, LayerMask layerMask)
    {
        return Physics.OverlapBox(position, extents * 0.5f + padding, rotation, layerMask);
    }

    public static Collider[] GetOverlaps(GameObject checkObject, BoxCollider boxCollider, Vector3 padding, LayerMask layerMask)
    {
        Collider[] overlaps = Physics.OverlapBox(checkObject.transform.TransformPoint(boxCollider.center), boxCollider.size / 2 + padding, checkObject.transform.rotation, layerMask);
        List<Collider> finalOverlaps = new();

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i].transform == checkObject.transform) continue;
            finalOverlaps.Add(overlaps[i]);
        }

        return finalOverlaps.ToArray();
    }

    public static Vector3 GetBiasedLocalSnapPoint(RaycastHit hit, BoxCollider hitCollider, float bias, float angleFactor)
    {
        Vector4 edgeData = GetClosestEdgeData(hit, hitCollider);

        Vector3 inverseEdgeDistance = edgeData;
        float maxValue = edgeData.w;

        float CloseToAngleFactor = Vector3.Angle(inverseEdgeDistance.normalized, Camera.main.transform.forward) % 180f / 180f;

        if (0.5f - maxValue > bias || CloseToAngleFactor < angleFactor)
        {
            inverseEdgeDistance = hit.collider.transform.InverseTransformDirection(hit.normal);
        }

        return inverseEdgeDistance.normalized;
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
