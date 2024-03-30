using UnityEngine;
using System.Collections.Generic;
using System.ComponentModel.Design;

public class GridPlacement : MonoBehaviour
{
    public GameObject cubePrefab;
    private GameObject _previewCube;
    private Transform _airshipParent;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    private BoxCollider _boxCollider;

    private Dictionary<GameObject, Block> blocks; // Integer is the id of the block
    private List<Transform> activeAirships;

    struct Block {
        public GameObject front; // FaceID 1-6
        public GameObject back;
        public GameObject left;
        public GameObject right;
        public GameObject up;
        public GameObject down;

        public void SetFace(int faceID, GameObject attachedBlock)
        {
            switch (faceID)
            {
                case 1: front = attachedBlock; break;
                case 2: back = attachedBlock; break;
                case 3: left = attachedBlock; break;
                case 4: right = attachedBlock; break;
                case 5: up = attachedBlock; break;
                case 6: down = attachedBlock; break;
            }
        }
    }

    private void Start() {
        blocks = new();
        activeAirships = new();

        _previewCube = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
        SetPreviewOpacity(_previewCube, previewMaterial);
        _previewCube.SetActive(false);
        _boxCollider = _previewCube.GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;//Prevent player colliding with preview
    }

    private void Update() {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayer))
        {
            _previewCube.SetActive(true);

            // Adjust position to either ground hit or snap point
            var placementPosition = CalculatePlacementPosition(hit);

            _previewCube.transform.position = placementPosition;
            _previewCube.transform.rotation = Quaternion.identity; // Reset rotation or set to snap point rotation

            if (Input.GetMouseButtonDown(0)) // Left mouse click
            {
                // Instantiate the actual object
                Transform parent = _airshipParent;
                if (_airshipParent == null) {
                    parent = new GameObject("Airship").transform;
                    activeAirships.Add(parent);
                }

                Vector2Int faceID = CalculateFaceID(hit.normal, hit.transform);
                var placedObject = CreateBlock(cubePrefab, placementPosition, Vector3.zero, parent);
                blocks[placedObject].SetFace(faceID.x, hit.transform.gameObject); // Set the attached block for the newly placed block
                if (_airshipParent == parent) // If we place on the ground you cant change this
                    blocks[hit.transform.gameObject].SetFace(faceID.y, placedObject); // Set the attached block for the block the newly placed block is on
            }
        }
        else _previewCube.SetActive(false);
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit) {
        var placementPosition = new Vector3(0,0,0);
        
        // Check if hit object is part of the airship and find the closest snap point if applicable
        if (hit.collider.gameObject.layer == _airshipLayer) {
            _airshipParent = hit.collider.gameObject.transform.parent;
            var closestSnapPoint = GetClosestSnapPoint(hit.point, hit.collider.gameObject);
            if (closestSnapPoint != null)
            {
                // If a snap point is found, set pos
                placementPosition = closestSnapPoint.position;
                if (_previewCube.TryGetComponent(out Collider collider))
                {
                    var hitNormal = hit.normal.normalized;

                    // Determine if we are snapping to the top or the side based on the hit normal
                    var isTopSnap = Mathf.Approximately(hitNormal.y, 1f);

                    if (isTopSnap) {
                        // Top snap, adjust by half the height of the object
                        var heightOffset = collider.bounds.extents.y;
                        placementPosition += new Vector3(0, heightOffset, 0);
                    }
                    else {
                        // Side snap, adjust by the collider extents in the direction of the hit normal
                        // This finds the appropriate offset to move the preview object's side to the hit point's location.
                        var sideOffset = new Vector3(
                            hitNormal.x * collider.bounds.extents.x,
                            hitNormal.y * collider.bounds.extents.y,
                            hitNormal.z * collider.bounds.extents.z
                        );
                        placementPosition += sideOffset;
                        // If snapping to the side, you might also need to adjust vertically if your objects
                        // can be attached at different vertical positions (this depends on your game's mechanics)
                        if (!Mathf.Approximately(hitNormal.y, 0f)) placementPosition += new Vector3(0, collider.bounds.extents.y * hitNormal.y, 0);
                    }
                }
            }
        }
        else {
            _airshipParent = null;//We null airship parent as the ray is not returning airship layer
            placementPosition = hit.point;//Set the location to the point at the ground we are looking at
            if (!_previewCube.TryGetComponent(out Collider collider)) return placementPosition;
            var heightOffset = collider.bounds.extents.y;
            placementPosition += new Vector3(0, heightOffset, 0);//add height offset

        }

        return placementPosition;
    }


    private static Transform GetClosestSnapPoint(Vector3 hitPoint, GameObject hitObject) {
        Transform closestSnapPoint = null;
        var closestDistance = Mathf.Infinity;

        foreach (Transform child in hitObject.transform) {
            if (!child.CompareTag("SnapPoint")) continue;
            var distance = Vector3.Distance(hitPoint, child.position);
            if (!(distance < closestDistance)) continue;
            closestSnapPoint = child;
            closestDistance = distance;
        }

        return closestSnapPoint;
    }

    private static void SetPreviewOpacity(GameObject obj, Material previewMat) {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        var cloneMat = new Material(previewMat);
        renderer.material = cloneMat;
    }

    private GameObject CreateBlock(GameObject prefab, Vector3 position, Vector3 rotation, Transform parent)
    {
        GameObject blockObject = Instantiate(prefab, position, Quaternion.Euler(rotation), parent);
        blockObject.layer = _airshipLayer;
        Block block = new Block();
        blocks.Add(blockObject, block);
        return blockObject;
    }

    private Vector2Int CalculateFaceID(Vector3 normal, Transform hitObject)
    {
        float[] dotProductArray = new float[8];
        float maxDot = 0;

        dotProductArray[0] = Vector3.Dot(hitObject.forward, normal); // Front face
        dotProductArray[1] = Vector3.Dot(-hitObject.forward, normal); // Back face
        dotProductArray[2] = Vector3.Dot(-hitObject.right, normal); //  Left face
        dotProductArray[3] = Vector3.Dot(hitObject.right, normal); // Right face
        dotProductArray[4] = Vector3.Dot(hitObject.up, normal); // Upper face
        dotProductArray[5] = Vector3.Dot(-hitObject.up, normal); // Downwards face

        for (int i = 0; i < 6; i++) {
            // Get the largest dot product
            Mathf.Max(dotProductArray[i], maxDot);
        }

        // Return the face it is attached to and the inverse face
        if (maxDot == dotProductArray[0]) return new Vector2Int(1, 2);
        if (maxDot == dotProductArray[1]) return new Vector2Int(2, 1);
        if (maxDot == dotProductArray[2]) return new Vector2Int(3, 4);
        if (maxDot == dotProductArray[3]) return new Vector2Int(4, 3);
        if (maxDot == dotProductArray[4]) return new Vector2Int(5, 6);
        if (maxDot == dotProductArray[5]) return new Vector2Int(6, 5);
        return new Vector2Int(0, 0); // If its nothing return 0, 0 (not possible)
    }

    private void ShipCheck()
    {
        List<GameObject> blocksLeft = new();
        List<GameObject> shipParts = new();
        foreach (GameObject key in blocks.Keys) {
            blocksLeft.Add(key); // Get all blocks in a tracker list
        }

        int shipIndex = 0;
        int addedShips = 0;
        int shipsAvailable = activeAirships.Count;

        while (blocksLeft.Count > 0) {
            shipParts.Clear();
            GameObject block = blocksLeft[0];
            FloodFill(ref blocksLeft, ref shipParts, block);

            Transform parent = null;
            if (shipIndex < shipsAvailable)
                parent = activeAirships[shipIndex];
            else {
                parent = new GameObject("Airship").transform;
                activeAirships.Add(parent); // Using lists adding ad the end of the list here to maintain the used ships
            }

            for (int i = 0; i < shipParts.Count; i++) {
                shipParts[i].transform.SetParent(parent);
            }

            shipIndex++;
        }

        for (int i = shipIndex; i < shipIndex + addedShips; i++)
        {
            Transform removedShip = activeAirships[i];
            activeAirships.Remove(removedShip);
            Destroy(removedShip.gameObject); // Destroy the excess ship parents
        }
    }

    private void FloodFill(ref List<GameObject> blocksLeft, ref List<GameObject> shipParts, GameObject checkObject)
    {
        Block block = blocks[checkObject];
        blocksLeft.Remove(checkObject);
        shipParts.Add(checkObject);

        if (block.front != null) FloodFill(ref blocksLeft, ref shipParts, block.front);
        if (block.back != null) FloodFill(ref blocksLeft, ref shipParts, block.back);
        if (block.left != null) FloodFill(ref blocksLeft, ref shipParts, block.left);
        if (block.right != null) FloodFill(ref blocksLeft, ref shipParts, block.right);
        if (block.up != null) FloodFill(ref blocksLeft, ref shipParts, block.up);
        if (block.down != null) FloodFill(ref blocksLeft, ref shipParts, block.down);
    }
}