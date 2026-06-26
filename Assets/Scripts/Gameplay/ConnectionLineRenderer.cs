using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ConnectionLineRenderer : MonoBehaviour
{
    public static ConnectionLineRenderer Instance { get; private set; }

    private LineRenderer lineRenderer;
    private int pointCount = 0;

    void Awake()
    {
        Instance = this;
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;

        if (lineRenderer.material == null)
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        lineRenderer.startColor = new Color(0.6f, 0.8f, 1f, 0.9f);
        lineRenderer.endColor = new Color(0.6f, 0.8f, 1f, 0.9f);
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    public void Draw(StarNode from, StarNode to)
    {
        pointCount += (pointCount == 0 ? 2 : 1);
        lineRenderer.positionCount = pointCount;

        if (pointCount == 2)
        {
            lineRenderer.SetPosition(0, from.WorldPosition);
            lineRenderer.SetPosition(1, to.WorldPosition);
        }
        else
        {
            lineRenderer.SetPosition(pointCount - 1, to.WorldPosition);
        }
    }

    public void Clear()
    {
        lineRenderer.positionCount = 0;
        pointCount = 0;
    }
}
