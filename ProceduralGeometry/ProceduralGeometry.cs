using System;
using System.Collections.Generic;
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

		meshInstance.CustomAabb = new Aabb(new Vector3(0,0,0), new Vector3(10,10,10));
		if(resizeBasedOnCounter) {
			int tris = 1024; // start with small mesh
			meshInstance.Mesh = getTrianglesMesh(tris);
		} else {
			int tris = ((tex_size * tex_size) / pixels_per_vertex) / 3;
			meshInstance.Mesh = getTrianglesMesh(tris);
		}
	}

	public override void _Process(double delta)
	{		
		if(RD == null || computeHandler == null) return;

		shader_texture.TextureRdRid = current_tex;

		if(Input.IsPhysicalKeyPressed(Key.G)) {
			if(controlWithMouse) {
				Vector2 mouse = Input.GetLastMouseVelocity();
				mouse_off += (float)delta * new Vector3(mouse.Y / 1000.0f,0,  -mouse.X / 1000.0f);
			} else {
				float seconds = Time.GetTicksUsec() / 1000000.0f;
				mouse_off = new Vector3(Mathf.Sin(seconds) - 1.0f,0,Mathf.Cos(seconds) - 1.0f) / 20.0f;
			}

			RenderingServer.CallOnRenderThread(Callable.From(computeProcess));
		}

		if(resizeBasedOnCounter) {
			if(lastMeshTris < counter_lastValue || lastMeshTris > counter_lastValue*2) {
				int newSize = Math.Clamp(nextPowerOf2(counter_lastValue),1024,536870912);

				if(newSize != lastMeshTris) { 
					GD.Print("Updating mesh to "+newSize+" tris");
					meshInstance.Mesh = getCachedTrianglesMesh(newSize);
				}
			}
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

	int lastMeshTris = 0;
	Vector3[] dummy_65537;

	// Experimenting with caching meshes when dynamic resizing
	Dictionary<int,ArrayMesh> availableMeshes;
	public ArrayMesh getCachedTrianglesMesh(int nTris) {
		if(availableMeshes == null) {
			availableMeshes = new Dictionary<int, ArrayMesh>();
		}

		if(availableMeshes.ContainsKey(nTris)) {

			lastMeshTris = nTris;
			return availableMeshes[nTris];
		} else {
			ArrayMesh ret = getTrianglesMesh(nTris);
			availableMeshes[nTris] = ret;

			lastMeshTris = nTris;
			return ret;
		}
	}
	// Generate a mesh that every 3 vertices define a disconected tri
	public ArrayMesh getTrianglesMesh(int nTris) {
		GD.Print("Generating mesh with "+nTris+" tris");
		// NO NEED TO PROVIDE VERTEX INFO. JUST INDICES ARE ENOUGH, 
		// see: https://github.com/godotengine/godot/issues/19473
		// and: https://github.com/godotengine/godot/pull/62046
		int[] indices = new int[nTris*3];
		for(int i=0;i<nTris;i++) {
			indices[i*3 + 0] = i*3 + 0;
			indices[i*3 + 1] = i*3 + 1;
			indices[i*3 + 2] = i*3 + 2;
		}

		// MESH WITHOUT VERTICES, ONLY INDICES ARE ENOUGH
		// https://docs.godotengine.org/en/stable/tutorials/3d/procedural_geometry/arraymesh.html#doc-arraymesh
		var surfaceArray = new Godot.Collections.Array();
		surfaceArray.Resize((int)Mesh.ArrayType.Max);

		/*// there is a bug that even using 'FlagUsesEmptyVertexArray', if you want more than 65k indices
		// to work, you need to provide a vertex array such that the 32 bit index is used instead of 16 bit
		// see https://github.com/godotengine/godot/issues/83446
		if(indices.Length > 65535) {
			// Provide a ever so slightly bigger vertex array such that the 16 bit index is not used
			if(dummy_65537 == null) {
				dummy_65537 = new Vector3[65537];
			}
			surfaceArray[(int)Mesh.ArrayType.Vertex] = dummy_65537;
		} else {
			// Provide dummy vertex array. Still needed even using 'FlagUsesEmptyVertexArray'
			surfaceArray[(int)Mesh.ArrayType.Vertex] = new Vector3[1];
		}*/

		surfaceArray[(int)Mesh.ArrayType.Index] = indices;

		// The idea is that since all this data is on the texture, the mesh doesn't need to store it
		// And would be a waste of memory to store it twice		
		var flags = Mesh.ArrayFormat.FlagUsesEmptyVertexArray; // NEED TO SPECIFY THAT THE VERTEX ARRAY IS EMPTY
		var arrMesh = new ArrayMesh();
		arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArray,null,null, flags);

		lastMeshTris = nTris;
		return arrMesh;
	}

	// =============================
	// Need to run on Render Thread
	// =============================

	// EXPERIMENTING WITH DYNAMIC MESH SIZE
	// Basically, the counter read from the compute shader is used to know how many triangles the mesh needs
	// then the nearest power of two is used, so that in the worst case the mesh will have 2x the number of triangles needed
	// Needs testing to see if the overhead of reading data from the GPU + changing meshes isn't greater than the overhead of just having a bigger mesh
	// Obs: doesn't work well if the rendering thread model isn't single-safe, because the mesh is 1 frame behind
	const bool resizeBasedOnCounter = true;

	const bool controlWithMouse = true;
	Vector3 mouse_off;

	// Change this accordingly.
	// tex_size should be choosed so that (tex_size*tex_size)/2 equals the maximum amount of vertices
	// Keep in mind this 'maximum amount' is always being drawed, just not visible because most of them
	// may produce degenerate triangles reading 0's from the texture.
	int tex_size = 4096;
	// cube_size defines the resolution of the cube that will be calculated on compute_shader
	// higher values means better resolution, but possibly produce also more quads, so update tex_size
	// also should be multiple of 8
	float cube_size = 512.0f;
	Rid current_tex;

	Rid counter_buffer;
	int counter_lastValue;
	int pixels_per_vertex = 2; // 1 for position, 1 for normal

	Rid shader;
	UniformSetStore uniformSet;
	Texture2Drd shader_texture;
	UniformBuffer uniformBuffer_data;
	RenderingDevice RD;
	ComputeShaderHandler computeHandler;

	public void createUniforms(int width, int height, float cubeSize) {
		int vertices = (tex_size * tex_size) / pixels_per_vertex;
		uniformSet = new UniformSetStore(RD, shader);
		uniformBuffer_data = new UniformBuffer((uint)width, (uint)height, cubeSize, (uint)(vertices / 3));
		uniformSet.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		current_tex = ComputeShaderHandler.createNewRDTexture(RD,width,height, format: RenderingDevice.DataFormat.R32G32B32A32Sfloat, usageBits: ComputeShaderHandler.UsageBitsForTexture2DRD);
		uniformSet.putBufferUniform(current_tex, 0, 0, uniformType: RenderingDevice.UniformType.Image);

		uint[] input = new uint[1] {0};
		counter_buffer = ComputeShaderHandler.createArrayBuffer(computeHandler.RD,input,sizeof(uint));
		uniformSet.putBufferUniform(counter_buffer, 0, 1, RenderingDevice.UniformType.StorageBuffer);

		uniformSet.createAllUniformSets();
	}

	public void computeInit() {
		RD = RenderingServer.GetRenderingDevice();

		shader = ComputeShaderHandler.loadShader(RD,"res://ProceduralGeometry/procedural_geometry.glsl");
		
		computeHandler = new ComputeShaderHandler(false,RD);
		computeHandler.setShader(shader, 8, 8, 8);
		// Defining a compute pipeline
		computeHandler.createPipeline();

		createUniforms(tex_size,tex_size,cube_size);

		// Run the first time
		computeProcess();
	}

	public void computeProcess() {
		ulong startTime = Time.GetTicksUsec();

		if(!RD.UniformSetIsValid(uniformSet.uniformSets[0].rid)) {
			GD.PrintErr("Uniform set invalid");
			return;
		}

		uniformBuffer_data.offx = mouse_off.X;
		uniformBuffer_data.offy = mouse_off.Y;
		uniformBuffer_data.offz = mouse_off.Z;
		uniformSet.pushConstant = ComputeShaderHandler.GetBytesFromStruct(uniformBuffer_data);

		// Clear the texture
		RD.TextureClear(current_tex, new Color(0,0,0,0), 0, 1, 0, 1);

		// Resetar counter
		uint[] input = new uint[1] {0};
		ComputeShaderHandler.updateBufferFromArray(computeHandler.RD, counter_buffer,input,sizeof(uint), 1);

		int invocations = (int)cube_size;
		computeHandler.dispatchPipeline(uniformSet, invocations,invocations,invocations);

		if(resizeBasedOnCounter) {
			// Read counter buffer to know if the mesh needs resizing
			ComputeShaderHandler.readArrayBuffer<uint>(RD, counter_buffer, input, sizeof(uint), 1);

			counter_lastValue = (int) input[0];
		}

		// If you want the output of a compute shader to be used as input of
		// another computer shader you'll need to add a barrier:
		// NOT DOING ANY DIFFERENCE BUT LEAVING HERE IF SOMETHING WEIRD HAPPENS
		//RD.Barrier(RenderingDevice.BarrierMask.Compute);

		//GD.Print( (Time.GetTicksUsec() - startTime) / 1000.0, "ms");
	}

	// Finds next power of two 
	// for n. If n itself is a 
	// power of two then returns n
	static int nextPowerOf2(int n)
	{
		n--;
		n |= n >> 1;
		n |= n >> 2;
		n |= n >> 4;
		n |= n >> 8;
		n |= n >> 16;
		n++;
		 
		return n;
	}

	public void computeDispose() {
		uniformSet.Dispose();
		computeHandler.Dispose();
		RD.FreeRid(current_tex);
		RD.FreeRid(counter_buffer);
	}
}
