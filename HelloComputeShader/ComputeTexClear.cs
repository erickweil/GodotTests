using Godot;
using System;
using System.Runtime.InteropServices;

public class ComputeTexClear : IDisposable {
	// Called when the node enters the scene tree for the first time.
	ComputeShaderHandler computeHandler;

	[StructLayout(LayoutKind.Sequential)]
	struct ParamsBuffer {
        public uint startx;
	    public uint starty;
        public uint endx;
		public uint endy;
		public Vector4 color;

		public ParamsBuffer(uint width,uint height, Color color) {
			this.startx = 0;
			this.starty = 0;
			this.endx = width;
			this.endy = height;
			this.color = new Vector4(color.R,color.G,color.B,color.A);
		}
    }

	
	public ComputeTexClear(bool isLocal, RenderingDevice rd) {
		computeHandler = new ComputeShaderHandler(isLocal, rd);
		computeHandler.loadShader("res://HelloComputeShader/compute_clear.glsl",8,8,1);
	}

	public void assignTexture(Rid tex) {
		computeHandler.putBufferUniform(tex, 0, 0, uniformType: RenderingDevice.UniformType.Image);


		// Defining a compute pipeline
		computeHandler.createPipeline();
	}
	
	public void runClear(Color color, uint width, uint height) {
		var data = new ParamsBuffer(width,height,color);
        computeHandler.pushConstant = ComputeShaderHandler.GetBytesFromStruct(data);

		computeHandler.dispatchPipeline((uint)width+8,(uint)height+8,1);
	}

	public void Dispose() {
		computeHandler.Dispose();
	}
}
