using UnityEngine;

public class GridPlacement : MonoBehaviour
{
    public GameObject cubePrefab;
    private GameObject _previewCube;
    private Transform _airshipParent;
    public Material previewMaterial; 
    public LayerMask placementLayer; // The layers we can player on
    private readonly LayerMask _airshipLayer = 3; // Layer 3 for airships
    private BoxCollider _boxCollider;

    private void Start() {
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
                var placedObject = Instantiate(cubePrefab, placementPosition, Quaternion.identity);
                placedObject.layer = _airshipLayer; // Set to airship layer
                placedObject.transform.parent = _airshipParent != null ? _airshipParent : new GameObject("Airship").transform;//Set parent
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
}