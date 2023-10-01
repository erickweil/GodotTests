#[compute]
#version 450

// Multiply each component to calculate how many invocations per workgroup
// 8x8x1 = 64 local invocations per workgroup.
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(rgba8, set = 0, binding = 0) uniform restrict writeonly image2D _output_tex;

// Our push PushConstant
layout(push_constant, std430) uniform Params {
    uint startx;
	uint starty;
	uint endx;
    uint endy;

    vec4 color;
} _params;

// The code we want to execute in each invocation
void main() {
    if(gl_GlobalInvocationID.x < _params.endx && gl_GlobalInvocationID.y < _params.endy) {
        ivec2 texcoord = ivec2(gl_GlobalInvocationID.xy);
	    imageStore(_output_tex, texcoord, _params.color);
    }
}