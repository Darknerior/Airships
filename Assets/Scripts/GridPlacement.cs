using UnityEngine;

public class GridPlacement : MonoBehaviour
{
    public GameObject cubePrefab;
    private GameObject _previewCube;
    private GameObject _previewShip;
    private Transform _interactionCube;
    private Transform _airshipParent;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    private BoxCollider _boxCollider;

    // Value holders for editing
    public GameObject editObject;
    private Vector3 pivotOffset;
    private Vector3 originalPosition;

    // Temp keybinds
    public KeyCode place;
    public KeyCode edit;
    public KeyCode cancel;
    public PlaceState state;
    
    public enum PlaceState {
        nothing,
        place,
        editCube,
        editShip
    };

    private void Start() {
        _previewCube = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
        SetPreviewOpacity(_previewCube, previewMaterial);
        _previewCube.SetActive(false);
        _boxCollider = _previewCube.GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;//Prevent player colliding with preview
    }

    private void Update() {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayer)) {
            StateManager();

            switch (state) { // What to do in each place state
                case PlaceState.place: PlaceCube(hit); break;
                case PlaceState.editCube: EditCube(hit); break;
                case PlaceState.editShip: EditShip(hit); break;
            }
        }
        else _previewCube.SetActive(false);
    }

    private void StateManager(){
        if (Input.GetKeyDown(cancel)) {
            if (state == PlaceState.editShip) CancelShipEdit();
            if (state == PlaceState.editCube) CancelEdit();
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
            if (state == PlaceState.editShip){
                CancelShipEdit();
                return;
            }

            if (state == PlaceState.editCube)
            {
                // Get the offset between the ship and the selected block for convenient movement
                pivotOffset = editObject.transform.position - editObject.transform.parent.position;
                editObject.SetActive(true);
                editObject = editObject.transform.parent.gameObject;
                editObject.SetActive(false);
                _previewCube.SetActive(false);
                state = PlaceState.editShip;
                return;
            }

            if (state == PlaceState.nothing) {
                state = PlaceState.editCube;
                return;
            }
        }
    }

    private void EditShip(RaycastHit hit) {
        var placementPosition = CalculatePlacementPosition(hit);
        if (_previewShip == null)
        {
            // Generate a preview of the ship
            _previewShip = Instantiate(editObject);
            _previewShip.SetActive(true);
            _previewShip.name = "preview";
            foreach (Transform child in _previewShip.transform)
            {
                child.gameObject.layer = LayerMask.NameToLayer("Preview");

                SetPreviewOpacity(child.gameObject, previewMaterial);
            }
        }

        _previewShip.transform.position = placementPosition - pivotOffset; // Offset the position of the preview to align with the look position
        _previewShip.transform.rotation = Quaternion.identity;

        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            editObject.SetActive(true);
            editObject.transform.position = _previewShip.transform.position;
            if (_airshipParent != null)
            {
                while (editObject.transform.childCount > 0) { // Set the all the children to the new airship
                    Transform child = editObject.transform.GetChild(editObject.transform.childCount - 1);
                    child.SetParent(_airshipParent);
                    child.gameObject.layer = LayerMask.NameToLayer("Preview");
                }
                Destroy(editObject);
            }
            CancelShipEdit();
        }
    }

    private void EditCube(RaycastHit hit) {
        GameObject hitObject = hit.transform.gameObject;

        if (editObject == null && hitObject.transform.CompareTag("Moveable")) {
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

    private void CancelEdit() {
        editObject.SetActive(true);
        editObject = null;
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void CancelShipEdit() {
        Destroy(_previewShip);
        _previewShip = null;
        editObject.SetActive(true);
        editObject = null;
        state = PlaceState.nothing;
    }

    private void CancelPlace() {
        _previewCube.SetActive(false);
        state = PlaceState.nothing;
    }

    private void PlaceCube(RaycastHit hit) {
        _previewCube.SetActive(true);

        // Adjust position to either ground hit or snap point
        var placementPosition = CalculatePlacementPosition(hit);

        _previewCube.transform.position = placementPosition;
        _previewCube.transform.rotation = Quaternion.identity; // Reset rotation or set to snap point rotation

        if (Input.GetMouseButtonDown(0)) // Left mouse click
        {
            // Instantiate the actual object or use the edited cube
            var placedObject = editObject;

            if (editObject == null) {
                placedObject = Instantiate(cubePrefab, placementPosition, Quaternion.identity);
                placedObject.transform.tag = "Moveable";
            }
            else {
                placedObject.SetActive(true);
                placedObject.transform.position = placementPosition;
                placedObject.transform.rotation = Quaternion.identity;
                CancelEdit();
            }

            placedObject.layer = _airshipLayer; // Set to airship layer
            placedObject.transform.parent = _airshipParent != null ? _airshipParent : new GameObject("Airship").transform;//Set parent
        }
    }

    private Vector3 CalculatePlacementPosition(RaycastHit hit) {
        var placementPosition = new Vector3(0,0,0);
        
        // Check if hit object is part of the airship and find the closest snap point if applicable
        if (hit.collider.gameObject.layer == _airshipLayer) {
            // If block editing is going on make sure the airship is correct
            _airshipParent = hit.transform.parent;
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
}