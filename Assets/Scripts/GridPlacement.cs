using UnityEngine;
using System.Collections.Generic;
public class GridPlacement : MonoBehaviour
{
    public BlockType[] blockTypes;
    [Range(0, 1)] public float edgeBias = 0;
    [Range(0, 1)] public float rotationBias = 0;
    public float rayPadding = 0;
    public float reach = 10;
    public GameObject airshipPrefab;
    private GameObject _previewCube;
    private GameObject _previewAirship;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    public LayerMask collisionLayer;
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    public LayerMask airshipLayer;
    private readonly Vector3 collisionPadding = new Vector3(-0.002f, -0.002f, -0.002f);
    private readonly Vector3 overlapPadding = new Vector3(0.002f, 0.002f, 0.002f);

    // Temp keybinds
    public KeyCode place;
    public KeyCode edit;
    public KeyCode cancel;
    public PlaceState state;
    public KeyCode rotx;
    public KeyCode rotnegx;
    public KeyCode rotz;
    public KeyCode rotnegz;
    public KeyCode roty;
    public KeyCode rotnegy;
    int parentNum = 1; // easy parent tracking
    
    public enum PlaceState {
        nothing,
        place,
        edit
    };

    // Value holders for editing
    private int currentBlockId = 0;
    private GameObject editObject;
    private MeshRenderer editObjectRenderer;
    private GameObject _previewEditObject;
    private GameObject shipObject;
    private Vector3 editOffset;
    private Transform rotationReference;
    private Vector3 referenceNormal;

    // Trackers
    private Dictionary<GameObject, Block> blocks; // Integer is the Id of the block
    private Dictionary<Transform, Rigidbody> activeBodies;

    private void Awake() {
        blocks = new();
        activeBodies = new();
        CreatePreviewBlock(0);
    }

    private void Update() {
        Vector3 direction = Camera.main.transform.forward;
        Vector3 camPos = Camera.main.transform.position;
        float paddingRadius = rayPadding * 0.5f;

        Collider closestBlock = null;
        RaycastHit hit = new();

        if (!Physics.Raycast(camPos, direction, out hit, Mathf.Infinity, airshipLayer))
        {
            Collider[] overlaps = Physics.OverlapCapsule(camPos + direction * paddingRadius, camPos + direction * (reach - paddingRadius), paddingRadius, airshipLayer);
            if (overlaps.Length > 0)
            {
                float closestDistance = float.MaxValue;

                for (int i = 0; i < overlaps.Length; i++) {
                    Collider block = overlaps[i];
                    Vector3 blockPosition = block.transform.position;
                    Vector3 camToBlock = blockPosition - camPos;
                    Vector3 projectedOnRay = Vector3.Project(camToBlock, direction);
                    Vector3 lineIntersection = camPos + projectedOnRay;
                    float distanceFromRay = Vector3.Distance(lineIntersection, blockPosition);
                    if (distanceFromRay < closestDistance && Physics.Raycast(lineIntersection, block.transform.position - lineIntersection, out RaycastHit newHit, Mathf.Infinity, airshipLayer)) {
                        closestDistance = distanceFromRay;
                        closestBlock = block;
                        hit = newHit;
                    }
                }
            }
        }

        if (closestBlock == null && !Physics.Raycast(camPos, direction, out hit, Mathf.Infinity, placementLayer)) {
            _previewCube.SetActive(false);
            rotationReference = null;
            return;
        }

        StateManager();

        switch (state) { // What to do in each place state
            case PlaceState.place: PlaceCube(hit); break;
            case PlaceState.edit: EditCube(hit); break;
        }

        PlacementManager(hit);
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
        if (Input.GetKeyDown(edit)) {
            if (state == PlaceState.edit) {
                CancelEdit();
                state = PlaceState.nothing;
                return;
            }

            if (state == PlaceState.nothing) {
                state = PlaceState.edit;
                return;
            }
        }
    }

    private void PlacementManager(RaycastHit hit) {
        if (Input.GetMouseButtonDown(0) && GetOverlaps(_previewCube, collisionPadding, collisionLayer).Length == 0) { // Left mouse click

            if (state == PlaceState.place) {
                GameObject placedObject = CreateBlock(currentBlockId, _previewCube.transform.position, _previewCube.transform.rotation);
                CombineAdjacentBlocks(placedObject);
            }

            if (state == PlaceState.edit) {
                editObjectRenderer.enabled = true;
                editObject.layer = _airshipLayer;
                RemoveWeights(editObject.transform.parent, new List<GameObject> { editObject });
                ManageDetachment(editObject); // Clear the face attachments of the edited block
                editObject.transform.position = _previewCube.transform.position;
                editObject.transform.rotation = _previewCube.transform.rotation;
                CombineAdjacentBlocks(editObject);
                CancelEdit();
            }
        }
    }

    private void CancelEdit() {
        editObjectRenderer.enabled = true;
        editObject.layer = _airshipLayer;
        editObject = null;
        _previewCube.SetActive(false);
        currentBlockId = 0;
        state = PlaceState.nothing;
    }

    private void CancelPlace() {
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void EditCube(RaycastHit hit) {
        GameObject hitObject = hit.collider.gameObject;

        if (editObject == null && hitObject.layer == _airshipLayer) {
            editObject = hitObject;
            CreatePreviewBlock(blocks[hitObject].blockId);
            editObjectRenderer = editObject.GetComponent<MeshRenderer>();
            editObject.layer = 0;
            editObjectRenderer.enabled = false;
        } 
        else if (editObject == null) {
            state = PlaceState.nothing;
            return;
        }

        _previewCube.SetActive(true);
        SetPreviewCube(hit);
        return;
    }

    private void PlaceCube(RaycastHit hit) {
        _previewCube.SetActive(true);
        BlockPicker();
        SetPreviewCube(hit);
    }

    private void DestroyBlock(GameObject destroyBlock) {
        ManageDetachment(destroyBlock);
        blocks.Remove(destroyBlock);
        Destroy(destroyBlock);
    }

    private void BlockPicker() {
        int scrolls = (int)Input.mouseScrollDelta.y;
        int newBlockId = (currentBlockId + scrolls) % blockTypes.Length;
        if (newBlockId < 0) {
            newBlockId += blockTypes.Length;
        }

        if (newBlockId != currentBlockId) {
            currentBlockId = newBlockId;
            CreatePreviewBlock(newBlockId);
        }
    }

    private void SetPreviewCube(RaycastHit hit) {
        // Adjust position to either ground hit or snap point
        referenceNormal = hit.normal;
        rotationReference = hit.collider.transform;
        var placementPosition = CalculatePlacementPosition(hit, _previewCube);
        _previewCube.transform.position = placementPosition;
        _previewCube.transform.rotation = rotationReference.rotation;
        
        _previewCube.transform.rotation = GetBaseRotation(_previewCube, hit);
    }

    void CreatePreviewBlock(int blockId) {
        GameObject newPreviewBlock = Instantiate(blockTypes[blockId].blockPrefab);
        SetPreviewOpacity(newPreviewBlock, previewMaterial);
        BoxCollider newCollider = newPreviewBlock.GetComponent<BoxCollider>();
        newCollider.isTrigger = true;
        if (_previewCube != null) blocks.Remove(_previewCube);
        Destroy(_previewCube);
        _previewCube = newPreviewBlock;
        blocks.Add(_previewCube, new Block() { blockId = blockId, blockCollider = newCollider });
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit, GameObject placementCube) {
        var placementPosition = Vector3.zero;

        if (hit.collider.gameObject.layer == _airshipLayer) {
            placementPosition = GetBiasedSnapPoint(hit, edgeBias);
        }
        else placementPosition = hit.point + GetOffset(_previewCube, hit.normal); //Set the location to the point at the ground we are looking at

        GameObject collisionCheckBlock = CreateBlock(currentBlockId, placementPosition, rotationReference.rotation);
        collisionCheckBlock.layer = LayerMask.NameToLayer("Preview");
        collisionCheckBlock.name = "collisioncheck";

        if (GetOverlaps(collisionCheckBlock, collisionPadding, collisionLayer).Length > 0) {
            Collider[] surroundingBlocks = Physics.OverlapBox(placementPosition, Vector3.one, rotationReference.rotation, airshipLayer);
            Vector3 newPlacementPosition = Vector3.zero;
            float bestValue = float.MaxValue;

            foreach (Collider block in surroundingBlocks) {
                for (int i = 1; i <= 6; i++) {
                    Vector3 localposition = GetOffset(block.gameObject, block.transform.InverseTransformDirection(LocalFaceIdToVector(i, block.transform))) * 2f;
                    float angleFromPoint = Vector3.Angle(block.transform.TransformDirection(localposition), Camera.main.transform.forward) % 180f / 180f * edgeBias;
                    Vector3 position = block.transform.TransformPoint(localposition);
                    float newValue = Vector3.Distance(placementPosition, position) + angleFromPoint;

                    collisionCheckBlock.transform.position = position;
                    collisionCheckBlock.transform.rotation = block.transform.rotation;

                    if (newValue > bestValue || GetOverlaps(collisionCheckBlock, collisionPadding, collisionLayer).Length > 0) continue;

                    bestValue = newValue;
                    newPlacementPosition = position;
                }
            }
            placementPosition = newPlacementPosition;
        }

        blocks.Remove(collisionCheckBlock);
        Destroy(collisionCheckBlock);

        return placementPosition;
    }

    private Vector3 GetOffset(GameObject offsetObject, Vector3 localNormal) {
        var collider = blocks[offsetObject].blockCollider;
        var center = collider.center * 2f; // If a collider is offset we adjust the offset accordingly (*2f because the added height is the offset *2)

        var extents = (collider.size - center) * 0.5f;

        // Adjust the placement position by half the placed objects size in the hits normal direction 
        var sideOffset = new Vector3(
            localNormal.x * extents.x,
            localNormal.y * extents.y,
            localNormal.z * extents.z
        );

        return sideOffset;
    }

    private Vector4 GetClosestEdgeData(RaycastHit hit) {
        Vector3 localNormal = hit.collider.transform.InverseTransformDirection(hit.normal);
        Vector3 localPoint = hit.collider.transform.InverseTransformPoint(hit.point);

        if (hit.collider.gameObject.layer != _airshipLayer) return Vector4.zero;

        Block block = blocks[hit.collider.gameObject];
        Vector3 size = block.blockCollider.size;
        Vector3 offset = GetOffset(hit.collider.gameObject, localNormal);

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

    private Quaternion GetBaseRotation(GameObject rotObject, RaycastHit hit) {
        Vector4 edgeData = GetClosestEdgeData(hit);
        if (edgeData == Vector4.zero) return Quaternion.identity;

        Vector3 inverseEdgeDistance = hit.collider.transform.TransformDirection(edgeData).normalized;
        float maxValue = edgeData.w;

        if (0.5f - maxValue > rotationBias || referenceNormal != hit.normal) {
            inverseEdgeDistance = -referenceNormal;
        }
        
        Quaternion rotation = SafeFromToRotation(-rotObject.transform.up, inverseEdgeDistance);
        Debug.DrawRay(rotObject.transform.position, -rotObject.transform.up * 2, Color.cyan);
        Debug.DrawRay(rotObject.transform.position, inverseEdgeDistance * 2, Color.red);
        Debug.Log("angle: " + Vector3.Angle(-rotObject.transform.up, inverseEdgeDistance) + " solution: " + rotation.eulerAngles.magnitude);

        return rotation * rotObject.transform.rotation;
    }

    public Quaternion SafeFromToRotation(Vector3 fromVector, Vector3 toVector)
    {
        if (Vector3.Dot(fromVector, toVector) < -0.99999f)
        {
            Vector3 axis = Vector3.Cross(rotationReference.up, fromVector).normalized;
            if (Mathf.Approximately(axis.magnitude, 0)) axis = Vector3.Cross(rotationReference.right, fromVector).normalized;
            return Quaternion.AngleAxis(180f, axis);
        }
        else
        {
            return Quaternion.FromToRotation(fromVector, toVector);
        }
    }


    private Vector3 GetBiasedSnapPoint(RaycastHit hit, float bias) {
        Vector4 edgeData = GetClosestEdgeData(hit);
        if (edgeData == Vector4.zero) return Vector3.zero;

        Vector3 inverseEdgeDistance = edgeData;
        float maxValue = edgeData.w;

        if (0.5f - maxValue > bias) {
            inverseEdgeDistance = hit.collider.transform.InverseTransformDirection(hit.normal);
        }

        referenceNormal = hit.collider.transform.TransformDirection(inverseEdgeDistance.normalized);

        return rotationReference.TransformPoint(GetOffset(rotationReference.gameObject, inverseEdgeDistance.normalized) * 2f);
    }

    private void SetPreviewOpacity(GameObject obj, Material previewMat) {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        var cloneMat = new Material(previewMat);
        renderer.material = cloneMat;
    }

    private GameObject CreateBlock(int blockId, Vector3 position, Quaternion rotation) {
        GameObject blockObject = Instantiate(blockTypes[blockId].blockPrefab, position, rotation);
        blockObject.layer = _airshipLayer;
        Block block = new Block() {blockCollider = blockObject.GetComponent<BoxCollider>(), blockId = blockId};
        blocks.Add(blockObject, block);
        return blockObject;
    }

    private GameObject CreateShip(List<GameObject> airshipBlocks) {
        GameObject airshipObject = Instantiate(airshipPrefab);
        airshipObject.name = parentNum.ToString();
        parentNum++;
        Rigidbody airshipBody = airshipObject.GetComponent<Rigidbody>();
        airshipBody.mass = 0;
        activeBodies.Add(airshipObject.transform, airshipBody);
        ReParentBlocks(airshipObject.transform, airshipBlocks);
        AddWeights(airshipObject.transform, airshipBlocks);
        return airshipObject;
    }

    private void DestroyShip(Transform ship) {
        activeBodies.Remove(ship);
        Destroy(ship.gameObject);
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
            if (GetOverlaps(child.gameObject, collisionPadding, collisionLayer).Length > 0) {
                return true;
            }
        }

        return false;
    }

    private int CalculateLocalFaceId(Vector3 normal, Transform refObject) {
        int id = 0;
        float minAngle = float.MaxValue;

        for (int i = 1; i <= 6; i++) {
            float angle = Mathf.Min(minAngle, Vector3.Angle(LocalFaceIdToVector(i, refObject), normal));

            if (minAngle > angle) {
                minAngle = angle;
                id = i;
            }
        }

        return id;
    }

    private void CombineAdjacentBlocks(GameObject checkObject) {
        Collider[] overlaps = GetOverlaps(checkObject, overlapPadding, airshipLayer);

        for (int i = 0; i < overlaps.Length; i++) {
            Collider collider = overlaps[i];
            Transform colTransform = collider.transform;
            Vector3 objectDirection = colTransform.position - checkObject.transform.position;
            int selfFaceId = CalculateLocalFaceId(objectDirection, checkObject.transform); // Check which face the object is near

            if (IsAligned(checkObject.transform, colTransform, selfFaceId)) // Check if the object is directly on it
            {
                SetFace(checkObject, selfFaceId, colTransform.gameObject); // Attach the object to itself
                int attachFaceId = CalculateLocalFaceId(-objectDirection, colTransform);
                SetFace(colTransform.gameObject, attachFaceId, checkObject); // Attach itself to the object
                if (colTransform.parent != checkObject.transform.parent) {
                    checkObject.transform.SetParent(colTransform.parent); 
                    AddWeights(colTransform.parent, new List<GameObject> { checkObject } );
                }
            }
        }

        if (checkObject.transform.parent == null) {
            CreateShip(new List<GameObject> { checkObject });
        }
    }

    private bool IsAligned(Transform baseObject, Transform alignedObject, int faceId) {
        Vector3 angleVector = Vector3.zero;
        switch (faceId)
        {
            case 1: angleVector = baseObject.forward; break;
            case 2: angleVector = -baseObject.forward; break;
            case 3: angleVector = -baseObject.right; break;
            case 4: angleVector = baseObject.right; break;
            case 5: angleVector = baseObject.up; break;
            case 6: angleVector = -baseObject.up; break;
        }

        Vector3 checkPoint = baseObject.TransformPoint(blocks[baseObject.gameObject].blockCollider.center);
        Debug.DrawRay(checkPoint, angleVector * 2, Color.yellow, 3);
        if (Physics.Raycast(checkPoint, angleVector, out RaycastHit hit, Mathf.Infinity, airshipLayer) && hit.collider.transform == alignedObject && Vector3.Angle(angleVector, alignedObject.position - baseObject.position) < 1f) {
            return true;
        }

        return false;
    }

    private void ReParentBlocks(Transform newParent, List<GameObject> reparentBlocks) {
        for (int i = 0; i < reparentBlocks.Count; i++) {
            reparentBlocks[i].transform.SetParent(newParent);
        }
    }

    private void RemoveWeights(Transform ship, List<GameObject> removeBlocks) {
        Rigidbody shipBody = activeBodies[ship];
        float totalMass = shipBody.mass;
        Vector3 weightedMass = shipBody.worldCenterOfMass * shipBody.mass;
        for (int i = 0; i < removeBlocks.Count; i++) {
            float weight = blockTypes[blocks[removeBlocks[i]].blockId].weight;
            totalMass -= weight;
            weightedMass -= removeBlocks[i].transform.position * weight;
        }

        shipBody.mass = totalMass;
        shipBody.centerOfMass = ship.InverseTransformPoint(weightedMass / totalMass);
        shipBody.WakeUp();
    }

    private void AddWeights(Transform ship, List<GameObject> addBlocks) {
        Rigidbody shipBody = activeBodies[ship];
        float totalMass = shipBody.mass;
        Vector3 weightedMass = shipBody.centerOfMass * totalMass;
        for (int i = 0; i < addBlocks.Count; i++) {
            float weight = blockTypes[blocks[addBlocks[i]].blockId].weight;
            totalMass += weight;
            weightedMass += addBlocks[i].transform.localPosition * weight;
        }

        shipBody.mass = totalMass;
        shipBody.centerOfMass = weightedMass / totalMass;
        shipBody.WakeUp();
    }
    
    private Collider[] GetOverlaps(GameObject checkObject, Vector3 padding, LayerMask layerMask)
    {
        BoxCollider boxCollider = blocks[checkObject].blockCollider;
        Collider[] overlaps = Physics.OverlapBox(checkObject.transform.TransformPoint(boxCollider.center), boxCollider.size / 2 + padding, checkObject.transform.rotation, layerMask);
        List<Collider> finalOverlaps = new();

        for (int i = 0; i < overlaps.Length; i++) {
            if (overlaps[i].transform == checkObject.transform) continue;
            finalOverlaps.Add(overlaps[i]);
        }

        return finalOverlaps.ToArray();
    }

    private void ClearFaces(GameObject clearObject) {
        Block block = blocks[clearObject];
        // Remove the block from the objects it was attached to
        for (int i = 1; i <= 6; i++) {
            GameObject faceObject = block.GetFace(i);
            if (faceObject != null) {
                Block newBlock = blocks[faceObject];
                newBlock.DetachObject(clearObject);
                blocks[faceObject] = newBlock;
            }
        }

        for (int i = 1; i <= 6; i++) {
            block.InternalSetFace(i, null);
        }
        blocks[clearObject] = block; // Clear the block itself
        clearObject.transform.SetParent(null);
    }

    private void ManageDetachment(GameObject removedObject) {
        Block block = blocks[removedObject];
        List<GameObject> attachedBlocks = new();
        // Remove the block from the objects it was attached to as well
        for (int i = 1; i <= 6; i++) {
            GameObject faceObject = block.GetFace(i);
            if (faceObject != null) {
                attachedBlocks.Add(faceObject);
            }
        }

        Transform baseParent = removedObject.transform.parent;
        ClearFaces(removedObject);
        List<List<GameObject>> ships = new();

        int biggestList = 0;
        int biggestListSize = 0;
        while (attachedBlocks.Count > 0) {
            GameObject baseBlock = attachedBlocks[0];
            attachedBlocks.Remove(baseBlock);
            List<GameObject> connectedBlocks = GraphTraversal(baseBlock, ref attachedBlocks); // Check if baseblock is attached to any of the pathfindblocks using clear position as a reference point
            ships.Add(connectedBlocks);
            if (connectedBlocks.Count > biggestListSize) {
                biggestList = ships.Count;
                biggestListSize = connectedBlocks.Count;
            }
        }

        if (biggestList != 0) {
            ships.RemoveAt(biggestList - 1);
        }

        foreach (List<GameObject> connectedBlocks in ships) {
            RemoveWeights(baseParent, connectedBlocks);
            CreateShip(connectedBlocks);
        }

        if (baseParent.childCount == 0) {
            DestroyShip(baseParent);
        }
    }

    private List<GameObject> GraphTraversal(GameObject baseBlock, ref List<GameObject> trackBlocks) { // Pathfind towards the referenceposition using while checking if the current block is part of trackblocks
        Queue<GameObject> traversalQueue = new();
        List<GameObject> connectedBlocks = new();

        traversalQueue.Enqueue(baseBlock);
        connectedBlocks.Add(baseBlock);

        while (traversalQueue.Count > 0) {
            GameObject blockObj = traversalQueue.Dequeue();
            Block block = blocks[blockObj];

            for (int i = 1; i <= 6; i++) {
                GameObject attachedBlock = block.GetFace(i);
                if (attachedBlock != null && !connectedBlocks.Contains(attachedBlock)) {
                    traversalQueue.Enqueue(attachedBlock);
                    connectedBlocks.Add(attachedBlock);
                    if (trackBlocks.Contains(blockObj)) {
                        trackBlocks.Remove(blockObj);
                    }
                }
            }
        }

        return connectedBlocks;
    }

    private void SetFace(GameObject SetObject, int faceId, GameObject attachObject) {
        // Because a dictionary returns a copy of a struct you cant directly modify it
        Block block = blocks[SetObject];
        block.InternalSetFace(faceId, attachObject);
        blocks[SetObject] = block;
    }

    private Vector3 FaceIdToVector(int faceId) {
        Vector3[] vectors = { new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0) };
        return vectors[faceId - 1];
    }

    private Vector3 LocalFaceIdToVector(int faceId, Transform refObject) {
        Vector3[] vectors = { refObject.forward, -refObject.forward, -refObject.right , refObject.right, refObject.up, -refObject.up};
        return vectors[faceId - 1];
    }

    [System.Serializable]
    public struct BlockType {
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

        public GameObject GetFace(int faceId) {
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

        public void DetachObject(GameObject detachObject) {
            for (int i = 1; i <= 6; i++) {
                if (GetFace(i) == detachObject) {
                    InternalSetFace(i, null);
                    return;
                }
            }
        }
    }
}