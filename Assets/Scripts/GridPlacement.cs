using UnityEngine;
using System.Collections.Generic;

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

    // Settings for interactable ui, switch between rotations and individual keybinds
    public KeyCode rotx;
    public KeyCode rotnegx;
    public KeyCode rotz;
    public KeyCode rotnegz;
    public KeyCode roty;
    public KeyCode rotnegy;
    
    public enum PlaceState {
        nothing,
        place,
        edit
    };

    // Value holders for editing
    private bool snapRotation = false;
    public GameObject editObject;
    public float rotAngle;

    // Trackers
    private Dictionary<GameObject, Block> blocks; // Integer is the id of the block
    private Dictionary<int, BlockType> blockTypes;
    private List<Transform> activeAirships;

    struct BlockType {
        public bool cube; // If the block is a cube
        // If this block can attach on that face
        public bool front;
        public bool back;
        public bool left;
        public bool right;
        public bool up;
        public bool down;

        public bool CanAttach(int faceID) {
            if (cube) {
                switch (faceID) {
                    case 1: return front;
                    case 2: return back;
                    case 3: return left;
                    case 4: return right;
                    case 5: return up;
                    case 6: return down;
                    default: return false;
                }
            }
            else {
                // Add triangle support
                return false;
            }
        }

        public bool AllowedConfiguration(int[] configuration) {
            for (int i = 0; i < configuration.Length; i++) {
                if (!CanAttach(configuration[i])) return false;
            }
            return true;
        }
    }

    struct Block {
        public int blockID;
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
        blockTypes = new();

        _previewCube = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
        SetPreviewOpacity(_previewCube, previewMaterial);
        _previewCube.SetActive(false);
        _boxCollider = _previewCube.GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true; //Prevent player colliding with preview

        // Temp
        SetBlockTypes();
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


        //_previewCube.transform.rotation = hit.transform.rotation;
        if (snapRotation) _previewCube.transform.rotation = Quaternion.Euler(GetStartingRotation(1, _previewCube));
        _previewCube.transform.Rotate(CheckRotation(1, _previewCube, hit), Space.World);

        _previewCube.transform.position = placementPosition;

        if (Input.GetMouseButtonDown(0) && Physics.OverlapBox(placementPosition, _boxCollider.size / 2 - new Vector3(0.001f, 0.001f, 0.001f), _previewCube.transform.rotation).Length <= 1) { // Left mouse click
            // Instantiate the actual object
            Transform parent = _airshipParent;
            if (_airshipParent == null) {
                parent = new GameObject("Airship").transform;
                activeAirships.Add(parent);
            }
            
            var placedObject = editObject;

            if (editObject == null) {
                placedObject = CreateBlock(cubePrefab, placementPosition, _previewCube.transform.rotation, parent);
            }
            else {
                placedObject.SetActive(true);
                placedObject.transform.position = placementPosition;
                placedObject.transform.rotation = _previewCube.transform.rotation;
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
            snapRotation = true;
            _airshipParent = null; //We null airship parent as the ray is not returning airship layer
            placementPosition = hit.point; //Set the location to the point at the ground we are looking at
            if (!_previewCube.TryGetComponent(out Collider collider)) return placementPosition;
            var heightOffset = collider.bounds.extents.y;
            placementPosition += new Vector3(0, heightOffset, 0); //add height offset

        }

        return placementPosition;
    }

    private Vector3 CheckRotation(int blockID, GameObject rotObject, RaycastHit hit)
    {
        float minAngle = float.MaxValue;
        Vector3 rotationVector = Vector3.zero;
        
        for (int i = 0; i < 6; i++) // Get the direction to rotate in based on the look direction
        {
            float angle = Mathf.Min(minAngle, Vector3.Angle(Camera.main.transform.forward, FaceIDToVector(i)));
            if (minAngle > angle) {
                minAngle = angle;
                rotationVector = FaceIDToVector(i);
            }
        }

        KeyCode[] keys = new KeyCode[6] {rotnegz, rotz, rotx, rotnegx, roty, rotnegy};
        Vector3[] rotationVectors = new Vector3[6] { rotationVector, -rotationVector, Vector3.Cross(rotationVector, new Vector3(0, 1, 0)), -Vector3.Cross(rotationVector, new(0, 1, 0)), Vector3.Cross(rotationVector, new(1, 0, 0)), -Vector3.Cross(rotationVector, new(1, 0, 0)) };

        Vector3 finalRotation = Vector3.zero;
        if (rotationVector != Vector3.zero) { // Should always return true
            for (int i = 0; i < 6; i++) {
                if (Input.GetKeyDown(keys[i])) {
                    finalRotation = Quaternion.AngleAxis(45, rotationVectors[i]).eulerAngles;
                    finalRotation = CanRotate(blockID, finalRotation, hit);
                    return finalRotation;
                }
            }
        }

        return finalRotation;
    }

    private Vector3 CanRotate(int blockID, Vector3 addRotation, RaycastHit hit) {
        // Get the attachment faces of the block it is being attached on (id.x) and the block itself (id.y)
        Vector2Int snapFaces = CalculateFaceID(hit.normal);
        Vector3 finalRotation = Vector3.zero;

        BlockType blockType = blockTypes[blockID];
        bool canAttach = false;
        int rotations = 0;
        while (!canAttach) {
            canAttach = blockType.CanAttach(snapFaces.y);
            finalRotation += addRotation;
            rotations++;
            if (rotations == 3) return Vector3.zero;
        }

        return finalRotation;
    }

    private Vector3 GetStartingRotation(int blockID, GameObject rotObject) {
        snapRotation = false;
        Collider[] overlaps = GetOverlaps(rotObject);
        if (overlaps.Length == 0) return Vector3.zero;
        List<int> attachedIDs = new();

        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - rotObject.transform.position;
            Vector2Int faceID = CalculateFaceID(objectDirection); // Check which face the object is near

            if (IsAligned(rotObject.transform, objectDirection, CalculateLocalFaceID(objectDirection, rotObject.transform))) // Check if the object is directly next to it
            {
                attachedIDs.Add(faceID.x); // Add the faceID of the rotobject itself
            }
        }

        if (attachedIDs.Count <= 0) return Vector3.zero;

        BlockType type = blockTypes[blockID];

        for (int i = 0; i < attachedIDs.Count; i++) {
            if (type.CanAttach(attachedIDs[i])) return Vector3.zero;
        }

        int selfID = 0;
        int attachID = attachedIDs[0] - 1;


        for (int i = 1; i <= 6; i++) {
            if (type.CanAttach(i)) selfID = i - 1;
        }

        Vector3[,] rotations = {{ new(0, 180, 0), new(0, -90, 0), new(0, 90, 0), new(90, 0, 0), new(-90, 0, 0)},
                                { new(0, -180, 0), new(0, 90, 0), new(0, -90, 0), new(-90, 0, 0), new(90, 0, 0)},
                                { new(0, 90, 0), new(0, -90, 0), new(0, 180, 0), new(0, 0, 90), new(0, 0, -90)}, 
                                { new(0, -90, 0), new(0, 90, 0), new(0, -180, 0), new(0, 0, -90), new(0, 0, 90)},
                                { new(-90, 0, 0), new(90, 0, 0), new(0, 0, -90), new(0, 0, 90), new(0, 0, 180)},
                                { new(90, 0, 0), new(-90, 0, 0), new(0, 0, 90), new(0, 0, -90), new(0, 0, -180)}};

        return rotations[selfID, attachID];
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

    private Vector2Int CalculateFaceID(Vector3 normal) {
        Vector2Int[] returnVectors = new Vector2Int[6] { new(1, 2), new(2, 1), new(3, 4), new(4, 3), new(5, 6), new(6, 5) };
        Vector3 minVector = Vector3.zero;
        float minAngle = float.MaxValue;

        for (int i = 0; i < 6; i++) {
            float angle = Mathf.Min(minAngle, Vector3.Angle(FaceIDToVector(i), normal));

            if (minAngle > angle) {
                minAngle = angle;
                minVector = FaceIDToVector(i);
            }
        }

        for (int i = 0; i < 6; i ++) {
            if (minVector == FaceIDToVector(i)) return returnVectors[i];
        }

        return new Vector2Int(0, 0); // If its nothing return 0, 0 (not possible)
    }

    private Vector2Int CalculateLocalFaceID(Vector3 normal, Transform hitObject) {
        Vector2Int[] returnVectors = new Vector2Int[6] { new(1, 2), new(2, 1), new(3, 4), new(4, 3), new(5, 6), new(6, 5) };
        Vector3[] checkVectors = new Vector3[6] { hitObject.forward, -hitObject.forward, -hitObject.right, hitObject.right, hitObject.up, -hitObject.right };
        Vector3 minVector = Vector3.zero;
        float minAngle = float.MaxValue;

        for (int i = 0; i < 6; i++) {
            float angle = Mathf.Min(minAngle, Vector3.Angle(checkVectors[i], normal));

            if (minAngle > angle) {
                minAngle = angle;
                minVector = checkVectors[i];
            }
        }

        for (int i = 0; i < 6; i ++) {
            if (minVector == checkVectors[i]) return returnVectors[i];
        }

        return new Vector2Int(0, 0); // If its nothing return 0, 0 (not possible)
    }

    private void SidesCheck(GameObject checkObject) {
        Collider[] overlaps = GetOverlaps(checkObject);

        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - checkObject.transform.position;
            Vector2Int faceID = CalculateFaceID(objectDirection); // Check which face the object is near

            if (IsAligned(checkObject.transform, objectDirection, CalculateLocalFaceID(objectDirection, checkObject.transform))) // Check if the object is directly next to it
            {
                SetFace(checkObject, faceID.x, colTransform.gameObject); // Attach the object to itself
                SetFace(colTransform.gameObject, faceID.y, checkObject); // Attach itself to the object
            }
        }
    }

    private bool IsAligned(Transform checkObject, Vector3 objectDirection, Vector2Int faceID) {
        Vector3 angleVector = Vector3.zero;
        switch (faceID.x)
        {
            case 1: angleVector = checkObject.forward; break;
            case 2: angleVector = -checkObject.forward; break;
            case 3: angleVector = -checkObject.right; break;
            case 4: angleVector = checkObject.right; break;
            case 5: angleVector = checkObject.up; break;
            case 6: angleVector = -checkObject.up; break;
        }

        return Vector3.Angle(angleVector, objectDirection) == 0;
    }
    
    private Collider[] GetOverlaps(GameObject checkObject)
    {
        Collider[] overlaps = Physics.OverlapBox(checkObject.transform.position, checkObject.GetComponent<BoxCollider>().size / 2, checkObject.transform.rotation, airshipLayer);
        if (overlaps.Length <= 1) return new Collider[0];
        List<Collider> finalOverlaps = new();
        for (int i = 0; i < overlaps.Length; i++) {
            if (overlaps[i].transform != checkObject.transform)
            {
                finalOverlaps.Add(overlaps[i]);
            }
        }

        return finalOverlaps.ToArray();
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
            FloodFill(ref blocksLeft, ref shipParts, block); // Get all 

            Transform parent;
            if (shipIndex < shipsAvailable)
                parent = activeAirships[shipIndex];
            else {
                parent = new GameObject("Airship").transform;
                activeAirships.Add(parent); // Lists add new indices at the end to we can safely do this without needing index adjustments
            }

            for (int i = 0; i < shipParts.Count; i++) {
                shipParts[i].transform.SetParent(parent); // Parent the ship parts under the new airship
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

    private void SetBlockTypes()
    {
        // Regular cube
        blockTypes.Add(1, new() { cube = true, front = true, back = true, left = true, right = true, up = true, down = true });
        // Triangle
        blockTypes.Add(2, new() { cube = false, front = false, back = true, left = true, right = true, up = false, down = true });
    }

    private Vector3 FaceIDToVector(int faceID) {
        Vector3[] vectors = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0) };
        return vectors[faceID];
    }
}