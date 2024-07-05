using UnityEngine;
using System.Collections.Generic;
public class GridPlacement : MonoBehaviour
{
    #region Class Variables
    public PlaceStateSettings placeStateSettings;
    private BlockManager _blockManager;
    private PreviewManager _previewManager;
    private WiringManager _wiringManager;
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
    public KeyCode wirePlace;
    public PlaceState state;
    
    public enum PlaceState {
        nothing = -1,
        place = 0,
        edit = 1,
        wirePlace = 2
    };

    // Value holders for editing
    private int currentBlockId = 0;
    private GameObject editObject;
    private MeshRenderer editObjectRenderer;
    private Transform rotationReference;
    private Vector3 normal;
    #endregion

    #region Unity Messages
    private void Awake() {
        _blockManager = GetComponent<BlockManager>();
        _previewManager = GetComponent<PreviewManager>();
        _wiringManager = GetComponent<WiringManager>();
        _wiringManager.Initialize();
        _blockManager.Initialize();
        _previewManager.SetNewPreviewBlock(_blockManager.GetBlockTypeFromId(0).blockPrefab, _blockManager.GetBlockTypeFromId(0).material);
        _previewManager.SetActive(false);

        airshipLayerIndex = PlacementUtils.LayerMaskToLayer(airshipLayer);
    }

    private void Update() {
        Vector3 direction = Camera.main.transform.forward;
        Vector3 camPos = Camera.main.transform.position;
        float paddingRadius = rayPadding * 0.5f;

        RaycastHit hit = PlacementUtils.GetPaddedHit(camPos, direction, paddingRadius, reach, airshipLayer);

        if (hit.collider == null && !Physics.Raycast(camPos, direction, out hit, Mathf.Infinity, placementLayer))
        {
            _previewManager.SetActive(false);
            rotationReference = null;
            return;
        }

        StateManager(hit);

        switch (state) { // What to do in each place state
            case PlaceState.nothing: _previewManager.SetActive(false);  return;
            case PlaceState.place: Place(hit); break;
            case PlaceState.edit: Edit(hit); break;
            case PlaceState.wirePlace: WirePlace(hit); break;
        }

        _previewManager.SetActive(true);

        _previewManager.SetTransform(placementPosition, placementRotation);

        PlacementManager();
    }

    #endregion

    #region Managers

    private void StateManager(RaycastHit hit) {
        if (Input.GetKeyDown(cancel)) {
            if (state == PlaceState.edit) CancelEdit();
            if (state == PlaceState.place) CancelPlace();
            if (state == PlaceState.wirePlace) CancelWirePlace();

            state = PlaceState.nothing;
            return;
        }
        if (Input.GetKeyDown(place) && state == PlaceState.nothing) {
            if (state == PlaceState.place) {
                CancelPlace();
                return;
            }

            currentBlockId = -1;
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

            if (state == PlaceState.nothing && hit.collider.gameObject.layer == airshipLayerIndex) {
                state = PlaceState.edit;

                editObject = hit.collider.gameObject;
                currentBlockId = _blockManager.GetBlockId(editObject);
                BlockType newBlockType = _blockManager.GetBlockTypeFromId(0);
                _previewManager.SetNewPreviewBlock(newBlockType.blockPrefab, newBlockType.material);
                editObjectRenderer = editObject.GetComponent<MeshRenderer>();
                editObject.layer = 0;
                editObjectRenderer.enabled = false;
                return;
            }
        }
        if (Input.GetKeyDown(wirePlace))
        {
            if (state == PlaceState.wirePlace)
            {
                CancelWirePlace();
                state = PlaceState.nothing;
                return;
            }

            if (state == PlaceState.nothing)
            {
                currentBlockId = -1; 
                state = PlaceState.wirePlace;
                _previewManager.SetActive(true);
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

    private int IdPicker(int idCount) {
        int scrolls = (int)Input.mouseScrollDelta.y;
        int newBlockId = (int)Mathf.Repeat(currentBlockId + scrolls, idCount);
        return newBlockId;
    }

    #endregion

    #region Placement Types

    void Place(RaycastHit hit)
    {
        int newBlockId = IdPicker(_blockManager.blockTypes.Length);
        if (newBlockId != currentBlockId)
        {
            currentBlockId = newBlockId;
            BlockType newBlockType = _blockManager.GetBlockTypeFromId(newBlockId);
            _previewManager.SetNewPreviewBlock(newBlockType.blockPrefab, newBlockType.material);
        }
        CalculateTransform(hit, placeStateSettings);
    }

    void Edit(RaycastHit hit)
    {
        if (editObject == null)
        {
            state = PlaceState.nothing;
            return;
        }

        CalculateTransform(hit, placeStateSettings);
    }

    void WirePlace(RaycastHit hit)
    {
        int newBlockId = IdPicker(_wiringManager.wireTypes.Length);
        if (newBlockId != currentBlockId)
        {
            currentBlockId = newBlockId;
            WireType newWireType = _wiringManager.GetWireTypeFromId(newBlockId);
            _previewManager.SetNewPreviewBlock(newWireType.wirePrefab, newWireType.material);
        }
        CalculateWireTransform(hit);
    }

    #endregion

    #region Calculate Transform

    private void CalculateTransform(RaycastHit hit, PlaceStateSettings settings) {
        // Adjust position to either ground hit or snap point
        rotationReference = hit.collider.transform;
        placementPosition = CalculatePlacementPosition(hit, settings.edgeBias, settings.edgeMinAngleAlignment, settings.closestBlockAngleWeight);
        placementRotation = CalculateBaseRotation(rotationReference.rotation, placementPosition, hit, settings.rotateEdgeDistance, settings.rotationMinAngleAlignment);
    }

    private void CalculateWireTransform(RaycastHit hit)
    {
        rotationReference = hit.collider.transform;
        placementPosition = CalculateWirePosition(hit);
        placementRotation = PlacementUtils.SafeFromToRotation(rotationReference.up, hit.normal);
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit, float edgeBias, float edgeMinAngleAlignment, float closestBlockAngleWeight) {
        normal = hit.normal;
        var placementPosition = hit.point + PlacementUtils.GetOffset(_blockManager.GetBlockTypeFromId(currentBlockId).collider, normal);

        bool canAttach = true;

        if (hit.collider.gameObject.layer == airshipLayerIndex)
        {
            Vector3 localPlacePos = PlacementUtils.GetBiasedLocalSnapPoint(hit, _blockManager.GetBlockType(hit.collider.gameObject).collider, edgeBias, edgeMinAngleAlignment);
            normal = hit.collider.transform.TransformDirection(localPlacePos);
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
                    float angleFactor = Vector3.Angle(-tempNormal, Camera.main.transform.forward) % 180f / 180f;
                    angleFactor = angleFactor * closestBlockAngleWeight;
                    float newValue = Vector3.Distance(tempPos, hit.point) * (1 + angleFactor);

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

    private Quaternion CalculateBaseRotation(Quaternion baseRotation, Vector3 basePosition, RaycastHit hit, float rotateEdgeDistance, float rotationMinAngleAlignment) {
        if (hit.collider.gameObject.layer != airshipLayerIndex) return Quaternion.identity;

        Vector4 edgeData = PlacementUtils.GetClosestEdgeData(hit, _blockManager.GetBlockType(hit.collider.gameObject).collider);

        Vector3 inverseEdgeDistance = hit.collider.transform.TransformDirection(edgeData).normalized;
        float maxValue = edgeData.w;

        float CloseToAngleFactor = Vector3.Angle(inverseEdgeDistance.normalized, Camera.main.transform.forward) % 180f / 180f;

        if (0.5f - maxValue > rotateEdgeDistance || normal != hit.normal || CloseToAngleFactor > 1f - rotationMinAngleAlignment) {
            inverseEdgeDistance = -normal;
        }
        
        Quaternion rotation = PlacementUtils.SafeFromToRotation(baseRotation * Vector3.down, inverseEdgeDistance);

        BlockType currentBlockType = _blockManager.GetBlockTypeFromId(currentBlockId);

        Vector3 toRotationReference = (rotationReference.position - basePosition).normalized;
        int rotObjectSnappoint = PlacementUtils.CalculateLocalFaceIdFromRotation(baseRotation, toRotationReference);

        if (!currentBlockType.CanAttach(rotObjectSnappoint) || PlacementUtils.GetCollisionsFromPoint(baseRotation * rotation * currentBlockType.collider.center + basePosition, currentBlockType.collider.size, PlacementUtils.collisionPadding, baseRotation * rotation, collisionLayer).Length > 0) // Get an alternative rotation when overlapping with something
        {
            Vector3[] adjacentVectors = PlacementUtils.GetAdjacentVectors(hit.collider.transform.InverseTransformDirection(normal));
            if (adjacentVectors == null) return rotation * baseRotation;

            for (int i = 0; i < 4; i++)
            {
                rotation = PlacementUtils.SafeFromToRotation(baseRotation * Vector3.down, hit.collider.transform.TransformDirection(adjacentVectors[i]));
                rotObjectSnappoint = PlacementUtils.CalculateLocalFaceIdFromRotation(baseRotation, rotation * toRotationReference);
                if (rotObjectSnappoint == 0 || PlacementUtils.GetCollisionsFromPoint(baseRotation * rotation * currentBlockType.collider.center + basePosition, currentBlockType.collider.size, PlacementUtils.collisionPadding, baseRotation * rotation, collisionLayer).Length > 0) continue;
                return rotation * baseRotation;
            }
        }

        return rotation * baseRotation;
    }

    private Vector3 CalculateWirePosition(RaycastHit hit)
    {
        if (hit.collider.gameObject.layer != airshipLayerIndex)
        {
            _previewManager.SetActive(false);
            return Vector3.zero;
        }

        return hit.collider.transform.TransformPoint(PlacementUtils.GetOffset(_blockManager.GetBlockType(hit.collider.gameObject).collider, hit.normal) * 2f);
    }

    #endregion

    #region CancelMethods
    private void CancelEdit()
    {
        editObjectRenderer.enabled = true;
        editObject.layer = airshipLayerIndex;
        editObject = null;
        _previewManager.SetActive(false);
        currentBlockId = 0;
        state = PlaceState.nothing;
    }

    private void CancelPlace()
    {
        _previewManager.SetActive(false);
        state = PlaceState.nothing;
    }

    private void CancelWirePlace()
    {
        state = PlaceState.nothing;
    }

    #endregion

    [System.Serializable]
    public struct PlaceStateSettings
    {
        [Range(0, 1)] public float edgeBias, edgeMinAngleAlignment;
        [Range(0, 1)] public float rotateEdgeDistance, rotationMinAngleAlignment;
        [Range(0, 1)] public float closestBlockAngleWeight;
    }
}