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
		public uint maxQuads;

		public float offx;
		public float offy;
		public float offz;
		float pad0;
		public UniformBuffer(uint texWidth, uint texHeight,float cubeSize, uint maxQuads)
		{
			this.texWidth = texWidth;
			this.texHeight = texHeight;
			this.cubeSize = cubeSize;
			this.maxQuads = maxQuads;

			// tem que ser múltiplo de 16 bytes
		}
	}

	public override void _Ready() {
		RenderingServer.CallOnRenderThread(Callable.From(computeInit));


		var material = meshInstance.MaterialOverride as ShaderMaterial;
		//shader_texture = (Texture2Drd)material.GetShaderParameter("image");
		shader_texture = new Texture2Drd();
		material.SetShaderParameter("image", shader_texture);

		meshInstance.Mesh = getQuadsMesh((tex_size * tex_size)/4);
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

	// Generate a mesh that every 4 vertices define a disconected quad
	public ArrayMesh getQuadsMesh(int nQuads) {

		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#doc-arraymesh
		var surfaceArray = new Godot.Collections.Array();
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		Vector3[] pos = new Vector3[nQuads*4];
		Vector2[] uv = new Vector2[nQuads*4];
		int[] indices = new int[nQuads*6];
		for(int i=0;i<nQuads;i++) {
			int i0 = i*4 + 0;
			int i1 = i*4 + 1;
			int i2 = i*4 + 2;
			int i3 = i*4 + 3;
			Vector3 off = new Vector3(i % tex_size, 0 , i / tex_size);
			
			pos[i0] = new Vector3(0,0,0) + off;
			pos[i1] = new Vector3(0,0,1) + off;
			pos[i2] = new Vector3(1,0,1) + off;
			pos[i3] = new Vector3(0,0,1) + off;

			uv[i0] = new Vector2(0,0);
			uv[i1] = new Vector2(0,1);
			uv[i2] = new Vector2(1,1);
			uv[i3] = new Vector2(0,1);

			indices[i*6+0] = i0;
			indices[i*6+1] = i1;
			indices[i*6+2] = i2;
			indices[i*6+3] = i2;
			indices[i*6+4] = i3;
			indices[i*6+5] = i0;
		}
		
		// Convert Lists to arrays and assign to surface array
		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#setting-up-the-arraymesh
		// https://docs.godotengine.org/en/stable/classes/class_mesh.html#enum-mesh-arraytype
		/*
			RGB32F			VERTEX			POS.xyz
			A2B10G10R10		ARRAY_NORMAL 	-
			A2B10G10R10		ARRAY_TANGENT 	-
			RGBA8			ARRAY_COLOR 	-
			RG32F			UV				UV.xy
			RG32F			UV2				-
			RGBA16UI 		ARRAY_BONES		-
			RGBA16UNORM 	ARRAY_WEIGHTS	-
			RGBA32F			CUSTOM0			-
			RGBA32F			CUSTOM1			-
			RGBA32F			CUSTOM2			-
			RGBA32F			CUSTOM3			-
		*/
		surfaceArray[(int)Mesh.ArrayType.Vertex] = pos;
		surfaceArray[(int)Mesh.ArrayType.TexUV] = uv;
		surfaceArray[(int)Mesh.ArrayType.Index] = indices;

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
	int tex_size = 1024;
	// cube_size defines the resolution of the cube that will be calculated on compute_shader
	// higher values means better resolution, but possibly produce also more quads, so update tex_size
	float cube_size = 128.0f;
	Rid current_tex;

	Rid counter_buffer;

	Texture2Drd shader_texture;
	UniformBuffer uniformBuffer_data;
	RenderingDevice RD;
	ComputeShaderHandler computeHandler;

	public void createUniforms(int width, int height, float cubeSize) {
		uniformBuffer_data = new UniformBuffer((uint)width, (uint)height, cubeSize, (uint)((width * height)/4));
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
