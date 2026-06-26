using UnityEngine;

public class StarNode : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator nodeAnimator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip connectSound;

    [SerializeField] private Color idleColor = new Color(0.4f, 0.4f, 0.6f);
    [SerializeField] private Color activeColor = Color.yellow;
    [SerializeField] private Color connectedColor = Color.white;

    public Vector2 WorldPosition => (Vector2)transform.position;

    private NodeState currentState = NodeState.Idle;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetState(NodeState state)
    {
        currentState = state;
        ApplyVisuals(state);

        if (state == NodeState.Connected)
        {
            if (nodeAnimator != null)
                nodeAnimator.SetTrigger("Connect");

            if (audioSource != null && connectSound != null)
                audioSource.PlayOneShot(connectSound);
        }
    }

    public NodeState GetState() => currentState;

    private void ApplyVisuals(NodeState state)
    {
        if (spriteRenderer == null) return;

        spriteRenderer.color = state switch
        {
            NodeState.Active => activeColor,
            NodeState.Connected => connectedColor,
            _ => idleColor
        };

        float scale = state == NodeState.Active ? 1.3f : 1f;
        transform.localScale = Vector3.one * scale;
    }
}
