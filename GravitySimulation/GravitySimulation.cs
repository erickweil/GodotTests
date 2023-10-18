using System;
using System.Runtime.InteropServices;
using Godot;

// https://github.com/godotengine/godot/pull/68235
// https://ask.godotengine.org/134841/rendering-triangle-using-renderingdevice-api
public partial class GravitySimulation : SubViewport {
    // Called when the node enters the scene tree for the first time.
	RenderingDevice RD;
	ComputeShaderHandler gravityAttract;
	ComputeShaderHandler gravityIntegrate;
	ComputeTexClear texClearer;
	GObj[] objects;
	Rid objects_buffer;
	Rid output_tex;

	ParamsBuffer params_data;
	
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

		public const int NUMBYTES = 64;

		public GObj(float mass, Vector3 pos, Vector3 vel, Color color) {
			this.pos = pos;
			this.upos = pos;
			this.vel = vel;
			this.color = new Vector3(color.R,color.G,color.B);
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
	
	[StructLayout(LayoutKind.Sequential)]
	struct ParamsBuffer {
        public uint width;
	    public uint height;
        public uint nObjects;
		public float mousex;
		public float mousey;
		public float mousez;

		public uint pad0,pad1;
        public ParamsBuffer(uint width, uint height, uint nObjects)
        {
            this.width = width;
            this.height = height;

            this.nObjects = nObjects;
            this.mousex = 0;
			this.mousey = 0;
			this.mousez = 100000.0f;
			pad0 = 0;
			pad1 = 0;
            // tem que ser múltiplo de 16 bytes
        }
    }

	public override void _Ready() {
		initializeCompute();
	}

	public Vector3 randV(Random rdn) {
		return new Vector3(rdn.NextSingle(),rdn.NextSingle(),rdn.NextSingle()) - new Vector3(rdn.NextSingle(),rdn.NextSingle(),rdn.NextSingle());
	}
	// https://github.com/godotengine/godot/pull/79696
	public void initializeCompute() {
		RD = RenderingServer.GetRenderingDevice();
		gravityAttract = new ComputeShaderHandler(false,RD);
		gravityAttract.loadShader("res://GravitySimulation/compute_gravity_attract.glsl",64,1,1);

		gravityIntegrate = new ComputeShaderHandler(false,RD);
		gravityIntegrate.loadShader("res://GravitySimulation/compute_gravity_integrate.glsl",64,1,1);

		var tex = GetTexture();

		setupObjectsBuffer(tex);

		setupAttract();
		setupIntegrate(tex);

		// setup texClearer
		texClearer = new ComputeTexClear(false,RD);
		texClearer.assignTexture(output_tex);

		//var viewport = GetViewport() as SubViewport;
		//viewport.RenderTargetClearMode = ClearMode.Always;

		testar(1);
	}

	public void spawnRandomObjs(Vector3 poff, Vector3 voff, int nStart, int nEnd, Color color) {
		Random rdn = Random.Shared;
		for(int i=nStart;i< nEnd && i < objects.Length;i++) {
			objects[i] = new GObj(1.0f,
				randV(rdn)/16.0f+poff,
				//new Vector3(i*1.0f/objects.Length,0.5f,0.5f),
				randV(rdn)/4.0f+voff,
				color
				//Color.FromHsv(rdn.NextSingle(),rdn.NextSingle(),1.0f)
			);
		}
	}

	public void setupObjectsBuffer(ViewportTexture tex) {
		objects = new GObj[64*64*2];

		Random rdn = Random.Shared;
		for(int i=0;i<32;i++) {
			int n3 = objects.Length/32;
			float rotInc = (Mathf.Pi*2.0f) / 32.0f;
			Transform3D rot = Transform3D.Identity.Rotated(new Vector3(0.0f,1.0f,0.0f),rotInc * i);
			Vector3 origin = rot * new Vector3(0.45f,0.0f,0);
			Vector3 vel = rot * new Vector3(0.0f,0.0f,1.2f);
			spawnRandomObjs(origin/2.0f + new Vector3(0.5f,0.5f,0.5f),vel,n3*i,n3*(i+1), Color.FromHsv(rdn.NextSingle(),rdn.NextSingle(),1.0f));
		}
		

		objects_buffer = ComputeShaderHandler.createStructArrayBuffer(RD,objects,GObj.NUMBYTES);

		params_data = new ParamsBuffer((uint)tex.GetWidth(), (uint)tex.GetHeight(), (uint)objects.Length);
	}

	public void setupAttract() {
		gravityAttract.putBufferUniform(objects_buffer, 0, 0, RenderingDevice.UniformType.StorageBuffer);

        gravityAttract.pushConstant = ComputeShaderHandler.GetBytesFromStruct(params_data);

		gravityAttract.createPipeline();
	}

	public void setupIntegrate(ViewportTexture tex) {
		gravityIntegrate.putBufferUniform(objects_buffer, 0, 0, RenderingDevice.UniformType.StorageBuffer);

		//output_tex = ComputeShaderHandler.createNewRDTexture(RD,objects.Length,GObj.NUMBYTES);
        output_tex = RenderingServer.TextureGetRdTexture(tex.GetRid());
        gravityIntegrate.putBufferUniform(output_tex, 0, 1, uniformType: RenderingDevice.UniformType.Image);

        gravityIntegrate.pushConstant = ComputeShaderHandler.GetBytesFromStruct(params_data);

		gravityIntegrate.createPipeline();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	//public override void _Process(double delta)
	public override void _Process(double delta)	{
		if(Input.IsActionPressed("ui_up")) {
			testar(1);
		}

		if(Input.IsActionPressed("ui_left")) {
			testar(100);
		}

		if(Input.IsActionJustPressed("ui_down")) {
			testarLer();
		}
	}

	public void testar(int steps) {
		ulong startTime = startMeasure();

		var tex = GetTexture();
		int width = tex.GetWidth();
		int height = tex.GetHeight();
		Vector2 pos = GetMousePosition();

			params_data.mousex = pos.X / width;
			params_data.mousey = pos.Y / width;
		if(Input.IsMouseButtonPressed(MouseButton.Left)) {
			params_data.mousez = 0.5f;
		}

		byte[] pushConstant = ComputeShaderHandler.GetBytesFromStruct(params_data);

		gravityAttract.pushConstant = pushConstant;
		gravityIntegrate.pushConstant = pushConstant;

		// Limpa a textura só uma vez
		texClearer.runClear(Color.Color8(0,0,0),(uint)width,(uint)height);

		for(int i=0;i<steps;i++) {
			gravityAttract.dispatchPipeline((uint)objects.Length,1,1);

			// Barrier? precisa mesmo disso?

			gravityIntegrate.dispatchPipeline((uint)objects.Length,1,1);
		}

		if(Input.IsActionJustPressed("ui_up")) {
			
			GD.Print(pos);
			GD.Print("Tempo: "+ getMeasureMs(startTime) +"ms");
		}
	}

	public void testarLer() {
		ulong startTime = startMeasure();
		
		// Read back the data from the buffers
		GObj[] output = new GObj[objects.Length];
		gravityIntegrate.readStructArrayBuffer(objects_buffer,output);
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
		gravityAttract.Dispose();
		gravityIntegrate.Dispose();
		texClearer.Dispose();
		RD.FreeRid(objects_buffer);
	}

	public static ulong startMeasure() {
		return Time.GetTicksUsec();
	}

	public static double getMeasureMs(ulong startTime) {
		return (Time.GetTicksUsec() - startTime) / 1000.0;
	}
}