using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// https://github.com/erickweil/GodotTests/blob/v4.2-beta1/HelloComputeShader/ComputeShaderHandler.cs
public class UniformSetStore : IDisposable {
	public class UniformSet {
		Godot.Collections.Array<RDUniform> uniformList;
		public Rid rid;

		public UniformSet() {
			uniformList = new Godot.Collections.Array<RDUniform>();
		}

		public void addUniform(RDUniform uniform) {
			uniformList.Add(uniform);
		}

		public void create(RenderingDevice rd, Rid shader, uint shaderSet) {
			rid = rd.UniformSetCreate(uniformList, shader, shaderSet);
		}
	}

    public UniformSetStore(RenderingDevice rd, Rid shader) {
		this.RD = rd;
        this.shader = shader;
		
		uniformSets = new List<UniformSet>();
	}

	public RenderingDevice RD;
    public Rid shader;
	public List<UniformSet> uniformSets;
    public byte[] pushConstant;

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
	public void putBufferUniform(Rid buffer, int set, int binding, 
	RenderingDevice.UniformType uniformType = RenderingDevice.UniformType.StorageBuffer) {
		// Create a uniform to assign the buffer to the rendering device
		var uniform = new RDUniform
		{
			UniformType = uniformType,
			Binding = binding
		};
		uniform.AddId(buffer);

		if(uniformSets.Count == set) {
			uniformSets.Add(new UniformSet());
		}
		UniformSet uniformSet = uniformSets[set];

		uniformSet.addUniform(uniform);
	}

	public void resetUniformSets() {
		for(int i=0;i<uniformSets.Count;i++) {
			if(RD.UniformSetIsValid(uniformSets[i].rid))
			RD.FreeRid(uniformSets[i].rid);
		}

		uniformSets = new List<UniformSet>();
	}

    public void createAllUniformSets() {
		for(int i=0;i<uniformSets.Count;i++) {
			uniformSets[i].create(RD,shader,(uint)i);
		}
	}

	public void createUniformSet(int set) {
		uniformSets[set].create(RD,shader,(uint)set);
	}

    public void Dispose()
	{
		GD.Print("Dispose UniformSetStore");
		for(int i = 0;i < uniformSets.Count; i++) {
			if(RD.UniformSetIsValid(uniformSets[i].rid))
			RD.FreeRid(uniformSets[i].rid);
		}
        uniformSets.Clear();
        uniformSets = null;
        pushConstant = null;
        RD = null;
	}
}