using TMPro;
using UnityEngine;

public class TVStageDisplay : MonoBehaviour
{
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Texture2D[] images;
    [SerializeField] private TextMeshPro stageText;

    private MaterialPropertyBlock properties;
    private MineAssembler owner;
    [SerializeField] private int currentStage;

    public TextMeshPro StageText => stageText;
    public int CurrentStage => currentStage;

    private void Awake()
    {
        ShowDefault();
    }

    public bool TryClaim(MineAssembler assembler)
    {
        if (assembler == null) return false;
        if (owner != null && owner != assembler) return false;
        owner = assembler;
        return true;
    }

    public void Release(MineAssembler assembler)
    {
        if (owner != assembler) return;
        owner = null;
        ShowDefault();
    }

    public void SetStage(MineAssembler assembler, int stage, string label)
    {
        if (assembler == null || assembler != owner) return;

        // Index 0 is reserved for the unclaimed/default screen.
        int imageIndex = stage + 1;
        if (stageText != null) stageText.text = label;
        ShowImage(imageIndex);
    }

    private void ShowDefault()
    {
        if (stageText != null) stageText.text = string.Empty;
        ShowImage(0);
    }

    private void ShowImage(int imageIndex)
    {
        currentStage = imageIndex;
        if (screenRenderer == null || images == null ||
            imageIndex < 0 || imageIndex >= images.Length || images[imageIndex] == null)
            return;

        properties ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(properties);
        properties.SetTexture("_BaseMap", images[imageIndex]);
        properties.SetTexture("_MainTex", images[imageIndex]);
        screenRenderer.SetPropertyBlock(properties);
    }
}
