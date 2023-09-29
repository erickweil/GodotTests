using System;
using Godot;

// https://github.com/godotengine/godot/pull/68235
// https://ask.godotengine.org/134841/rendering-triangle-using-renderingdevice-api
public partial class Demo : SubViewport {
    Rid framebuffer;
    Rid pipeline;
    Rid shader;
    Rid vtx_buf;
    Rid vtx_array;
    Color[] clear_colors;
    
    RenderingDevice RD;

    public void createFrameBuffer() {
        var vptex = RenderingServer.TextureGetRdTexture(GetTexture().GetRid());
        framebuffer = RD.FramebufferCreate(new Godot.Collections.Array<Rid>{ vptex });
    }

    public override void _Ready() {
        clear_colors = new Color[] {Color.Color8(0,0,0,0)};
        RD = RenderingServer.GetRenderingDevice();

        // Carregar Shader
        var shaderFile = GD.Load<RDShaderFile>("res://RenderingPipeline/shader.glsl");
        var shaderSpirV = shaderFile.GetSpirV();
		shader = RD.ShaderCreateFromSpirV(shaderSpirV);

        // Definir pontos do Triângulo
        var vattr = new RDVertexAttribute();
        vattr.Format = RenderingDevice.DataFormat.R32G32B32Sfloat;
        vattr.Location = 0;
        vattr.Stride = sizeof(float) * 3; // 12 bytes por vértice

        var points = new float[] {
             0.0f, -0.5f, 0.0f,
             0.5f,  0.5f, 0.0f,
            -0.5f,  0.5f, 0.0f
        };
        uint nVertices = 3;
        var points_raw = new byte[points.Length * sizeof(float)];
		Buffer.BlockCopy(points, 0, points_raw, 0, points_raw.Length);
        vtx_buf = RD.VertexBufferCreate((uint)points_raw.Length,points_raw);
        var vtx_format = RD.VertexFormatCreate(new Godot.Collections.Array<RDVertexAttribute>{ vattr });
        vtx_array = RD.VertexArrayCreate(nVertices, vtx_format, new Godot.Collections.Array<Rid> { vtx_buf});

        createFrameBuffer();

        var blend = new RDPipelineColorBlendState();
        blend.Attachments.Add(new RDPipelineColorBlendStateAttachment());
        pipeline = RD.RenderPipelineCreate(
            shader,
            RD.ScreenGetFramebufferFormat(),
            vtx_format,
            RenderingDevice.RenderPrimitive.Triangles,
            new RDPipelineRasterizationState(),
            new RDPipelineMultisampleState(),
            new RDPipelineDepthStencilState(),
            blend
        );

        GD.Print("OK");
	}

    public override void _ExitTree()
    {
        base._ExitTree();
        RD.FreeRid(vtx_array);
        RD.FreeRid(vtx_buf);
        RD.FreeRid(pipeline);
        RD.FreeRid(framebuffer);
        RD.FreeRid(shader);
    }

    public override void _Process(double delta)
    {
        // handle resizing
        if(!RD.FramebufferIsValid(framebuffer)) {
            createFrameBuffer();
        }

        var draw_list = RD.DrawListBegin(framebuffer,
        RenderingDevice.InitialAction.Clear, RenderingDevice.FinalAction.Read,
        RenderingDevice.InitialAction.Clear, RenderingDevice.FinalAction.Read,
        clear_colors);
        RD.DrawListBindRenderPipeline(draw_list, pipeline);
        RD.DrawListBindVertexArray(draw_list, vtx_array);
        RD.DrawListDraw(draw_list, false, 1);
        RD.DrawListEnd();
    }
}