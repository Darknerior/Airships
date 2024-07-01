using UnityEngine;
using System.Collections.Generic;
public class GridPlacement : MonoBehaviour
{
    [Range(0, 1)] public float edgeBias = 0, edgeAngleFactor = 0;
    [Range(0, 1)] public float rotationBias = 0, rotationAngleFactor = 0;
    [Range(0, 1)] public float closestBlockAngleWeight = 0;
    private BlockManager _blockManager;
    private PreviewManager _previewManager;
    public float rayPadding = 0;
    public float reach = 10;
    public LayerMask placementLayer; // The layers we can player on
    public LayerMask collisionLayer;
    public LayerMask airshipLayer;
    int airshipLayerIndex;
    Vector3 placementPosition;
    Quaternion placementRotation;

    // Temp keybinds
    public KeyCode place;
    public KeyCode edit;
    public KeyCode cancel;
    public PlaceState state;
    
    public enum PlaceState {
        nothing,
        place,
        edit,
        shipEdit
    };

    // Value holders for editing
    private int currentBlockId = 0;
    private GameObject editObject;
    private MeshRenderer editObjectRenderer;
    private Transform rotationReference;
    private Vector3 normal;

    private void Awake() {
        _blockManager = GetComponent<BlockManager>();
        _previewManager = GetComponent<PreviewManager>();
        _blockManager.Initialize();
        _previewManager.SetNewPreviewBlock(_blockManager.GetBlockTypeFromId(0));
        _previewManager.SetActive(false);

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
            _previewManager.SetActive(false);
            rotationReference = null;
            return;
        }

        _previewManager.SetActive(true);

        StateManager(hit);

        switch (state) { // What to do in each place state
            case PlaceState.nothing: return;
            case PlaceState.place: BlockPicker(); break;
            case PlaceState.edit: if (editObject == null) state = PlaceState.nothing; break;
        }

        CalculateTransform(hit);

        _previewManager.SetTransform(placementPosition, placementRotation);

        PlacementManager();
    }

    private void StateManager(RaycastHit hit) {
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
            _previewManager.SetActive(true);
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

                editObject = hit.collider.gameObject;
                currentBlockId = _blockManager.GetBlockId(editObject);
                _previewManager.SetNewPreviewBlock(_blockManager.GetBlockTypeFromId(currentBlockId));
                editObjectRenderer = editObject.GetComponent<MeshRenderer>();
                editObject.layer = 0;
                editObjectRenderer.enabled = false;
                return;
            }
        }
    }

    private void PlacementManager() {
        BoxCollider collider = _blockManager.GetBlockTypeFromId(currentBlockId).collider;
        if (Input.GetMouseButtonDown(0) && PlacementUtils.GetCollisionsFromPoint(placementPosition + placementRotation * collider.center, collider.size, PlacementUtils.collisionPadding, placementRotation, collisionLayer).Length == 0) { // Left mouse click
            GameObject placedObject = null;

            if (state == PlaceState.place) {
                placedObject = _blockManager.CreateBlock(currentBlockId, placementPosition, placementRotation, airshipLayerIndex);
            }

            if (state == PlaceState.edit) {
                placedObject = editObject;
                editObjectRenderer.enabled = true;
                editObject.layer = airshipLayerIndex;
                _blockManager.DetachBlock(editObject); // Clear the face attachments of the edited block
                editObject.transform.SetPositionAndRotation(placementPosition, placementRotation);
                CancelEdit();
            }

            Collider[] surroundingObjects = PlacementUtils.GetOverlaps(placedObject, _blockManager.GetBlockType(placedObject).collider, PlacementUtils.overlapPadding + Vector3.one, airshipLayer);
            _blockManager.CombineAdjacentBlocks(placedObject, surroundingObjects);
        }
    }

    private void CancelEdit() {
        editObjectRenderer.enabled = true;
        editObject.layer = airshipLayerIndex;
        editObject = null;
        _previewManager.SetActive(false);
        currentBlockId = 0;
        state = PlaceState.nothing;
    }

    private void CancelPlace() {
        _previewManager.SetActive(false);
        state = PlaceState.nothing;
    }

    private void BlockPicker() {
        int scrolls = (int)Input.mouseScrollDelta.y;
        int newBlockId = (currentBlockId + scrolls) % _blockManager.blockTypes.Length;
        if (newBlockId < 0) {
            newBlockId += _blockManager.blockTypes.Length;
        }

        if (newBlockId != currentBlockId) {
            currentBlockId = newBlockId;
            _previewManager.SetNewPreviewBlock(_blockManager.GetBlockTypeFromId(newBlockId));
        }
    }

    private void CalculateTransform(RaycastHit hit) {
        // Adjust position to either ground hit or snap point
        rotationReference = hit.collider.transform;
        placementPosition = CalculatePlacementPosition(hit);
        placementRotation = GetBaseRotation(rotationReference.rotation, placementPosition, hit);
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit) {
        normal = hit.normal;
        var placementPosition = hit.point + PlacementUtils.GetOffset(_blockManager.GetBlockTypeFromId(currentBlockId).collider, normal);

        bool canAttach = true;

        if (hit.collider.gameObject.layer == airshipLayerIndex)
        {
            Vector3 localPlacePos = GetBiasedLocalSnapPoint(hit, edgeBias);
            int faceId = PlacementUtils.FaceIdFromVector(localPlacePos.normalized);
            if (_blockManager.GetBlockType(hit.collider.gameObject).CanAttach(faceId)) placementPosition = hit.collider.transform.TransformPoint(localPlacePos);
            else canAttach = false;
        }

        if (!canAttach || PlacementUtils.GetCollisionsFromPoint(placementPosition, Vector3.one, PlacementUtils.collisionPadding, rotationReference.rotation, collisionLayer).Length > 0) { // If you try to place inside another block
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
                    angleFactor = (1 - angleFactor) * closestBlockAngleWeight + 1 * (1 - closestBlockAngleWeight);
                    float newValue = Vector3.Distance(tempPos, hit.point) * angleFactor;

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

    private Quaternion GetBaseRotation(Quaternion baseRotation, Vector3 basePosition, RaycastHit hit) {
        if (hit.collider.gameObject.layer != airshipLayerIndex) return Quaternion.identity;

        Vector4 edgeData = PlacementUtils.GetClosestEdgeData(hit, _blockManager.GetBlockType(hit.collider.gameObject).collider);

        Vector3 inverseEdgeDistance = hit.collider.transform.TransformDirection(edgeData).normalized;
        float maxValue = edgeData.w;

        float CloseToAngleFactor = Vector3.Angle(inverseEdgeDistance.normalized, Camera.main.transform.forward) % 180f / 180f;

        if (0.5f - maxValue > rotationBias || normal != hit.normal || CloseToAngleFactor > 1f - rotationAngleFactor) {
            inverseEdgeDistance = -normal;
        }
        
        Quaternion rotation = PlacementUtils.SafeFromToRotation(baseRotation * Vector3.down, inverseEdgeDistance, rotationReference);

        BlockType currentBlockType = _blockManager.GetBlockTypeFromId(currentBlockId);

        Vector3 toRotationReference = (rotationReference.position - basePosition).normalized;
        int rotObjectSnappoint = PlacementUtils.CalculateLocalFaceIdFromRotation(baseRotation, toRotationReference);

        if (!currentBlockType.CanAttach(rotObjectSnappoint) || PlacementUtils.GetCollisionsFromPoint(baseRotation * rotation * currentBlockType.collider.center + basePosition, currentBlockType.collider.size, PlacementUtils.collisionPadding, baseRotation * rotation, collisionLayer).Length > 0) // Get an alternative rotation when overlapping with something
        {
            Vector3[] adjacentVectors = PlacementUtils.GetAdjacentVectors(hit.collider.transform.InverseTransformDirection(normal));
            if (adjacentVectors == null) return rotation * baseRotation;

            for (int i = 0; i < 4; i++)
            {
                rotation = PlacementUtils.SafeFromToRotation(baseRotation * Vector3.down, hit.collider.transform.TransformDirection(adjacentVectors[i]), rotationReference);
                rotObjectSnappoint = PlacementUtils.CalculateLocalFaceIdFromRotation(baseRotation, rotation * toRotationReference);
                if (rotObjectSnappoint == 0 || PlacementUtils.GetCollisionsFromPoint(baseRotation * rotation * currentBlockType.collider.center + basePosition, currentBlockType.collider.size, PlacementUtils.collisionPadding, baseRotation * rotation, collisionLayer).Length > 0) continue;
                return rotation * baseRotation;
            }
        }

        return rotation * baseRotation;
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
}