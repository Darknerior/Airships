using UnityEngine;
using System.Collections.Generic;
public class GridPlacement : MonoBehaviour
{
    [Range(0, 1)] public float edgeBias = 0, edgeAngleFactor = 0;
    [Range(0, 1)] public float rotationBias = 0, rotationAngleFactor = 0;
    [Range(0, 1)] public float closestBlockAngleWeight = 0;
    private BlockManager _blockManager;
    public float rayPadding = 0;
    public float reach = 10;
    public GameObject airshipPrefab;
    private GameObject _previewCube;
    private GameObject _previewAirship;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    public LayerMask collisionLayer;
    public LayerMask airshipLayer;
    int airshipLayerIndex;

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
        edit
    };

    // Value holders for editing
    private int currentBlockId = 0;
    private GameObject editObject;
    private MeshRenderer editObjectRenderer;
    private GameObject _previewEditObject;
    private GameObject shipObject;
    private Vector3 editOffset;
    private Transform previousHitBlock; // Keep track of the previous block hit by the raycast for accurate snaprotation
    private Transform rotationReference;
    private Vector3 baseRotation;
    private Vector3 normal;

    private void Start() {
        _blockManager = GetComponent<BlockManager>();
        _previewCube = CreatePreviewBlock(0);
        airshipLayerIndex = PlacementUtils.LayerMaskToLayer(airshipLayer);
    }

    private void Update() {
        Vector3 direction = Camera.main.transform.forward;
        Vector3 camPos = Camera.main.transform.position;
        float paddingRadius = rayPadding * 0.5f;

        Collider closestBlock = null;

        if (!Physics.Raycast(camPos, direction, out RaycastHit hit, Mathf.Infinity, airshipLayer))
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
                    float closer = Mathf.Min(closestDistance, distanceFromRay);
                    if (closer < closestDistance && Physics.Raycast(lineIntersection, block.transform.position - lineIntersection, out RaycastHit newHit, Mathf.Infinity, airshipLayer)) {
                        closestDistance = closer;
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
        BoxCollider collider = _blockManager.GetBlockTypeFromId(currentBlockId).collider;
        if (Input.GetMouseButtonDown(0) && PlacementUtils.GetCollisionsFromPoint(_previewCube.transform.TransformPoint(collider.center), collider.size, PlacementUtils.collisionPadding, _previewCube.transform.rotation, collisionLayer).Length == 0) { // Left mouse click
            GameObject placedObject = null;

            if (state == PlaceState.place) {
                placedObject = _blockManager.CreateBlock(currentBlockId, _previewCube.transform.position, _previewCube.transform.rotation, airshipLayerIndex);
            }

            if (state == PlaceState.edit) {
                placedObject = editObject;
                editObjectRenderer.enabled = true;
                editObject.layer = airshipLayerIndex;
                _blockManager.DetachBlock(editObject); // Clear the face attachments of the edited block
                editObject.transform.SetPositionAndRotation(_previewCube.transform.position, _previewCube.transform.rotation);
                CancelEdit();
            }

            Collider[] surroundingObjects = PlacementUtils.GetOverlaps(placedObject, _blockManager.GetBlockType(placedObject).collider, PlacementUtils.overlapPadding + Vector3.one, airshipLayer);
            Debug.Log(surroundingObjects.Length);
            _blockManager.CombineAdjacentBlocks(placedObject, surroundingObjects);
        }
    }

    private void AttachBlockTo(GameObject ToAttach, GameObject AttachTo, int selfId, int AttachId) {

    }

    private void CancelEdit() {
        editObjectRenderer.enabled = true;
        editObject.layer = airshipLayerIndex;
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

        if (editObject == null && hitObject.layer == airshipLayerIndex) {
            editObject = hitObject;
            _previewCube = CreatePreviewBlock(_blockManager.GetBlockId(hitObject));
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

    private void BlockPicker() {
        int scrolls = (int)Input.mouseScrollDelta.y;
        int newBlockId = (currentBlockId + scrolls) % _blockManager.blockTypes.Length;
        if (newBlockId < 0) {
            newBlockId += _blockManager.blockTypes.Length;
        }

        if (newBlockId != currentBlockId) {
            currentBlockId = newBlockId;
            Destroy(_previewCube);
            _previewCube = CreatePreviewBlock(newBlockId);
        }
    }

    private void SetPreviewCube(RaycastHit hit) {
        // Adjust position to either ground hit or snap point
        rotationReference = hit.collider.transform;
        var placementPosition = CalculatePlacementPosition(hit);
        _previewCube.transform.position = placementPosition;
        _previewCube.transform.rotation = rotationReference.rotation;
        _previewCube.transform.rotation = GetBaseRotation(_previewCube, hit);
    }

    GameObject CreatePreviewBlock(int blockId) {
        GameObject newPreviewBlock = _blockManager.CreateBlock(blockId, Vector3.zero, Quaternion.identity, LayerMask.NameToLayer("Preview"));
        SetPreviewOpacity(newPreviewBlock, previewMaterial);
        BoxCollider newCollider = newPreviewBlock.GetComponent<BoxCollider>();
        newCollider.isTrigger = true;
        return newPreviewBlock;
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit) {
        normal = hit.normal;
        var placementPosition = hit.point + PlacementUtils.GetOffset(_blockManager.GetBlockTypeFromId(currentBlockId).collider, normal);

        if (hit.collider.gameObject.layer == airshipLayerIndex)
        {
            Vector3 localPlacePos = GetBiasedLocalSnapPoint(hit, edgeBias);
            int faceId = PlacementUtils.FaceIdFromVector(localPlacePos.normalized);
            if (_blockManager.GetBlockType(hit.collider.gameObject).CanAttach(faceId)) placementPosition = hit.collider.transform.TransformPoint(localPlacePos);
        }

        if (PlacementUtils.GetCollisionsFromPoint(placementPosition, Vector3.one, PlacementUtils.collisionPadding, rotationReference.rotation, collisionLayer).Length > 0) { // If you try to place inside another block
            Collider[] surroundingCubes = PlacementUtils.GetCollisionsFromPoint(placementPosition, Vector3.one * 0.5f, Vector3.one, rotationReference.rotation, airshipLayer);
            float bestValue = float.MaxValue;

            for (int i = 0; i < surroundingCubes.Length; i++) {
                Transform blockTransform = surroundingCubes[i].transform;
                BlockType placedOnBlockType = _blockManager.GetBlockType(blockTransform.gameObject);
       
                for (int j = 1; j <= 6; j++)
                {
                    if (!placedOnBlockType.CanAttach(j)) continue;

                    Vector3 tempPos = blockTransform.TransformPoint(PlacementUtils.GetOffset(placedOnBlockType.collider, PlacementUtils.FaceIdToVector(j)) * 2f);
                    Vector3 tempNormal = (tempPos - blockTransform.position).normalized;
                    float angleFactor = Vector3.Angle(tempNormal, Camera.main.transform.forward) % 180f / 180f;
                    angleFactor = (1 - angleFactor) * closestBlockAngleWeight;
                    float newValue = Vector3.Distance(tempPos, hit.point);

                    if (newValue > bestValue || PlacementUtils.GetCollisionsFromPoint(tempPos, Vector3.one, PlacementUtils.collisionPadding, blockTransform.rotation, collisionLayer).Length > 0) continue;

                    normal = tempNormal;
                    placementPosition = tempPos;
                    rotationReference = blockTransform;
                    bestValue = newValue;
                }
            }
        }

        return placementPosition;
    }

    private Quaternion GetBaseRotation(GameObject rotObject, RaycastHit hit) {
        if (hit.collider.gameObject.layer != airshipLayerIndex) return Quaternion.identity;

        Vector4 edgeData = PlacementUtils.GetClosestEdgeData(hit, _blockManager.GetBlockType(hit.collider.gameObject).collider);

        Vector3 inverseEdgeDistance = hit.collider.transform.TransformDirection(edgeData).normalized;
        float maxValue = edgeData.w;

        float CloseToAngleFactor = Vector3.Angle(inverseEdgeDistance.normalized, Camera.main.transform.forward) % 180f / 180f;

        if (0.5f - maxValue > rotationBias || normal != hit.normal || CloseToAngleFactor > 1f - rotationAngleFactor) {
            inverseEdgeDistance = -normal;
        }
        
        Quaternion rotation = PlacementUtils.SafeFromToRotation(-rotObject.transform.up, inverseEdgeDistance, rotationReference);

        BlockType currentBlockType = _blockManager.GetBlockTypeFromId(currentBlockId);

        Vector3 toRotationReference = (rotationReference.position - rotObject.transform.position).normalized;
        int rotObjectSnappoint = PlacementUtils.CalculateLocalFaceId(toRotationReference, rotObject.transform);


        if (!currentBlockType.CanAttach(rotObjectSnappoint) || PlacementUtils.GetCollisionsFromPoint(rotObject.transform.TransformPoint(rotation * currentBlockType.collider.center), currentBlockType.collider.size, PlacementUtils.collisionPadding, rotObject.transform.rotation, collisionLayer).Length > 0) // Get an alternative rotation when overlapping with something
        {
            Vector3[] adjacentVectors = PlacementUtils.GetAdjacentVectors(rotationReference.InverseTransformDirection(normal));
            for (int i = 0; i < 4; i++)
            {
                rotation = PlacementUtils.SafeFromToRotation(-rotObject.transform.up, hit.collider.transform.TransformDirection(adjacentVectors[i]), rotationReference);
                rotObjectSnappoint = PlacementUtils.CalculateLocalFaceId(rotation * toRotationReference, rotObject.transform);
                if (rotObjectSnappoint == 0 || PlacementUtils.GetCollisionsFromPoint(rotObject.transform.TransformPoint(rotation * currentBlockType.collider.center), currentBlockType.collider.size, PlacementUtils.collisionPadding, rotation * rotObject.transform.rotation, collisionLayer).Length > 0) continue;
                return rotation * rotObject.transform.rotation;
            }
        }

        return rotation * rotObject.transform.rotation;
    }

    private Vector3 GetBiasedLocalSnapPoint(RaycastHit hit, float bias)
    {
        if (hit.collider.gameObject.layer != airshipLayerIndex) return Vector3.zero;
        Vector4 edgeData = PlacementUtils.GetClosestEdgeData(hit, _blockManager.GetBlockType(hit.collider.gameObject).collider);

        Vector3 inverseEdgeDistance = edgeData;
        float maxValue = edgeData.w;

        float CloseToAngleFactor = Vector3.Angle(inverseEdgeDistance.normalized, Camera.main.transform.forward) % 180f / 180f;

        if (0.5f - maxValue > bias || CloseToAngleFactor < edgeAngleFactor)
        {
            inverseEdgeDistance = hit.collider.transform.InverseTransformDirection(hit.normal);
        }

        normal = hit.collider.transform.TransformDirection(inverseEdgeDistance.normalized);

        return inverseEdgeDistance.normalized;
    }

    private void SetPreviewOpacity(GameObject obj, Material previewMat) {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        var cloneMat = new Material(previewMat);
        renderer.material = cloneMat;
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
}