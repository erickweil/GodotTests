using System;
using System.Runtime.Intrinsics.Arm;
using Godot;

// A ideia é ser capaz de Renderizar algo no Compute shader e então ver
// o resultado sem uma leitura no CPU
public partial class ConwayGameOfLife : SubViewport {
    Rid computeTex;
    Rid vptex;
    UniformBuffer uniformBuffer_data;

    RenderingDevice RD;
    ComputeShaderHandler computeHandler;
    Rid shader;
    UniformSetStore uniformSet;
    struct UniformBuffer {
        public uint width;
	    public uint height;
        public uint mousex;
	    public uint mousey;
        public UniformBuffer(uint width, uint height,uint mousex, uint mousey)
        {
            this.width = width;
            this.height = height;

            this.mousex = mousex;
            this.mousey = mousey;

            // tem que ser múltiplo de 16 bytes
        }
    }

    public override void _Ready() {
        RD = RenderingServer.GetRenderingDevice();
        computeHandler = new ComputeShaderHandler(false,RD);
		shader = ComputeShaderHandler.loadShader(RD, "res://ConwayGameOfLife/compute_game_of_life.glsl");
        computeHandler.setShader(shader,8,8,1);

        ViewportTexture tex = GetTexture();

        uniformSet = new UniformSetStore(RD, shader);
        createUniforms(tex);

        
		// Defining a compute pipeline
		computeHandler.createPipeline();
	}

    public void createUniforms(ViewportTexture tex) {
        uniformBuffer_data = new UniformBuffer((uint)tex.GetWidth(), (uint)tex.GetHeight(), 0, 0);

        uniformSet.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

        computeTex = ComputeShaderHandler.createNewRDTexture(RD,tex.GetWidth(),tex.GetHeight());
        uniformSet.putBufferUniform(computeTex, 0, 0, uniformType: RenderingDevice.UniformType.Image);

        vptex = RenderingServer.TextureGetRdTexture(GetTexture().GetRid());
        uniformSet.putBufferUniform(vptex, 0, 1, uniformType: RenderingDevice.UniformType.Image);

        uniformSet.createAllUniformSets();
    }

    public override void _Process(double delta)
    {
        if(Input.IsActionPressed("ui_up")) return;

        ulong startTime = Time.GetTicksUsec();


        ViewportTexture tex = GetTexture();
        int width = tex.GetWidth();
        int height = tex.GetHeight();

        if(!RD.UniformSetIsValid(uniformSet.uniformSets[0].rid)) {
            GD.Print("Re-doing uniform set");

            uniformSet.resetUniformSets();

            RD.FreeRid(computeTex);

            createUniforms(tex);

            uniformSet.createUniformSet(0);
        }

        Vector2 pos = GetMousePosition();

        //GD.Print(pos);
        uniformBuffer_data.width = (uint)width;
        uniformBuffer_data.height = (uint)height;
        uniformBuffer_data.mousex = (uint)Math.Clamp((int)pos.X, 0, width);
        uniformBuffer_data.mousey = (uint)Math.Clamp((int)pos.Y, 0, height);
        uniformSet.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

        //for(int i = 0; i < 10; i++) {
            computeHandler.dispatchPipeline(uniformSet, width+8,height+8,1);

            // If you want the output of a compute shader to be used as input of
            // another computer shader you'll need to add a barrier:
            // NOT DOING ANY DIFFERENCE BUT LEAVING HERE IF SOMETHING WEIRD HAPPENS
            //RD.Barrier(RenderingDevice.BarrierMask.Compute);

            RD.TextureCopy(vptex,computeTex,Vector3.Zero,Vector3.Zero,new Vector3(width,height,0),0,0,0,0);
        //}

        //GD.Print( (Time.GetTicksUsec() - startTime) / 1000.0, "ms");
    }


    public override void _ExitTree()
    {
        base._ExitTree();
        computeHandler.Dispose();
        RD.FreeRid(computeTex);
    }
}