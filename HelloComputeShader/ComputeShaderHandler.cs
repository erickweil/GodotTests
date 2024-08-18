using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// https://github.com/erickweil/GodotTests/blob/v4.2-beta1/HelloComputeShader/ComputeShaderHandler.cs
public class ComputeShaderHandler : IDisposable {
	public RenderingDevice RD;
	private bool isLocalRenderingDevice;
	Rid shader;
	uint local_size_x, local_size_y, local_size_z;
	Rid pipeline;
	

	public ComputeShaderHandler(bool local, RenderingDevice rd = null) {
		isLocalRenderingDevice = local;
		if(rd == null) {
			if(isLocalRenderingDevice) {
				this.RD = RenderingServer.CreateLocalRenderingDevice();
			} else {
                // DEPOIS: Quando atualizar pra Godot 4.3, vai mudar tudo isso de multiplas threads e etc...
				// Ver depois questões de multithreading que pode dar problema
				// """ 
				// 		For clarity, this is only needed for compute code that needs to synchronize with the main RenderingDevice. 
				//		Compute code running on a local rendering device can be managed from any thread
				// """
				//  Add ability to call code on rendering thread #79696 https://github.com/godotengine/godot/pull/79696
				this.RD = RenderingServer.GetRenderingDevice();
			}
		} else {
			this.RD = rd;
		}
	}
	
	public void setShader(Rid shader, uint local_size_x, uint local_size_y, uint local_size_z) {
		this.local_size_x = local_size_x;
		this.local_size_y = local_size_y;
		this.local_size_z = local_size_z;

		this.shader = shader;
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
		// Create a compute pipeline
		pipeline = RD.ComputePipelineCreate(shader);
	}

	
	/*//TESTE REALIZAR VÁRIOS DISPATCH NO MESMO COMPUTE LIST
	//Não fez diferença na performance?
	public long computeList;
	public void preDispatchPipeline() {
		computeList = RD.ComputeListBegin();
		RD.ComputeListBindComputePipeline(computeList, pipeline);
	}

	public void postDispatchPipeline() {
		RD.ComputeListEnd();
	}*/

	public void dispatchPipeline(UniformSetStore uniforms,int xInvocations, int yInvocations, int zInvocations) {
		if(RD.ComputePipelineIsValid(pipeline) == false) {
			throw new InvalidDataException("Pipeline inválido");
		}
		// Deveria dar erro caso a conta não bater?
		uint xGroups = (uint)Mathf.Ceil((float)xInvocations / local_size_x);
		uint yGroups = (uint)Mathf.Ceil((float)yInvocations / local_size_y);
		uint zGroups = (uint)Mathf.Ceil((float)zInvocations / local_size_z);

		//GD.Print("X:",xGroups," Y:",yGroups," Z:",zGroups);
		List<UniformSetStore.UniformSet> uniformSets = uniforms.uniformSets;
		byte[] pushConstant = uniforms.pushConstant;

		var computeList = RD.ComputeListBegin();
		// Se quiser pode repetir o dispatch
			RD.ComputeListBindComputePipeline(computeList, pipeline);
			for(int i = 0;i < uniformSets.Count; i++)
			{
				if(RD.UniformSetIsValid(uniformSets[i].rid) == false)
				throw new InvalidDataException("UniformSet inválido");

				RD.ComputeListBindUniformSet(computeList, uniformSets[i].rid, (uint)i);
			}
			
			if(pushConstant != null)
			RD.ComputeListSetPushConstant(computeList,pushConstant,(uint)pushConstant.Length);

			RD.ComputeListDispatch(computeList, xGroups: xGroups, yGroups: yGroups, zGroups: zGroups);
		RD.ComputeListEnd();
	}

	public void submitAndSyncPipeline(UniformSetStore uniforms,int xInvocations, int yInvocations, int zInvocations) {
		// Submit to GPU and wait for sync
		dispatchPipeline(uniforms, xInvocations, yInvocations, zInvocations);

		if(isLocalRenderingDevice) {
			/*
			Ideally, you would not call sync() to synchronize the RenderingDevice right away as it will cause the CPU to 
			wait for the GPU to finish working. In our example, we synchronize right away because we want our data available 
			for reading right away. In general, you will want to wait at least 2 or 3 frames before synchronizing so that 
			the GPU is able to run in parallel with the CPU.
			*/
			RD.Submit();
			RD.Sync();
		}
	}

	public void Dispose()
	{
		GD.Print("Dispose ComputeShaderHandler");
		RD.FreeRid(pipeline);
		RD = null;
	}

	// =====================================================
	// Helpers
	// =====================================================
	// https://github.com/godotengine/godot-demo-projects/tree/65b34f81920752a382d14d544aa451de46b32a07/misc/compute_shader_heightmap

	/*
	We can load the newly created shader file compute_example.glsl and create a precompiled version of it using this:
	*/
	public static Rid loadShader(RenderingDevice RD, string filePath) {
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

		return RD.ShaderCreateFromSpirV(shaderSpirV);
	}

	public const RenderingDevice.TextureUsageBits UsageBitsForTexture2DRD = 
		  RenderingDevice.TextureUsageBits.SamplingBit
		| RenderingDevice.TextureUsageBits.StorageBit // for be used as uniform
		| RenderingDevice.TextureUsageBits.CanUpdateBit
		| RenderingDevice.TextureUsageBits.CanCopyToBit;

	public const RenderingDevice.TextureUsageBits UsageBitsForCompute =
	      RenderingDevice.TextureUsageBits.StorageBit  // for be used as uniform
		| RenderingDevice.TextureUsageBits.CanUpdateBit 
		| RenderingDevice.TextureUsageBits.CanCopyFromBit 
		| RenderingDevice.TextureUsageBits.CanCopyToBit;

	public static Rid createNewRDTexture(RenderingDevice rd,
	int widht, int height, 
	bool clear = true,
	RenderingDevice.DataFormat format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
	RenderingDevice.TextureUsageBits usageBits = UsageBitsForCompute) {
		var textureFormat = new RDTextureFormat();
		textureFormat.Format = format;
		textureFormat.Width = (uint)widht;
		textureFormat.Height = (uint)height;
		textureFormat.Depth = 1;
		textureFormat.UsageBits = usageBits;
		textureFormat.Mipmaps = 1;
		textureFormat.Samples = RenderingDevice.TextureSamples.Samples1;

		var textureRid = rd.TextureCreate(textureFormat,new RDTextureView());
		//var textureRid = rd.TextureBufferCreate((uint)(widht*height*16),format);

		if(clear) {
			// CLEAR THE TEXTURE BEFORE USAGE
        	rd.TextureClear(textureRid, new Color(0,0,0,0), 0, 1, 0, 1);
		}

		return textureRid;
	}

	public static Rid createArrayBuffer<T>(RenderingDevice rd,T[] input, int elementSize) {
		var inputBytes = new byte[input.Length * elementSize];
		Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);

		// Create a storage buffer that can hold our float values.
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		var buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);

		return buffer;
	}

	public static Rid createStructArrayBuffer<T>(RenderingDevice rd,T[] input, int elementSize) {
		var inputBytes = GetBytesFromStructArray(input,elementSize, input.Length);

		// Create a storage buffer that can hold our float values.
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		var buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);

		return buffer;
	}

	public static Rid createBufferFromBytes(RenderingDevice rd,byte[] inputBytes) {
		// Create a storage buffer that can hold our float values.
		// Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
		var buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);

		return buffer;
	}

	public static void updateBufferFromBytes(RenderingDevice rd, Rid buffer, byte[] inputBytes) {
		rd.BufferUpdate(buffer,0,(uint)inputBytes.Length,inputBytes);
	}

	public static void updateBufferFromArray<T>(RenderingDevice rd, Rid buffer, T[] input, int elementSize, int lengthToCopy) {
		var inputBytes = new byte[lengthToCopy * elementSize];
		Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);
		
		rd.BufferUpdate(buffer,0,(uint)inputBytes.Length,inputBytes);
	}

	public static void updateBufferFromStructArray<T>(RenderingDevice rd, Rid buffer, T[] input, int elementSize, int lengthToCopy) {
		var inputBytes = GetBytesFromStructArray(input,elementSize,lengthToCopy);
		
		rd.BufferUpdate(buffer,0,(uint)inputBytes.Length,inputBytes);
	}

	// https://stackoverflow.com/questions/3278827/how-to-convert-a-structure-to-a-byte-array-in-c
	public static byte[] GetBytesFromStruct<T>(T str)
	{
		int size = Marshal.SizeOf(str);
		byte[] arr = new byte[size];
		GCHandle h = default(GCHandle);
		try
		{
			h = GCHandle.Alloc(arr, GCHandleType.Pinned);

			Marshal.StructureToPtr<T>(str, h.AddrOfPinnedObject(), false);
		}
		finally
		{
			if (h.IsAllocated)
			{
				h.Free();
			}
		}

		return arr;
	}

	public static byte[] GetBytesFromStructArray<T>(T[] input, int elementSize, int lengthToCopy) {
		var inputBytes = new byte[lengthToCopy * elementSize];
		
		// Não funciona BlockCopy https://stackoverflow.com/questions/33181945/blockcopy-a-class-getting-object-must-be-an-array-of-primitives
		for(int i = 0;i< lengthToCopy;i++) {
			byte[] elemBytes = GetBytesFromStruct(input[i]);

			Buffer.BlockCopy(elemBytes, 0, inputBytes, i * elementSize, elemBytes.Length);
		}

		return inputBytes;
	}

	public static float[] readFloatBuffer(RenderingDevice RD, Rid buffer) {
		var outputBytes = RD.BufferGetData(buffer);
		var output = new float[outputBytes.Length / sizeof(float)];
		Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

		return output;
	}

	public static void readArrayBuffer<T>(RenderingDevice RD,Rid buffer,T[] output, int elementSize, int lengthToRead) {
		var outputBytes = RD.BufferGetData(buffer,0,(uint)(elementSize * lengthToRead));
		//var output = new float[outputBytes.Length / sizeof(float)];
		Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);
	}

	public static void readStructArrayBuffer<T>(RenderingDevice RD,Rid buffer,T[] output) where T : struct {
		byte[] outputBytes = RD.BufferGetData(buffer);
		ReadOnlySpan<T> span = MemoryMarshal.Cast<byte,T>(outputBytes);
		for(int i = 0;i< output.Length;i++) {
			output[i] = span[i];
		}
	}

	/*public void readStructArrayBuffer<T>(Rid buffer,T[] output) {
		var outputBytes = RD.BufferGetData(buffer);
		// https://gist.github.com/13xforever/2835844
		var pData = GCHandle.Alloc(outputBytes, GCHandleType.Pinned);
		for(int i = 0;i< output.Length;i++) {
			var result = (T)Marshal.PtrToStructure(pData.AddrOfPinnedObject(), typeof(T));
		}
		pData.Free();
		return result;
	}*/
}