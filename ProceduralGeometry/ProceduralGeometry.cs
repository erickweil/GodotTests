using System;
using System.Runtime.Intrinsics.Arm;
using Godot;

// A ideia é ser capaz de Renderizar algo no Compute shader e então ver
// o resultado sem uma leitura no CPU
public partial class ProceduralGeometry : Node3D {
	
	[Export]
	MeshInstance3D meshInstance;

	struct UniformBuffer {
		public uint texWidth;
		public uint texHeight;
		public float cubeSize;
		public uint maxTriangles;

		public float offx;
		public float offy;
		public float offz;
		float pad0;
		public UniformBuffer(uint texWidth, uint texHeight,float cubeSize, uint maxTriangles)
		{
			this.texWidth = texWidth;
			this.texHeight = texHeight;
			this.cubeSize = cubeSize;
			this.maxTriangles = maxTriangles;

			// tem que ser múltiplo de 16 bytes
		}
	}

	public override void _Ready() {
		RenderingServer.CallOnRenderThread(Callable.From(computeInit));


		var material = meshInstance.MaterialOverride as ShaderMaterial;
		//shader_texture = (Texture2Drd)material.GetShaderParameter("image");
		shader_texture = new Texture2Drd();
		material.SetShaderParameter("image", shader_texture);

		meshInstance.Mesh = getTrianglesMesh((tex_size * tex_size)/3);
	}

	public override void _Process(double delta)
	{		
		if(RD == null || computeHandler == null) return;

		shader_texture.TextureRdRid = current_tex;

		Vector2 mouse = Input.GetLastMouseVelocity();
		//meshInstance.RotateX(0.005f * (float)delta * mouse.X);
		//meshInstance.RotateY(0.005f * (float)delta * mouse.Y);

		mouse_off += (float)delta * new Vector3(mouse.Y / 500.0f,0,  -mouse.X / 500.0f);

		if(Input.IsActionJustPressed("ui_up") || Input.IsActionPressed("ui_down"))
		RenderingServer.CallOnRenderThread(Callable.From(computeProcess));
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		if(shader_texture != null) {
			shader_texture.TextureRdRid = new Rid();
		}

		RenderingServer.CallOnRenderThread(Callable.From(computeDispose));
	}

	public ArrayMesh getTrianglesMesh(int nTriangles) {

		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#doc-arraymesh
		var surfaceArray = new Godot.Collections.Array();
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		Vector3[] pos = new Vector3[nTriangles*3];
		Vector2[] uv = new Vector2[nTriangles*3];
		int[] indices = new int[nTriangles*3];
		for(int i=0;i<nTriangles;i++) {
			int i0 = i*3 + 0;
			int i1 = i*3 + 1;
			int i2 = i*3 + 2;
			Vector3 off = new Vector3(i % tex_size, 0 , i / tex_size);
			pos[i0] = new Vector3(1,0,0) + off;
			pos[i1] = new Vector3(0,0,1) + off;
			pos[i2] = new Vector3(0,0,0) + off;
			uv[i0] = new Vector2(1,0);
			uv[i1] = new Vector2(0,1);
			uv[i2] = new Vector2(0,0);
			indices[i0] = i0;
			indices[i1] = i1;
			indices[i2] = i2;
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
	int tex_size = 256;
	float cube_size = 64.0f;
	Rid current_tex;

	Rid counter_buffer;

	Texture2Drd shader_texture;
	UniformBuffer uniformBuffer_data;
	RenderingDevice RD;
	ComputeShaderHandler computeHandler;

	public void createUniforms(int width, int height, float cubeSize) {
		uniformBuffer_data = new UniformBuffer((uint)width, (uint)height, cubeSize, (uint)((width * height)/3));
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
	}

	public void computeProcess() {
		ulong startTime = Time.GetTicksUsec();

		if(!RD.UniformSetIsValid(computeHandler.uniformSets[0].rid)) {
			//GD.Print("Re-doing uniform set");
			//computeHandler.resetUniformSets();
			//RD.FreeRid(current_tex);
			//createUniforms(tex_size,tex_size);
			//computeHandler.createUniformSet(0);

			GD.PrintErr("Uniform set invalid");
			return;
		}

		// uniformBuffer_data.width = width;
		// uniformBuffer_data.height = height;
		// uniformBuffer_data.mousex = (uint)Math.Clamp((int)pos.X,0,(int)width);
		// uniformBuffer_data.mousey = (uint)Math.Clamp((int)pos.Y,0,(int)height);
		// computeHandler.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		uniformBuffer_data.offx = mouse_off.X;
		uniformBuffer_data.offy = mouse_off.Y;
		uniformBuffer_data.offz = mouse_off.Z;
		computeHandler.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		// Resetar counter
		uint[] input = new uint[1] {0};
		ComputeShaderHandler.updateBufferFromArray(computeHandler.RD, counter_buffer,input,sizeof(uint));

		computeHandler.dipatchPipeline(64,64,64);

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
