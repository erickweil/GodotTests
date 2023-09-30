using System;
using System.Runtime.Intrinsics.Arm;
using Godot;

// A ideia é ser capaz de Renderizar algo no Compute shader e então ver
// o resultado sem uma leitura no CPU
public partial class ComputeTexture : SubViewport {
    Rid computeTex;
    Rid vptex;
    UniformBuffer uniformBuffer_data;
    Rid uniformBuffer;
    ComputeShaderHandler computeHandler;
    struct UniformBuffer {
        public uint offx;
	    public uint offy;
        public uint width;
	    public uint height;
        // tem que alinhar de 4 em 4 bytes
        public float color_r;
        public float color_b;
        public float color_g;
	    public float color_a;
        public UniformBuffer(int widht, int height, Vector4 color)
        {
            this.offx = 0;
            this.offy = 0;
            this.width = (uint) widht;
            this.height = (uint) height;
            //this.pad_a = 0.0f;
            //this.pad_b = 0.0f;
            this.color_r = color.X;
            this.color_g = color.Y;
            this.color_b = color.Z;
            this.color_a = color.W;
        }
    }

    public override void _Ready() {
        computeHandler = new ComputeShaderHandler(false);
		computeHandler.loadShader("res://RenderingPipeline/compute_texture.glsl",8,8,1);

        uniformBuffer_data = new UniformBuffer(
                128,
                128,
                new Vector4(0,0,0,1)
        );

        uniformBuffer = computeHandler.createBufferFromBytes(ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data));
        computeHandler.addBufferUniform(uniformBuffer,0);

        vptex = RenderingServer.TextureGetRdTexture(GetTexture().GetRid());
        computeHandler.addBufferUniform(vptex,1,uniformType: RenderingDevice.UniformType.Image);
		// Defining a compute pipeline
        computeHandler.createUniformSet();
		computeHandler.createPipeline();
	}

    public override void _ExitTree()
    {
        base._ExitTree();
        computeHandler.Dispose();
    }

    public override void _Process(double delta)
    {
        if(Input.IsActionPressed("ui_up")) return;

        ulong startTime = Time.GetTicksUsec();
        if(!computeHandler.rd.UniformSetIsValid(computeHandler.uniformSet)) {
            GD.Print("Re-doing uniform set");
            vptex = RenderingServer.TextureGetRdTexture(GetTexture().GetRid());
            computeHandler.resetUniforms();

            computeHandler.addBufferUniform(uniformBuffer,0);
            computeHandler.addBufferUniform(vptex,1,uniformType: RenderingDevice.UniformType.Image);

            computeHandler.createUniformSet();
        }

        Vector2 pos = GetMousePosition();

        //GD.Print(pos);

        ViewportTexture tex = GetTexture();
        uint width = (uint)tex.GetWidth();
        uint height = (uint)tex.GetHeight();

        uniformBuffer_data.offx = (uint)Math.Clamp((int)pos.X-50,0,(int)width);
        uniformBuffer_data.offy = (uint)Math.Clamp((int)pos.Y-50,0,(int)width);
        uniformBuffer_data.width = (uint)Math.Clamp((int)pos.X+50,0,(int)width);
        uniformBuffer_data.height = (uint)Math.Clamp((int)pos.Y+50,0,(int)height);
        //uniformBuffer_data.width = width;
        //uniformBuffer_data.height = height;


        uniformBuffer_data.color_g = pos.X / width;
        uniformBuffer_data.color_b = pos.Y / height;
        computeHandler.updateBufferFromBytes(uniformBuffer,ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data));

        computeHandler.dipatchPipeline(width+8,height+8,1);

        //GD.Print( (Time.GetTicksUsec() - startTime) / 1000.0, "ms");
    }
}