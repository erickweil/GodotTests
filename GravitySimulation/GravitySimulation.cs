using System;
using System.Runtime.InteropServices;
using Godot;

// https://github.com/godotengine/godot/pull/68235
// https://ask.godotengine.org/134841/rendering-triangle-using-renderingdevice-api
public partial class GravitySimulation : SubViewport {
    // Called when the node enters the scene tree for the first time.
	ComputeShaderHandler computeHandler;
	int nObjects;
	GObj[] input;
	Rid input_buffer;
	
	[StructLayout(LayoutKind.Sequential)]
    struct GObj {
		// Basically even if vec3, it will be aligned to match 4 float leaving 4 empty bytes
		Vector3 pos; // 16
		float ax;
		Vector3 upos; // 32
		float ay;
		Vector3 vel; // 48
		float az;
		// Here color and mass together span the same as one vec4
		Vector3 color; // 64 (color alpha is mass)
		float mass;

		public GObj(float mass, Vector3 pos, Vector3 vel, Vector3 color) {
			this.pos = pos;
			this.upos = pos;
			this.vel = vel;
			this.color = color;
			this.mass = mass;
			this.ax = 0.0f;
			this.ay = 0.0f;
			this.az = 0.0f;
		}

		public Vector3 Accel {
			get {
				return new Vector3(ax,ay,az);
			}
		}

		public override string ToString() {
			return pos+", "+upos+", "+vel+", "+color+" "+mass+" "+Accel;
    	}
    };

	public override void _Ready() {
		initializeCompute();
	}

	// https://github.com/godotengine/godot/pull/79696
	public void initializeCompute() {
		computeHandler = new ComputeShaderHandler(false);
		computeHandler.loadShader("res://GravitySimulation/compute_gravity.glsl",64,1,1);

		// Prepare our data. We use floats in the shader, so we need 32 bit.
		nObjects = 64;
		input = new GObj[nObjects];

		Random rdn = Random.Shared;
		for(int i=0;i<nObjects;i++) {
			input[i] = new GObj(1.0f,
				new Vector3(rdn.NextSingle(),rdn.NextSingle(),rdn.NextSingle()),
				new Vector3(rdn.NextSingle(),rdn.NextSingle(),rdn.NextSingle()),
				new Vector3(rdn.NextSingle(),rdn.NextSingle(),rdn.NextSingle())
			);
		}

		input_buffer = ComputeShaderHandler.createStructArrayBuffer(computeHandler.RD,input,64);

		computeHandler.putBufferUniform(input_buffer, 0, 0, RenderingDevice.UniformType.StorageBuffer);

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
		
		computeHandler.dipatchPipeline((uint)nObjects,1,1);

		GD.Print("Tempo: "+ getMeasureMs(startTime) +"ms");
	}

	public void testarLer() {
		ulong startTime = startMeasure();
		
		// Read back the data from the buffers
		GObj[] output = new GObj[nObjects];
		computeHandler.readStructArrayBuffer(input_buffer,output);
		//var output =  computeHandler.readFloatBuffer(input_buffer);

		//GD.Print("Input: ", string.Join(", ", input));
		//GD.Print("Output: ", string.Join(", ", output));
		for(int i=0;i<4;i++) {
			GD.Print("i:",i," "+output[i]);
		}
		for(int i=output.Length - 4;i<output.Length;i++) {
			GD.Print("i:",i," "+output[i]);
		}

		GD.Print("Tempo: "+ getMeasureMs(startTime) +"ms");
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