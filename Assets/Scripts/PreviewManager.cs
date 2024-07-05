using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewManager : MonoBehaviour
{
    [Range(0, 1)] public float previewOpacity;
    Transform _previewBlock;

    public void SetNewPreviewBlock(GameObject prefab, Material material)
    {
        if (_previewBlock != null) Destroy(_previewBlock.gameObject);
        GameObject newPreviewBlock = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        newPreviewBlock.layer = LayerMask.NameToLayer("Preview");
        SetPreviewOpacity(newPreviewBlock, material);
        BoxCollider newCollider = newPreviewBlock.GetComponent<BoxCollider>();
        newCollider.isTrigger = true;
        _previewBlock = newPreviewBlock.transform;
    }

    public void SetActive(bool active)
    {
        if (_previewBlock == null) return;
        _previewBlock.gameObject.SetActive(active);
    }

    public void SetTransform(Vector3 position, Quaternion rotation)
    {
        _previewBlock.SetPositionAndRotation(position, rotation);
    }

    private void SetPreviewOpacity(GameObject obj, Material material)
    {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        Material newMaterial = new(material);
        newMaterial.SetFloat("_Mode", 3);
        newMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        newMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        newMaterial.SetInt("_ZWrite", 0);
        newMaterial.DisableKeyword("_ALPHATEST_ON");
        newMaterial.EnableKeyword("_ALPHABLEND_ON");
        newMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        newMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color newColor = newMaterial.color;
        newColor.a = previewOpacity;
        newMaterial.color = newColor;
        renderer.material = newMaterial;
    }
}
