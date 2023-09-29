# https://github.com/godotengine/godot/pull/68235
# https://ask.godotengine.org/134841/rendering-triangle-using-renderingdevice-api
extends SubViewport

var framebuffer: RID
var pipeline: RID
var shader: RID
var vtx_buf: RID
var vtx_array: RID
var clear_colors := PackedColorArray([Color.TRANSPARENT])
@onready var RD := RenderingServer.get_rendering_device()

func create_framebuffer():
    var vptex := RenderingServer.texture_get_rd_texture(get_texture().get_rid())
    framebuffer = RD.framebuffer_create([vptex])

func _ready():
    #var src := RDShaderSource.new()
    #src.source_fragment = frag_src
    #src.source_vertex = vert_src
    #var spirv := RD.shader_compile_spirv_from_source(src)
    #shader = RD.shader_create_from_spirv(spirv)

    var shader_file = load("res://RenderingPipeline/shader.glsl")
    var shader_spirv: RDShaderSPIRV = shader_file.get_spirv()
    shader = RD.shader_create_from_spirv(shader_spirv)
    
    var vattr := RDVertexAttribute.new()
    vattr.format = RenderingDevice.DATA_FORMAT_R32G32B32_SFLOAT
    vattr.location = 0
    vattr.stride = 4 * 3
    
    var points := PackedFloat32Array([
        0.0, -0.5, 0.0,
        0.5, 0.5, 0.0,
        -0.5, 0.5, 0.0
    ])
    var points_raw := points.to_byte_array()
    vtx_buf = RD.vertex_buffer_create(points_raw.size(), points_raw)
    var vtx_format := RD.vertex_format_create([vattr])
    vtx_array = RD.vertex_array_create(3, vtx_format, [vtx_buf])
    
    create_framebuffer()
    
    var blend := RDPipelineColorBlendState.new()
    blend.attachments.push_back(RDPipelineColorBlendStateAttachment.new())
    pipeline = RD.render_pipeline_create(
        shader,
        RD.screen_get_framebuffer_format(),
        vtx_format,
        RenderingDevice.RENDER_PRIMITIVE_TRIANGLES,
        RDPipelineRasterizationState.new(),
        RDPipelineMultisampleState.new(),
        RDPipelineDepthStencilState.new(),
        blend
    )
    
func _exit_tree():
    RD.free_rid(vtx_array)
    RD.free_rid(vtx_buf)
    RD.free_rid(pipeline)
    RD.free_rid(framebuffer)
    RD.free_rid(shader)

func _process(_delta):
    # handle resizing
    if not RD.framebuffer_is_valid(framebuffer):
        create_framebuffer()

    var draw_list := RD.draw_list_begin(framebuffer,
        RenderingDevice.INITIAL_ACTION_CLEAR, RenderingDevice.FINAL_ACTION_READ,
        RenderingDevice.INITIAL_ACTION_CLEAR, RenderingDevice.FINAL_ACTION_READ,
        clear_colors)
    RD.draw_list_bind_render_pipeline(draw_list, pipeline)
    RD.draw_list_bind_vertex_array(draw_list, vtx_array)
    RD.draw_list_draw(draw_list, false, 1)
    RD.draw_list_end()