using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public class ComputeShaderHandler : IDisposable {
	private RenderingDevice rd;
	private bool isLocalRenderingDevice;
	List<Rid> createdRids;
	Rid shader;
	uint local_size_x, local_size_y, local_size_z;

	Godot.Collections.Array<RDUniform> uniformList;

	Rid uniformSet;
	Rid pipeline;

	public ComputeShaderHandler(bool local) {
		isLocalRenderingDevice = local;
		if(isLocalRenderingDevice) {
			rd = RenderingServer.CreateLocalRenderingDevice();
		} else {
			// Ver depois questões de multithreading que pode dar problema
			// """ 
			// 		For clarity, this is only needed for compute code that needs to synchronize with the main RenderingDevice. 
			//		Compute code running on a local rendering device can be managed from any thread
			// """
			//  Add ability to call code on rendering thread #79696 https://github.com/godotengine/godot/pull/79696
			rd = RenderingServer.GetRenderingDevice();
		}

		uniformList = new Godot.Collections.Array<RDUniform>();
		createdRids = new List<Rid>();
	}

	/*
	We can load the newly created shader file compute_example.glsl and create a precompiled version of it using this:
	*/
	public void loadShader(string filePath, uint local_size_x, uint local_size_y, uint local_size_z) {
		this.local_size_x = local_size_x;
		this.local_size_y = local_size_y;
		this.local_size_z = local_size_z;
		// https://docs.godotengine.org/en/stable/classes/class_rdshaderfile.html
		var shaderFile = GD.Load<RDShaderFile>(filePath);

		if(shaderFile.BaseError != "") {
			throw new InvalidDataException("Não foi possível carregar o shader:"+shaderFile.BaseError);
		}

		// https://docs.godotengine.org/en/stable/classes/class_rdshaderspirv.html#class-rdshaderspirv
		var shaderSpirV = shaderFile.GetSpirV();

		if(shaderSpirV.CompileErrorCompute != "") {
			throw new InvalidDataException("Não foi possível compilar o shader:"+shaderSpirV.CompileErrorCompute);
		}

		shader = rd.ShaderCreateFromSpirV(shaderSpirV);
		createdRids.Add(shader);
	}

	/*
	With the buffer in place we need to tell the rendering device to use this buffer. 
	To do that we will need to create a uniform (like in normal shaders) and assign it to a
		uniform set which we can pass to our shader later.
	*/
	/* https://docs.godotengine.org/en/stable/classes/class_renderingdevice.html#enum-renderingdevice-uniformtype
		enum UniformType:
		UniformType UNIFORM_TYPE_SAMPLER = 0
				Sampler uniform. TODO: Difference between sampler and texture uniform
		UniformType UNIFORM_TYPE_SAMPLER_WITH_TEXTURE = 1
				Sampler uniform with a texture.
		UniformType UNIFORM_TYPE_TEXTURE = 2
				Texture uniform.
		UniformType UNIFORM_TYPE_IMAGE = 3
				Image uniform. TODO: Difference between texture and image uniform
		UniformType UNIFORM_TYPE_TEXTURE_BUFFER = 4
				Texture buffer uniform. TODO: Difference between texture and texture buffe uniformr
		UniformType UNIFORM_TYPE_SAMPLER_WITH_TEXTURE_BUFFER = 5
				Sampler uniform with a texture buffer. TODO: Difference between texture and texture buffer uniform
		UniformType UNIFORM_TYPE_IMAGE_BUFFER = 6
				Image buffer uniform. TODO: Difference between texture and image uniforms
		UniformType UNIFORM_TYPE_UNIFORM_BUFFER = 7
				Uniform buffer uniform.
		UniformType UNIFORM_TYPE_STORAGE_BUFFER = 8
				Storage buffer uniform.
		UniformType UNIFORM_TYPE_INPUT_ATTACHMENT = 9
				Input attachment uniform.
		UniformType UNIFORM_TYPE_MAX = 10
				Represents the size of the UniformType enum.
	*/
	public void addBufferUniform(Rid buffer, int binding, 
	RenderingDevice.UniformType uniformType = RenderingDevice.UniformType.StorageBuffer) {
		// Create a uniform to assign the buffer to the rendering device
		var uniform = new RDUniform
		{
			UniformType = uniformType,
			Binding = binding
		};
		uniform.AddId(buffer);

		uniformList.Add(uniform);

		//var uniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { uniform }, shader, set);
		//return uniformSet;
	}

	/* Defining a compute pipeline
	The next step is to create a set of instructions our GPU can execute. We need a pipeline and a compute list for that.
	The steps we need to do to compute our result are:

	1 - Create a new pipeline.
	2 - Begin a list of instructions for our GPU to execute.
	3 - Bind our compute list to our pipeline
	4 - Bind our buffer uniform to our pipeline
	5 - Specify how many workgroups to use
	6 - End the list of instructions

	Note that we are dispatching the compute shader with 5 work groups in the X axis, and one in the others. 
	Since we have 2 local invocations in the X axis (specified in our shader), 10 compute shader invocations will be launched in total. 
	If you read or write to indices outside of the range of your buffer,
	you may access memory outside of your shaders control or parts of other variables which may cause issues on some hardware.
	*/
	public void createPipeline() {
		uniformSet = rd.UniformSetCreate(uniformList, shader, 0);
		createdRids.Add(uniformSet);

		// Create a compute pipeline
		pipeline = rd.ComputePipelineCreate(shader);
		createdRids.Add(pipeline);
	}

	public void dipatchPipeline(uint xInvocations, uint yInvocations, uint zInvocations) {
		if(rd.UniformSetIsValid(uniformSet) == false) {
			throw new InvalidDataException("UniformSet inválido");
		}
		if(rd.ComputePipelineIsValid(pipeline) == false) {
			throw new InvalidDataException("Pipeline inválido");
		}
		// Deveria dar erro caso a conta não bater?
		uint xGroups = xInvocations / local_size_x;
		uint yGroups = yInvocations / local_size_y;
		uint zGroups = zInvocations / local_size_z;

		var computeList = rd.ComputeListBegin();
		// Se quiser pode repetir o dispatch
			rd.ComputeListBindComputePipeline(computeList, pipeline);
			rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
			rd.ComputeListDispatch(computeList, xGroups: xGroups, yGroups: yGroups, zGroups: zGroups);
		rd.ComputeListEnd();
	}

	public void submitAndSyncPipeline(uint xInvocations, uint yInvocations, uint zInvocations) {
		// Submit to GPU and wait for sync
		dipatchPipeline(xInvocations, yInvocations, zInvocations);

		if(isLocalRenderingDevice) {
			/*
			Ideally, you would not call sync() to synchronize the RenderingDevice right away as it will cause the CPU to 
			wait for the GPU to finish working. In our example, we synchronize right away because we want our data available 
			for reading right away. In general, you will want to wait at least 2 or 3 frames before synchronizing so that 
			the GPU is able to run in parallel with the CPU.
			*/
			rd.Submit();
			rd.Sync();
		}
	}

    public void Dispose()
    {
		GD.Print("Dispose ComputeShaderHandler");
		for(int i = createdRids.Count-1; i >= 0; i--) {
			rd.FreeRid(createdRids[i]);
		}
		createdRids.Clear();
		createdRids = null;
        //rd.Dispose();
		//rd = null;
    }

	// =====================================================
	// Helpers
	// =====================================================
	public Rid createFloatBuffer(float[] input) {
		var inputBytes = new byte[input.Length * sizeof(float)];
		Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);

		// Create a storage buffer that can hold our float values.
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		var buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);
		createdRids.Add(buffer);

		return buffer;
	}

	public float[] readFloatBuffer(Rid buffer) {
		var outputBytes = rd.BufferGetData(buffer);
		var output = new float[outputBytes.Length / sizeof(float)];
		Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

		return output;
	}
}