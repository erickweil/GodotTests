using Godot;
using System;

/* 
 https://docs.godotengine.org/en/stable/tutorials/shaders/compute_shaders.html
 Ver também https://github.com/godotengine/godot-demo-projects/blob/master/misc/compute_shader_heightmap/main.gd

	Discussão multithread draw list https://github.com/godotengine/godot-proposals/discussions/7163

 Vídeos sobre Compute shaders:
 - Learn GODOT 4 Compute Shaders with RAYTRACING!!  https://www.youtube.com/watch?v=ueUMr92GQJc

 Projetos Exemplo
 - https://github.com/godotengine/godot-docs/issues/4834 "Minimal Compute Shader Example"
 - Godot 4.2 Beta compute shader + texturas https://github.com/godotengine/godot-demo-projects/pull/938
 - Heightmap https://github.com/godotengine/godot-demo-projects/tree/65b34f81920752a382d14d544aa451de46b32a07/misc/compute_shader_heightmap
 - Raytracing https://github.com/nekotogd/Raytracing_Godot4/tree/master
 - Life Compute in Godot https://github.com/snailrhymer/Compute-Life
*/
public partial class Main : Node3D {
	// Called when the node enters the scene tree for the first time.
	ComputeShaderHandler computeHandler;
	Rid shader;
	UniformSetStore uniformSet;
	int width, height;
	float[] input;
	Rid input_buffer;
	public override void _Ready() {
		initializeCompute();
	}

	// https://github.com/godotengine/godot/pull/79696
	public void initializeCompute() {
		computeHandler = new ComputeShaderHandler(false);
		shader = ComputeShaderHandler.loadShader(computeHandler.RD, "res://HelloComputeShader/compute_example.glsl");
		computeHandler.setShader(shader,64,1,1);

		// Prepare our data. We use floats in the shader, so we need 32 bit.
		width = 128;
		height = 128;
		input = new float[width * height];
		input_buffer = ComputeShaderHandler.createArrayBuffer(computeHandler.RD,input,sizeof(float));
		uniformSet = new UniformSetStore(computeHandler.RD, shader);
		uniformSet.putBufferUniform(input_buffer, 0, 0, RenderingDevice.UniformType.StorageBuffer);
		uniformSet.createAllUniformSets();

		// Defining a compute pipeline
		computeHandler.createPipeline();

		testar();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	//public override void _Process(double delta)
	public override void _Process(double delta)	{
		if(Input.IsActionJustPressed("ui_up")) {
			testar();
		}

		if(Input.IsActionJustPressed("ui_down")) {
			testarLer();
		}
	}

	public void testar() {
		ulong startTime = startMeasure();
		computeShader();
		GD.Print("Tempo: "+ getMeasureMs(startTime) +"ms");
	}

	public void testarLer() {
		ulong startTime = startMeasure();
		
		// Read back the data from the buffers
		var output =  ComputeShaderHandler.readFloatBuffer(computeHandler.RD, input_buffer);

		//GD.Print("Input: ", string.Join(", ", input));
		//GD.Print("Output: ", string.Join(", ", output));
		for(int i=0;i<128;i++) {
			GD.Print("i:",i," "+output[i]);
		}
		for(int i=input.Length - 128;i<input.Length;i++) {
			GD.Print("i:",i," "+output[i]);
		}

		GD.Print("Tempo: "+ getMeasureMs(startTime) +"ms");
	}

	
	public void computeShader() {
		computeHandler.dispatchPipeline(uniformSet, width*height,1,1);
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		computeHandler.Dispose();
	}

	public static ulong startMeasure() {
		return Time.GetTicksUsec();
	}

	public static double getMeasureMs(ulong startTime) {
		return (Time.GetTicksUsec() - startTime) / 1000.0;
	}
}
