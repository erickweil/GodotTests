using Godot;
// Default interface for line drawing

public abstract class LineDrawer
{
    public abstract void Clear();
    public abstract void AddLine(Vector3 from, Vector3 to, Color color);
    public abstract void Render();

    public void DrawCircle(Vector3 origin, Vector3 forward, Vector3 right, Color color, int resolution) {
        float angle = 0;
        float angleStep = Mathf.Pi * 2 / resolution;

        Vector3 lastPoint = origin + right;
        for(int i=0;i<resolution;i++) {
            angle += angleStep;

            Vector3 nextPoint = origin + right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);

            AddLine(lastPoint, nextPoint, color);

            lastPoint = nextPoint;
        }
    }
}