using System;
using Godot;

// https://github.com/godotengine/godot/pull/68235
// https://ask.godotengine.org/134841/rendering-triangle-using-renderingdevice-api
public partial class ComputePipeline : SubViewport {
	Rid framebuffer;
	Rid pipeline;
	Rid shader;
	Rid vtx_buf;
	Rid color_buf;
	Rid vtx_array;
	Color[] clear_colors;
	
	RenderingDevice RD;

	ComputeShaderHandler computeHandler;
	float[] input;
	Rid input_buffer;

	public void createFrameBuffer() {
		var vptex = RenderingServer.TextureGetRdTexture(GetTexture().GetRid());
		framebuffer = RD.FramebufferCreate(new Godot.Collections.Array<Rid>{ vptex });
	}

	public override void _Ready() {
		clear_colors = new Color[] {Color.Color8(0,0,0,0)};
		RD = RenderingServer.GetRenderingDevice();

		computeHandler = new ComputeShaderHandler(false, RD);
		computeHandler.loadShader("res://RenderingPipeline/compute_pipeline.glsl",8,1,1);


		// Carregar Shader
		var shaderFile = GD.Load<RDShaderFile>("res://RenderingPipeline/shader.glsl");
		var shaderSpirV = shaderFile.GetSpirV();
		shader = RD.ShaderCreateFromSpirV(shaderSpirV);

		// https://paroj.github.io/gltut/Basics/Tut02%20Vertex%20Attributes.html

		uint nVertices = 6;
		var vertices = new float[] {
			// Vertices
			 -1.0f, -1.0f, 0.0f, 
			 -1.0f,  1.0f, 0.0f, 
			  1.0f, -1.0f, 0.0f,
			  1.0f, -1.0f, 0.0f, 
			 -1.0f,  1.0f, 0.0f, 
			  1.0f,  1.0f, 0.0f
		};
		var colors = new float[] {
			// Colors
			0.0f, 0.0f, 1.0f, 1.0f,
			1.0f, 0.0f, 1.0f, 1.0f,
			0.0f, 1.0f, 1.0f, 1.0f,
			1.0f, 1.0f, 0.0f, 1.0f,
			0.0f, 1.0f, 0.0f, 1.0f,
			0.0f, 1.0f, 1.0f, 1.0f,
			0.0f, 0.0f, 0.0f, 0.0f,
			0.0f, 0.0f, 0.0f, 0.0f
		};

		// Definir buffers
		var vattr = new RDVertexAttribute();
		vattr.Format = RenderingDevice.DataFormat.R32G32B32Sfloat;
		vattr.Location = 0;
		vattr.Stride = sizeof(float) * 3; // quantos bytes por vértice
		vattr.Offset = 0;

		var points_raw = new byte[vertices.Length * sizeof(float)];
		Buffer.BlockCopy(vertices, 0, points_raw, 0, points_raw.Length);
		vtx_buf = RD.VertexBufferCreate((uint)points_raw.Length,points_raw);

		var vattr_color = new RDVertexAttribute();
		vattr_color.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
		vattr_color.Location = 1;
		vattr_color.Stride = sizeof(float) * 4; // quantos bytes por vértice
		vattr_color.Offset = 0;

		var colors_raw = new byte[colors.Length * sizeof(float)];
		Buffer.BlockCopy(colors, 0, colors_raw, 0, colors_raw.Length);
		color_buf = RD.VertexBufferCreate((uint)colors_raw.Length,colors_raw, true);
		//color_buf = computeHandler.createFloatBuffer(colors);

		var vtx_format = RD.VertexFormatCreate(new Godot.Collections.Array<RDVertexAttribute>{ vattr, vattr_color });
		vtx_array = RD.VertexArrayCreate(nVertices, vtx_format, new Godot.Collections.Array<Rid> { vtx_buf, color_buf});

		createFrameBuffer();

		var blend = new RDPipelineColorBlendState();
		blend.Attachments.Add(new RDPipelineColorBlendStateAttachment());
		pipeline = RD.RenderPipelineCreate(
			shader,
			RD.ScreenGetFramebufferFormat(),
			vtx_format,
			RenderingDevice.RenderPrimitive.Triangles,
			new RDPipelineRasterizationState(),
			new RDPipelineMultisampleState(),
			new RDPipelineDepthStencilState(),
			blend
		);

		computeHandler.putBufferUniform(color_buf,0,0);
		// Defining a compute pipeline
		computeHandler.createPipeline();

		GD.Print("OK");
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		RD.FreeRid(vtx_array);
		RD.FreeRid(vtx_buf);
		RD.FreeRid(pipeline);
		RD.FreeRid(framebuffer);
		RD.FreeRid(shader);
	}

	public override void _Process(double delta)
	{
		if(!Input.IsActionJustPressed("ui_up")) return;

		//computeHandler.dipatchPipeline(8,1,1);
		
		// handle resizing
		if(!RD.FramebufferIsValid(framebuffer)) {
			createFrameBuffer();
		}

		var draw_list = RD.DrawListBegin(framebuffer,
		RenderingDevice.InitialAction.Clear, RenderingDevice.FinalAction.Read,
		RenderingDevice.InitialAction.Clear, RenderingDevice.FinalAction.Read,
		clear_colors);
		RD.DrawListBindRenderPipeline(draw_list, pipeline);
		RD.DrawListBindVertexArray(draw_list, vtx_array);
		RD.DrawListDraw(draw_list, false, 1);
		RD.DrawListEnd();
	}
}
