using Godot;

// https://docs.godotengine.org/en/latest/classes/class_immediatemesh.html
// https://docs.godotengine.org/en/latest/tutorials/3d/procedural_geometry/immediatemesh.html
// https://github.com/godotengine/godot-docs/issues/5901
public class LineImmediateMesh : LineDrawer {
    private ImmediateMesh lineMesh;
    public bool needCallBegin;

    public LineImmediateMesh(MeshInstance3D meshInstance) {
        lineMesh = new ImmediateMesh();

        meshInstance.Mesh = lineMesh;

        // https://github.com/V-Sekai/godot-vrm/blob/master/addons/vrm/vrm_secondary.gd#L232
        var m = new StandardMaterial3D();
        //m.NoDepthTest = true;
        m.DisableReceiveShadows = true;
        m.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        m.VertexColorUseAsAlbedo = true;
        m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        meshInstance.MaterialOverride = m;

        needCallBegin = true;
    }

    public override void Clear() {
        lineMesh.ClearSurfaces();
    }

    public override void AddLine(Vector3 from, Vector3 to, Color color) {
        if(needCallBegin) {
            lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            needCallBegin = false;
        }

        lineMesh.SurfaceSetColor(color);
        lineMesh.SurfaceAddVertex(from);

        lineMesh.SurfaceSetColor(color);
        lineMesh.SurfaceAddVertex(to);
    }

    public override void Render() {
        lineMesh.SurfaceEnd();

        needCallBegin = true;
    }
}