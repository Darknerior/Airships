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

    public static Quaternion SafeFromToRotation(Vector3 fromVector, Vector3 toVector, Transform rotObject)
    {
        if (Vector3.Dot(fromVector, toVector) < -0.99999f)
        {
            Vector3 axis = Vector3.Cross(rotObject.up, fromVector).normalized;
            if (Mathf.Approximately(axis.magnitude, 0)) axis = Vector3.Cross(rotObject.right, fromVector).normalized;
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
}
