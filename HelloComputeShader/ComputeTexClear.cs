using Godot;
using System;
using System.Runtime.InteropServices;

public class ComputeTexClear : IDisposable {
	// Called when the node enters the scene tree for the first time.
	ComputeShaderHandler computeHandler;
    UniformSetStore uniformSetStore;
    Rid shader;

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
		shader = ComputeShaderHandler.loadShader(rd, "res://HelloComputeShader/compute_clear.glsl");
        computeHandler.setShader(shader,8,8,1);
	}

	public void assignTexture(Rid tex) {
        uniformSetStore = new UniformSetStore(computeHandler.RD, shader);
		uniformSetStore.putBufferUniform(tex, 0, 0, uniformType: RenderingDevice.UniformType.Image);
        uniformSetStore.createAllUniformSets();

		// Defining a compute pipeline
		computeHandler.createPipeline();
	}
	
	public void runClear(Color color, int width, int height) {
		var data = new ParamsBuffer((uint)width,(uint)height,color);
        uniformSetStore.pushConstant = ComputeShaderHandler.GetBytesFromStruct(data);

		computeHandler.dispatchPipeline(uniformSetStore, width+8, height+8, 1);
	}

	public void Dispose() {
		computeHandler.Dispose();
	}
}
