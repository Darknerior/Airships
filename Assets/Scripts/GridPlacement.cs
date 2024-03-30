using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using Unity.VisualScripting;

public class GridPlacement : MonoBehaviour
{
    public GameObject cubePrefab;
    private GameObject _previewCube;
    private Transform _airshipParent;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    public LayerMask airshipLayer;

    private BoxCollider _boxCollider;

    // Temp keybinds
    public KeyCode place;
    public KeyCode edit;
    public KeyCode cancel;
    public PlaceState state;
    
    public enum PlaceState {
        nothing,
        place,
        edit
    };

    // Value holders for editing
    public GameObject editObject;

    // Trackers
    private Dictionary<GameObject, Block> blocks; // Integer is the id of the block
    private List<Transform> activeAirships;

    struct Block {
        public GameObject front; // FaceID 1-6
        public GameObject back;
        public GameObject left;
        public GameObject right;
        public GameObject up;
        public GameObject down;

        public void InternalSetFace(int faceID, GameObject attachedBlock)
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
            StateManager();

            switch (state) { // What to do in each place state
                case PlaceState.place: PlaceCube(hit); break;
                case PlaceState.edit: EditCube(hit); break;
            }
        }
        else _previewCube.SetActive(false);
    }

    private void StateManager() {
        if (Input.GetKeyDown(cancel)) {
            if (state == PlaceState.edit) CancelEdit();
            if (state == PlaceState.place) CancelPlace();


            state = PlaceState.nothing;
            return;
        }
        if (Input.GetKeyDown(place) && state == PlaceState.nothing) {
            if (state == PlaceState.place) {
                CancelPlace();
                return;
            }

            state = PlaceState.place;
            return;
        }
        if (Input.GetKeyDown(edit) && state == PlaceState.nothing) {
            if (state == PlaceState.edit){
                CancelEdit();
                return;
            }

            state = PlaceState.edit;
            return;
        }
    }

    private void CancelEdit() {
        editObject.SetActive(true);
        editObject = null;
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void CancelPlace() {
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void EditCube(RaycastHit hit) {
        GameObject hitObject = hit.transform.gameObject;

        if (editObject == null && hitObject.layer == _airshipLayer) {
            editObject = hitObject;
            _airshipParent = editObject.transform.parent;
        } 
        else if (editObject == null) {
            state = PlaceState.nothing;
            return;
        }

        editObject.SetActive(false);
        PlaceCube(hit);
        return;
    }

    private void PlaceCube(RaycastHit hit) {
        _previewCube.SetActive(true);

        // Adjust position to either ground hit or snap point
        var placementPosition = CalculatePlacementPosition(hit);

        _previewCube.transform.position = placementPosition;
        _previewCube.transform.rotation = hit.transform.rotation; // Reset rotation or set to snap point rotation

        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            // Instantiate the actual object
            Transform parent = _airshipParent;
            if (_airshipParent == null) {
                parent = new GameObject("Airship").transform;
                activeAirships.Add(parent);
            }
            
            var placedObject = editObject;

            if (editObject == null) {
                placedObject = CreateBlock(cubePrefab, placementPosition, hit.transform.rotation, parent);
            }
            else {
                placedObject.SetActive(true);
                placedObject.transform.position = placementPosition;
                placedObject.transform.rotation = Quaternion.identity;
                ClearFaces(placedObject); // Clear the face attachments of the edited block
                CancelEdit();
            }

            SidesCheck(placedObject);
            ShipCheck();
        }
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
                if (_previewCube.TryGetComponent(out BoxCollider collider))
                {
                    var hitNormal = hit.normal;
                    var extents = collider.size * 0.5f;

                    // Adjust the placement position by half the placed objects size in the hits normal direction 
                    var sideOffset = new Vector3(
                        hitNormal.x * extents.x,
                        hitNormal.y * extents.y,
                        hitNormal.z * extents.z
                    );

                    placementPosition += sideOffset;
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

    private GameObject CreateBlock(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent) {
        GameObject blockObject = Instantiate(prefab, position, rotation, parent);
        blockObject.layer = _airshipLayer;
        Block block = new Block();
        blocks.Add(blockObject, block);
        return blockObject;
    }

    private Vector2Int CalculateFaceID(Vector3 normal, Transform hitObject) {
        float[] angleArray = new float[6];
        float minAngle = float.MaxValue;

        angleArray[0] = Vector3.Angle(hitObject.forward, normal); // Front face
        angleArray[1] = Vector3.Angle(-hitObject.forward, normal); // Back face
        angleArray[2] = Vector3.Angle(-hitObject.right, normal); //  Left face
        angleArray[3] = Vector3.Angle(hitObject.right, normal); // Right face
        angleArray[4] = Vector3.Angle(hitObject.up, normal); // Upper face
        angleArray[5] = Vector3.Angle(-hitObject.up, normal); // Downwards face

        for (int i = 0; i < 6; i++) {
            minAngle = Mathf.Min(angleArray[i], minAngle);
        }
        
        // Return the face it is attached to and the inverse face
        if (minAngle == angleArray[0]) return new Vector2Int(1, 2);
        if (minAngle == angleArray[1]) return new Vector2Int(2, 1);
        if (minAngle == angleArray[2]) return new Vector2Int(3, 4);
        if (minAngle == angleArray[3]) return new Vector2Int(4, 3);
        if (minAngle == angleArray[4]) return new Vector2Int(5, 6);
        if (minAngle == angleArray[5]) return new Vector2Int(6, 5);
        return new Vector2Int(0, 0); // If its nothing return 0, 0 (not possible)
    }

    private void SidesCheck(GameObject checkObject) {
        Collider[] overlaps = Physics.OverlapBox(checkObject.transform.position, checkObject.GetComponent<BoxCollider>().size / 2, checkObject.transform.rotation, airshipLayer);
        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            if (colTransform.gameObject == checkObject) continue;
            Vector3 objectDirection = colTransform.position - checkObject.transform.position;
            Vector2Int faceID = CalculateFaceID(objectDirection, checkObject.transform);
            Vector3 angleVector = Vector3.zero;
            switch (faceID.x)
            {
                case 1: angleVector = checkObject.transform.forward; break;
                case 2: angleVector = -checkObject.transform.forward; break;
                case 3: angleVector = -checkObject.transform.right; break;
                case 4: angleVector = checkObject.transform.right; break;
                case 5: angleVector = checkObject.transform.up; break;
                case 6: angleVector = -checkObject.transform.up; break;
            }

            if (Vector3.Angle(angleVector, objectDirection) == 0)
            {
                SetFace(checkObject, faceID.x, colTransform.gameObject);
                SetFace(colTransform.gameObject, faceID.y, checkObject);
            }
        }
    }

    private void ShipCheck() {
        List<GameObject> blocksLeft = new();
        List<GameObject> shipParts = new();
        foreach (GameObject key in blocks.Keys) {
            blocksLeft.Add(key); // Get all blocks in a tracker list
        }

        int shipIndex = 0;
        int shipsAvailable = activeAirships.Count;

        while (blocksLeft.Count > 0) {
            shipParts.Clear();
            GameObject block = blocksLeft[0];
            FloodFill(ref blocksLeft, ref shipParts, block);

            Transform parent;
            if (shipIndex < shipsAvailable)
                parent = activeAirships[shipIndex];
            else {
                parent = new GameObject("Airship").transform;
                activeAirships.Add(parent); // Using lists adding at the end of the list here to maintain the used ships
            }

            for (int i = 0; i < shipParts.Count; i++) {
                shipParts[i].transform.SetParent(parent);
            }

            shipIndex++;
        }

        while (shipIndex < activeAirships.Count) {
            Transform removedShip = activeAirships[shipIndex];
            activeAirships.Remove(removedShip);
            Destroy(removedShip.gameObject); // Destroy the excess empty ships
        }
    }

    private void FloodFill(ref List<GameObject> blocksLeft, ref List<GameObject> shipParts, GameObject checkObject)
    {
        Queue<GameObject> checkQueue = new();
        checkQueue.Enqueue(checkObject);
        while(checkQueue.Count> 0)
        {
            GameObject currentObject = checkQueue.Dequeue();
            blocksLeft.Remove(currentObject);
            shipParts.Add(currentObject);
            Block block = blocks[currentObject];

            if (block.front != null && blocksLeft.Contains(block.front)) checkQueue.Enqueue(block.front);
            if (block.back != null && blocksLeft.Contains(block.back)) checkQueue.Enqueue(block.back);
            if (block.left != null && blocksLeft.Contains(block.left)) checkQueue.Enqueue(block.left);
            if (block.right != null && blocksLeft.Contains(block.right)) checkQueue.Enqueue(block.right);
            if (block.up != null && blocksLeft.Contains(block.up)) checkQueue.Enqueue(block.up);
            if (block.down != null && blocksLeft.Contains(block.down)) checkQueue.Enqueue(block.down);
        }
    }

    private void ClearFaces(GameObject clear) {
        Block block = blocks[clear];
        // Remove the block from the objects it was attached to as well
        if (block.front != null) SetFace(block.front, 2, null);
        if (block.back != null) SetFace(block.back, 1, null);
        if (block.left != null) SetFace(block.left, 4, null);
        if (block.right != null) SetFace(block.right, 3, null);
        if (block.up != null) SetFace(block.up, 6, null);
        if (block.down != null) SetFace(block.down, 5, null);
        blocks[clear] = new Block(); // Clear the block itself
    }

    private void SetFace(GameObject SetObject, int faceID, GameObject attachObject) {
        // Because a dictionary returns a copy of a struct you cant directly modify it
        Block block = blocks[SetObject];
        block.InternalSetFace(faceID, attachObject);
        blocks[SetObject] = block;
    }
}