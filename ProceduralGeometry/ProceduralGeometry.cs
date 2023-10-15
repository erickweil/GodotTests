using System;
using System.Runtime.Intrinsics.Arm;
using Godot;

// Example showing the use of a Texture2DRD as a means of
// comunicating between the compute shader and the mesh being drawn.

// The texture is in format R32G32B32A32Sfloat which means that it contains arbitrary floating points
// and the compute shader updates vertice position and normal info into the texture EACH FRAME without problems.

// Hold G to see it in action
public partial class ProceduralGeometry : Node3D {
	
	[Export]
	MeshInstance3D meshInstance;

	struct UniformBuffer {
		public uint texWidth;
		public uint texHeight;
		public float cubeSize;
		public uint maxTris;

		public float offx;
		public float offy;
		public float offz;
		float pad0;
		public UniformBuffer(uint texWidth, uint texHeight,float cubeSize, uint maxTris)
		{
			this.texWidth = texWidth;
			this.texHeight = texHeight;
			this.cubeSize = cubeSize;
			this.maxTris = maxTris;
			
			offx = 0; offy = 0; offz = 0; pad0 = 0;

			// tem que ser múltiplo de 16 bytes
		}
	}

	public override void _Ready() {
		RenderingServer.CallOnRenderThread(Callable.From(computeInit));


		var material = meshInstance.MaterialOverride as ShaderMaterial;
		//shader_texture = (Texture2Drd)material.GetShaderParameter("image");
		shader_texture = new Texture2Drd();
		material.SetShaderParameter("image", shader_texture);

		int vertices = (tex_size * tex_size) / pixels_per_vertex;
		meshInstance.Mesh = getTrianglesMesh(vertices / 3);
	}

	public override void _Process(double delta)
	{		
		if(RD == null || computeHandler == null) return;

		shader_texture.TextureRdRid = current_tex;

		if(Input.IsPhysicalKeyPressed(Key.G)) {
			Vector2 mouse = Input.GetLastMouseVelocity();

			mouse_off += (float)delta * new Vector3(mouse.Y / 1000.0f,0,  -mouse.X / 1000.0f);

			RenderingServer.CallOnRenderThread(Callable.From(computeProcess));
		}
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		if(shader_texture != null) {
			shader_texture.TextureRdRid = new Rid();
		}

		RenderingServer.CallOnRenderThread(Callable.From(computeDispose));
	}

	// Generate a mesh that every 3 vertices define a disconected tri
	public ArrayMesh getTrianglesMesh(int nTris) {

		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#doc-arraymesh
		var surfaceArray = new Godot.Collections.Array();
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		// Even though the vertex info is on the texture, still need to provide vertex info
		// From AddSurfaceFromArrays docs: "That first vertex sub-array is always required; the others are optional."
		Vector3[] pos = new Vector3[nTris*3];
		for(int i=0;i<nTris;i++) {
			int i0 = i*3 + 0;
			int i1 = i*3 + 1;
			int i2 = i*3 + 2;
			
			// those values don't matter, the values provided by the compute shader in the
			// texture will be used instead. what could be stored here?
			//Vector3 off = new Vector3(i % tex_size, 0 , i / tex_size);
			//pos[i0] = new Vector3(0,0,0) + off;
			//pos[i1] = new Vector3(0,0,1) + off;
			//pos[i2] = new Vector3(1,0,1) + off;
			
			// USING COLOR FOR DEBUGGING COMPUTE SHADER EXECUTION ORDER
			float inter = (float)i / nTris;
			pos[i0] = new Vector3(1.0f-inter,Mathf.Abs(0.5f-inter),inter);
			pos[i1] = new Vector3(1.0f-inter,Mathf.Abs(0.5f-inter),inter);
			pos[i2] = new Vector3(1.0f-inter,Mathf.Abs(0.5f-inter),inter);
		}
		
		// Convert Lists to arrays and assign to surface array
		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#setting-up-the-arraymesh
		// https://docs.godotengine.org/en/stable/classes/class_mesh.html#enum-mesh-arraytype
		/*
			RGB32F			VERTEX			POS.xyz
			A2B10G10R10		ARRAY_NORMAL 	-
			A2B10G10R10		ARRAY_TANGENT 	-
			RGBA8			ARRAY_COLOR 	-
			RG32F			UV				-
			RG32F			UV2				-
			RGBA16UI 		ARRAY_BONES		-
			RGBA16UNORM 	ARRAY_WEIGHTS	-
			RGBA32F			CUSTOM0			-
			RGBA32F			CUSTOM1			-
			RGBA32F			CUSTOM2			-
			RGBA32F			CUSTOM3			-
		*/
		surfaceArray[(int)Mesh.ArrayType.Vertex] = pos;

		// Mesh without indices (each 3 vertices define a triangle)
		// The idea is that since all this data is on the texture, the mesh doesn't need to store it
		// And would be a waste of memory to store it twice
		var arrMesh = new ArrayMesh();
		arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray,null,null);
	
		return arrMesh;
	}

	// =============================
	// Need to run on Render Thread
	// =============================
	Vector3 mouse_off;

	// Change this accordingly.
	// tex_size should be choosed so that (tex_size*tex_size)/2 equals the maximum amount of vertices
	// Keep in mind this 'maximum amount' is always being drawed, just not visible because most of them
	// may produce degenerate triangles reading 0's from the texture.
	int tex_size = 2048;
	// cube_size defines the resolution of the cube that will be calculated on compute_shader
	// higher values means better resolution, but possibly produce also more quads, so update tex_size
	// also should be multiple of 8
	float cube_size = 256.0f;
	Rid current_tex;

	Rid counter_buffer;
	int pixels_per_vertex = 2; // 1 for position, 1 for normal

	Texture2Drd shader_texture;
	UniformBuffer uniformBuffer_data;
	RenderingDevice RD;
	ComputeShaderHandler computeHandler;

	public void createUniforms(int width, int height, float cubeSize) {
		int vertices = (tex_size * tex_size) / pixels_per_vertex;
		uniformBuffer_data = new UniformBuffer((uint)width, (uint)height, cubeSize, (uint)(vertices / 3));
		computeHandler.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		current_tex = ComputeShaderHandler.createNewRDTexture(RD,width,height, format: RenderingDevice.DataFormat.R32G32B32A32Sfloat, usageBits: ComputeShaderHandler.UsageBitsForTexture2DRD);
		computeHandler.putBufferUniform(current_tex, 0, 0, uniformType: RenderingDevice.UniformType.Image);

		uint[] input = new uint[1] {0};
		counter_buffer = ComputeShaderHandler.createArrayBuffer(computeHandler.RD,input,sizeof(uint));
		computeHandler.putBufferUniform(counter_buffer, 0, 1, RenderingDevice.UniformType.StorageBuffer);
	}

	public void computeInit() {
		RD = RenderingServer.GetRenderingDevice();
		computeHandler = new ComputeShaderHandler(false,RD);
		computeHandler.loadShader("res://ProceduralGeometry/procedural_geometry.glsl",8,8,8);

		createUniforms(tex_size,tex_size,cube_size);
		
		// Defining a compute pipeline
		computeHandler.createPipeline();


		// Run the first time
		computeProcess();
	}

	public void computeProcess() {
		ulong startTime = Time.GetTicksUsec();

		if(!RD.UniformSetIsValid(computeHandler.uniformSets[0].rid)) {
			GD.PrintErr("Uniform set invalid");
			return;
		}

		uniformBuffer_data.offx = mouse_off.X;
		uniformBuffer_data.offy = mouse_off.Y;
		uniformBuffer_data.offz = mouse_off.Z;
		computeHandler.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		// Clear the texture
		RD.TextureClear(current_tex, new Color(0,0,0,0), 0, 1, 0, 1);

		// Resetar counter
		uint[] input = new uint[1] {0};
		ComputeShaderHandler.updateBufferFromArray(computeHandler.RD, counter_buffer,input,sizeof(uint));

		uint invocations = (uint)(int)cube_size;
		computeHandler.dipatchPipeline(invocations,invocations,invocations);

		// If you want the output of a compute shader to be used as input of
		// another computer shader you'll need to add a barrier:
		// NOT DOING ANY DIFFERENCE BUT LEAVING HERE IF SOMETHING WEIRD HAPPENS
		//RD.Barrier(RenderingDevice.BarrierMask.Compute);

		//GD.Print( (Time.GetTicksUsec() - startTime) / 1000.0, "ms");
	}

	public void computeDispose() {
		computeHandler.Dispose();
		RD.FreeRid(current_tex);
		RD.FreeRid(counter_buffer);
	}
}
