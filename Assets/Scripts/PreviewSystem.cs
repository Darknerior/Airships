using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewManager : MonoBehaviour
{
    [Range(0, 1)] public float previewOpacity;
    Transform _previewCube;

    public void SetNewPreviewBlock(BlockType blockType)
    {
        if (_previewCube != null) Destroy(_previewCube.gameObject);
        _previewCube = CreatePreviewBlock(blockType).transform;
    }

    public void SetActive(bool active)
    {
        _previewCube.gameObject.SetActive(active);
    }

    public void SetTransform(Vector3 position, Quaternion rotation)
    {
        _previewCube.SetPositionAndRotation(position, rotation);
    }

    GameObject CreatePreviewBlock(BlockType blockType)
    {
        GameObject newPreviewBlock = Instantiate(blockType.blockPrefab, Vector3.zero, Quaternion.identity);
        newPreviewBlock.layer = LayerMask.NameToLayer("Preview");
        SetPreviewOpacity(newPreviewBlock, blockType);
        BoxCollider newCollider = newPreviewBlock.GetComponent<BoxCollider>();
        newCollider.isTrigger = true;
        return newPreviewBlock;
    }

    private void SetPreviewOpacity(GameObject obj, BlockType blockType)
    {
        if (!obj.TryGetComponent(out Renderer renderer)) return;
        var cloneMat = new Material(blockType.material);

        cloneMat.SetFloat("_Mode", 3);
        cloneMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        cloneMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        cloneMat.SetInt("_ZWrite", 0);
        cloneMat.DisableKeyword("_ALPHATEST_ON");
        cloneMat.EnableKeyword("_ALPHABLEND_ON");
        cloneMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        cloneMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color newColor = cloneMat.color;
        newColor.a = previewOpacity;
        cloneMat.color = newColor;
        renderer.material = cloneMat;
    }
}
