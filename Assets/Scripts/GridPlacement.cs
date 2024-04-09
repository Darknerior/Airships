using UnityEngine;
using System.Collections.Generic;

public class GridPlacement : MonoBehaviour
{
    public GameObject cubePrefab;
    public GameObject airshipPrefab;
    private GameObject _previewCube;
    private GameObject _previewAirship;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    public LayerMask collisionLayer;
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    public LayerMask airshipLayer;

    private BoxCollider _boxCollider;

    // Temp keybinds
    public KeyCode place;
    public KeyCode edit;
    public KeyCode cancel;
    public PlaceState state;

    // make Settings for interactable ui, switch between rotations and individual keybinds
    public KeyCode rotx;
    public KeyCode rotnegx;
    public KeyCode rotz;
    public KeyCode rotnegz;
    public KeyCode roty;
    public KeyCode rotnegy;
    
    public enum PlaceState {
        nothing,
        place,
        edit,
        editShip
    };

    // Value holders for editing
    private GameObject editObject;
    private GameObject _previewEditObject;
    private GameObject shipObject;
    private Vector3 editOffset;
    private Transform previousHitBlock; // Keep track of the previous block hit by the raycast for accurate snaprotation
    private Quaternion previousHitRotation;
    private int previousSnapId;
    private Transform rotationReference;
    private Vector3 baseRotation;
    private Vector3 relativeRotation;

    // Trackers
    private Dictionary<GameObject, Block> blocks; // Integer is the Id of the block
    private Dictionary<int, BlockType> blockTypes;
    private List<Transform> activeAirships;

    struct BlockType {
        public float weight; // Weight of the block
        public GameObject blockPrefab; // The prefab for the block
        // If this block can attach on that face
        public bool front;
        public bool back;
        public bool left;
        public bool right;
        public bool up;
        public bool down;

        public bool CanAttach(int faceId) {
            switch (faceId) {
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

    struct Block {
        public int blockId; // Id of this block
        // What block is attached to what face
        public BoxCollider blockCollider; // Store collider to prevent a ton of GetComponent calls
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

        public bool IsOccupied() {
            if (front == null || back == null || left == null || right == null || up == null || down == null) return false;
            return true;
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
                case PlaceState.editShip: EditShip(hit); break;
            }
        }
        else _previewCube.SetActive(false);
    }

    private void StateManager() {
        if (Input.GetKeyDown(cancel)) {
            if (state == PlaceState.editShip) CancelEditShip();
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
        if (Input.GetKeyDown(edit)) {
            if (state == PlaceState.editShip) {
                CancelEditShip();
                return;
            }

            if (state == PlaceState.edit) {
                editObject.SetActive(true);
                shipObject = editObject.transform.parent.gameObject;
                shipObject.SetActive(false);
                _previewAirship = CreatePreviewShip(shipObject);
                editOffset = editObject.transform.localPosition;
                _previewCube.SetActive(false);
                relativeRotation = Vector3.zero;
                state = PlaceState.editShip;
                return;
            }

            if (state == PlaceState.nothing) {
                state = PlaceState.edit;
                return;
            }
        }
    }

    private void CancelEditShip() {
        shipObject.SetActive(true);
        shipObject = null;
        Destroy(_previewAirship);
        editObject = null;
        relativeRotation = Vector3.zero;
        state = PlaceState.nothing;
    }

    private void CancelEdit() {
        editObject.SetActive(true);
        editObject = null;
        _previewCube.SetActive(false);
        relativeRotation = Vector3.zero;
        state = PlaceState.nothing;
    }

    private void CancelPlace() {
        relativeRotation = Vector3.zero;
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void EditShip(RaycastHit hit) {
        rotationReference = hit.collider.transform;
        var placementPosition = CalculatePlacementPosition(hit, _previewEditObject);
        _previewAirship.transform.position = placementPosition - editOffset;

        baseRotation = GetBaseRotation(1, _previewEditObject, hit);

        Vector3 originalPosition = _previewEditObject.transform.position;

        _previewAirship.transform.rotation = Quaternion.Euler(baseRotation + relativeRotation);
        Vector3 offset = _previewEditObject.transform.position - originalPosition; // Rotating might offset the editObject, so we adjust for that
        _previewAirship.transform.position -= offset;

        relativeRotation += GetRotation(1, _previewEditObject, hit);
        _previewAirship.transform.rotation = Quaternion.Euler(baseRotation + relativeRotation);
        offset = _previewEditObject.transform.position - originalPosition;
        _previewAirship.transform.position -= offset;

        if (Input.GetMouseButtonDown(0) && !ShipOverlaps(_previewAirship)) {
            shipObject.SetActive(true);
            shipObject.transform.position = _previewAirship.transform.position;
            shipObject.transform.rotation = _previewAirship.transform.rotation;
            foreach (Transform child in shipObject.transform) {
                SidesCheck(child.gameObject);
            }
            ShipCheck();
            CancelEditShip();
        }
    }

    private void EditCube(RaycastHit hit) {
        GameObject hitObject = hit.collider.gameObject;

        if (editObject == null && hitObject.layer == _airshipLayer) {
            editObject = hitObject;
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
        rotationReference = hit.collider.transform;
        var placementPosition = CalculatePlacementPosition(hit, _previewCube);
        _previewCube.transform.position = placementPosition;

        baseRotation = GetBaseRotation(1, _previewCube, hit);

        _previewCube.transform.rotation = Quaternion.Euler(baseRotation + relativeRotation);
        relativeRotation += GetRotation(1, _previewCube, hit);

        _previewCube.transform.rotation = Quaternion.Euler(baseRotation + relativeRotation);

        if (Input.GetMouseButtonDown(0) && GetOverlaps(_previewCube, _boxCollider.size - new Vector3(0.002f, 0.002f, 0.002f), collisionLayer ).Length == 0) { // Left mouse click
            var placedObject = editObject;

            if (editObject == null) {
                placedObject = CreateBlock(1, cubePrefab, placementPosition, _previewCube.transform.rotation);
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

    private Vector3 CalculatePlacementPosition(RaycastHit hit, GameObject placementCube) {
        var placementPosition = new Vector3(0,0,0);

        previousHitRotation = hit.collider.transform.rotation;
        previousHitBlock = hit.collider.transform;

        Transform closestSnapPoint;

        if (hit.collider.gameObject.layer == _airshipLayer) {
            closestSnapPoint = GetClosestSnapPoint(hit.point, hit.collider.gameObject);
            if (closestSnapPoint != null)
            {
                // If a snap point is found, set pos
                placementPosition = closestSnapPoint.position;
            }
        }
        else placementPosition = hit.point; //Set the location to the point at the ground we are looking at
        placementPosition += GetOffset(hit.normal);

        Vector3 difference = placementPosition - placementCube.transform.position;
        placementCube.transform.position += difference;

        if (GetOverlaps(placementCube, _boxCollider.size - new Vector3(0.002f, 0.002f, 0.002f), collisionLayer).Length > 0) { // If you try to place inside another block
            Collider[] surroundingCubes = GetOverlaps(placementCube, _boxCollider.size * 2f, airshipLayer);
            Transform closestSurroundingSnapPoint = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < surroundingCubes.Length; i++) {
                Collider collider = surroundingCubes[i];
                var newClosestSnapPoint = GetClosestSnapPoint(hit.point, collider.gameObject);
                float distance = Vector3.Distance(newClosestSnapPoint.position, hit.point);
                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestSurroundingSnapPoint = newClosestSnapPoint;
                }
            }

            if (closestSurroundingSnapPoint != null) {
                if (Physics.Raycast(Camera.main.transform.position, closestSurroundingSnapPoint.position - Camera.main.transform.position, out RaycastHit newHit, Mathf.Infinity, airshipLayer)) {
                    hit.normal = newHit.normal; // Change the hit.normal to the newHit.normal
                    closestSnapPoint = closestSurroundingSnapPoint;
                    placementPosition = closestSnapPoint.position + GetOffset(newHit.normal);
                    previousHitBlock = closestSnapPoint.parent;
                    previousHitRotation = closestSnapPoint.parent.rotation;
                    rotationReference = closestSnapPoint.parent;
                }
            }
        }
        placementCube.transform.position -= difference;

        // Check if hit object is part of the airship and find the closest snap point if applicable
        int snapFaceId = CalculateLocalFaceId(hit.normal, hit.collider.transform);
        if (hit.collider.transform != previousHitBlock || rotationReference.rotation != previousHitRotation || snapFaceId != previousSnapId) {
            relativeRotation = Vector3.zero;
        }

        previousSnapId = snapFaceId;

        return placementPosition;
    }

    private Vector3 GetOffset(Vector3 hitNormal) {
        var extents = _boxCollider.size * 0.5f;

        // Adjust the placement position by half the placed objects size in the hits normal direction 
        var sideOffset = new Vector3(
            hitNormal.x * extents.x,
            hitNormal.y * extents.y,
            hitNormal.z * extents.z
        );

        return sideOffset;
    }

    private Vector3 GetRotation(int blockId, GameObject rotObject, RaycastHit hit)
    {
        float minAngle = float.MaxValue;
        Vector3 rotationVector = Vector3.zero;
        
        for (int i = 1; i <= 6; i++) // Get the direction to rotate in based on the look direction
        {
            float angle = Mathf.Min(minAngle, Vector3.Angle(Camera.main.transform.forward, FaceIdToVector(i)));
            if (minAngle > angle) {
                minAngle = angle;
                rotationVector = FaceIdToVector(i);
            }
        }

        KeyCode[] keys = new KeyCode[6] {rotnegz, rotz, rotx, rotnegx, roty, rotnegy};
        Vector3[] rotationVectors = new Vector3[6] { rotationVector, -rotationVector, Vector3.Cross(rotationVector, new Vector3(0, 1, 0)), -Vector3.Cross(rotationVector, new(0, 1, 0)), Vector3.Cross(rotationVector, new(1, 0, 0)), -Vector3.Cross(rotationVector, new(1, 0, 0)) };

        if (rotationVector != Vector3.zero) { // Should always return true
            for (int i = 0; i < 6; i++) {
                if (Input.GetKeyDown(keys[i])) {
                    Vector3 rotationStep = Quaternion.AngleAxis(45, rotationVectors[i]).eulerAngles;
                    Vector3 finalRotation = Vector3.zero;
                    Quaternion originalRotation = rotObject.transform.rotation;
                    BlockType blockType = blockTypes[blockId];
                    bool canAttach = false;
                    int rotations = 0;

                    while (!canAttach) { // If the block cant attach we keep rotating till it can
                        rotations++;
                        rotObject.transform.Rotate(rotationStep, Space.World);
                        finalRotation += rotationStep;
                        int snapFaces = CalculateLocalFaceId(hit.normal, hit.collider.transform); // Get local faceId for accurate attachment check
                        canAttach = blockType.CanAttach(snapFaces);
                        if (GetOverlaps(rotObject, _boxCollider.size - new Vector3(0.002f, 0.002f, 0.002f), collisionLayer).Length > 0) canAttach = false;
                        if (state == PlaceState.editShip && ShipOverlaps(_previewAirship)) canAttach = false; // Check if we are editing a ship and check if it overlaps with anything
                        if (rotations == 3) return Vector3.zero;
                    }

                    rotObject.transform.rotation = originalRotation;
                    return finalRotation;
                }
            }
        }

        return Vector3.zero;

    }


    private Vector3 GetBaseRotation(int blockId, GameObject rotObject, RaycastHit hit) {
        rotObject.transform.rotation = rotationReference.rotation;
        Collider[] overlaps = GetOverlaps(rotObject, _boxCollider.size, airshipLayer);
        if (overlaps.Length == 0) return Vector3.zero;
        List<int> attachedIds = new();

        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - rotObject.transform.position;
            int selfFaceId = CalculateFaceId(objectDirection); // Check which face the object is near

            if (IsAligned(rotObject.transform, objectDirection, selfFaceId)) // Check if the object is directly next to it
            {
                attachedIds.Add(selfFaceId); // Add the faceId of the rotobject itself
            }
        }

        if (attachedIds.Count == 0) return rotationReference.transform.eulerAngles;

        BlockType type = blockTypes[blockId];

        for (int i = 0; i < attachedIds.Count; i++) {
            if (type.CanAttach(attachedIds[i])) return rotationReference.eulerAngles;
        }

        int selfId = 0;
        int attachId = attachedIds[0] - 1;


        for (int i = 0; i < 6; i++) {
            if (type.CanAttach(i)) selfId = i - 1;
        }

        Vector3[,] rotations = {{ new(0, 180, 0), new(0, -90, 0), new(0, 90, 0), new(90, 0, 0), new(-90, 0, 0)},
                                { new(0, -180, 0), new(0, 90, 0), new(0, -90, 0), new(-90, 0, 0), new(90, 0, 0)},
                                { new(0, 90, 0), new(0, -90, 0), new(0, 180, 0), new(0, 0, 90), new(0, 0, -90)}, 
                                { new(0, -90, 0), new(0, 90, 0), new(0, -180, 0), new(0, 0, -90), new(0, 0, 90)},
                                { new(-90, 0, 0), new(90, 0, 0), new(0, 0, -90), new(0, 0, 90), new(0, 0, 180)},
                                { new(90, 0, 0), new(-90, 0, 0), new(0, 0, 90), new(0, 0, -90), new(0, 0, -180)}}; // All rotation cases with each Id combos (6 * 5)

        return rotations[selfId, attachId] + rotationReference.eulerAngles;
    }

    private Transform GetClosestSnapPoint(Vector3 hitPoint, GameObject hitObject) {
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

    private void SetPreviewOpacity(GameObject obj, Material previewMat) {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        var cloneMat = new Material(previewMat);
        renderer.material = cloneMat;
    }

    private GameObject CreateBlock(int blockId, GameObject prefab, Vector3 position, Quaternion rotation) {
        GameObject blockObject = Instantiate(prefab, position, rotation);
        blockObject.layer = _airshipLayer;
        Block block = new Block() {blockCollider = blockObject.GetComponent<BoxCollider>(), blockId = blockId};
        blocks.Add(blockObject, block);
        return blockObject;
    }

    private GameObject CreatePreviewShip(GameObject copyShip) {
        // Generate a preview of the ship
        GameObject previewShip = new GameObject("PreviewShip");
        foreach (Transform child in copyShip.transform) {
            GameObject previewChild = Instantiate(child.gameObject, child.localPosition, child.localRotation, previewShip.transform);
            previewChild.GetComponent<BoxCollider>().isTrigger = true;
            if (child == editObject.transform) _previewEditObject = previewChild;
            previewChild.gameObject.layer = LayerMask.NameToLayer("Preview");
            SetPreviewOpacity(previewChild, previewMaterial);
        }
        return previewShip;
    }

    private bool ShipOverlaps(GameObject checkShip) {
        foreach (Transform child in checkShip.transform) {
            if (GetOverlaps(child.gameObject, _boxCollider.size - new Vector3(0.002f, 0.002f, 0.002f), collisionLayer).Length > 0) {
                return true;
            }
        }

        return false;
    }

    private int CalculateFaceId(Vector3 normal) {
        int Id = 0;
        float minAngle = float.MaxValue;

        for (int i = 1; i <= 6; i++) {
            float angle = Mathf.Min(minAngle, Vector3.Angle(FaceIdToVector(i), normal));

            if (minAngle > angle) {
                minAngle = angle;
                Id = i;
            }
        }

        return Id; // If its nothing return 0, 0 (not possible)
    }

    private int CalculateLocalFaceId(Vector3 normal, Transform hitObject) {
        Vector3[] checkVectors = new Vector3[6] { hitObject.forward, -hitObject.forward, -hitObject.right, hitObject.right, hitObject.up, -hitObject.up };
        int Id = 0;
        float minAngle = float.MaxValue;

        for (int i = 0; i < 6; i++) {
            float angle = Mathf.Min(minAngle, Vector3.Angle(checkVectors[i], normal));

            if (minAngle > angle) {
                minAngle = angle;
                Id = i + 1;
            }
        }

        return Id;
    }

    private void SidesCheck(GameObject checkObject) {
        Collider[] overlaps = GetOverlaps(checkObject, _boxCollider.size + new Vector3(0.002f, 0.002f, 0.002f), airshipLayer);

        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - checkObject.transform.position;
            int selfFaceId = CalculateLocalFaceId(objectDirection, checkObject.transform); // Check which face the object is near

            if (IsAligned(checkObject.transform, objectDirection, selfFaceId)) // Check if the object is directly next to it
            {
                SetFace(checkObject, selfFaceId, colTransform.gameObject); // Attach the object to itself
                int attachFaceId = CalculateLocalFaceId(-objectDirection, colTransform);
                SetFace(colTransform.gameObject, attachFaceId, checkObject); // Attach itself to the object
            }
        }
    }

    private bool IsAligned(Transform checkObject, Vector3 objectDirection, int faceId) {
        Vector3 angleVector = Vector3.zero;
        switch (faceId)
        {
            case 1: angleVector = checkObject.forward; break;
            case 2: angleVector = -checkObject.forward; break;
            case 3: angleVector = -checkObject.right; break;
            case 4: angleVector = checkObject.right; break;
            case 5: angleVector = checkObject.up; break;
            case 6: angleVector = -checkObject.up; break;
        }

        return Vector3.Angle(angleVector, objectDirection) < 1f;
    }
    
    private Collider[] GetOverlaps(GameObject checkObject, Vector3 extents, LayerMask layerMask)
    {
        Collider[] overlaps = Physics.OverlapBox(checkObject.transform.position, extents / 2, checkObject.transform.rotation, layerMask);
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
                parent = Instantiate(airshipPrefab).transform;
                activeAirships.Add(parent); // Lists add new indices at the end to we can safely do this without needing index adjustments
            }

            Rigidbody shipBody = parent.gameObject.GetComponent<Rigidbody>();
            float totalWeight = 0;
            Vector3 weightPosition = Vector3.zero;

            for (int i = 0; i < shipParts.Count; i++) {
                GameObject part = shipParts[i];
                part.transform.SetParent(parent); // Parent the ship parts under the new airship
                BlockType type = blockTypes[blocks[part].blockId];
                totalWeight += type.weight;
                weightPosition += type.weight * part.transform.position;
            }

            shipBody.centerOfMass = parent.transform.InverseTransformPoint(weightPosition / totalWeight); // Set the correct center of mass according to the weight of the blocks and their positions
            shipBody.mass = totalWeight;

            shipBody.WakeUp();

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

        for (int i = 0; i < 6; i++) {
            block.InternalSetFace(i, null);
        }
        blocks[clear] = block; // Clear the block itself
    }

    private void SetFace(GameObject SetObject, int faceId, GameObject attachObject) {
        // Because a dictionary returns a copy of a struct you cant directly modify it
        Block block = blocks[SetObject];
        block.InternalSetFace(faceId, attachObject);
        blocks[SetObject] = block;
    }

    private void SetBlockTypes()
    {
        // Regular cube
        blockTypes.Add(1, new() {blockPrefab = cubePrefab, weight = 1, front = true, back = true, left = true, right = true, up = true, down = true });
        // Slab
        blockTypes.Add(2, new() {blockPrefab = null /* create a slab prefab */, weight = 0.5f, front = true, back = true, left = true, right = true, up = false, down = true });
    }

    private Vector3 FaceIdToVector(int faceId) {
        Vector3[] vectors = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0) };
        return vectors[faceId - 1];
    }
}