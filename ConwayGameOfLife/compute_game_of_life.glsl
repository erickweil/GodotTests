#[compute]
#version 450
/* https://docs.godotengine.org/en/stable/tutorials/shaders/compute_shaders.html
These two lines communicate two things:
    The following code is a compute shader. This is a Godot-specific hint that is needed for the editor to properly import the shader file.
    The code is using GLSL version 450.
	
This code takes an array of floats, multiplies each element by 2 and store the results back in the buffer array.
*/


/* Invocations in the (x, y, z) dimension
	Next, we communicate the number of invocations to be used in each workgroup. 
	Invocations are instances of the shader that are running within the same workgroup. 
	When we launch a compute shader from the CPU, we tell it how many workgroups to run. 
	Workgroups run in parallel to each other.

	While running one workgroup, you cannot access information in another workgroup.
	However, invocations in the same workgroup can have some limited access to other invocations.
*/
// Multiply each component to calculate how many invocations per workgroup
// 8x1x1 = 8 local invocations per workgroup.
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;


/* A binding to the buffer we create in our script
Here we provide information about the memory that the compute shader will have access to. 
The layout property allows us to tell the shader where to look for the buffer, 
we will need to match these set and binding positions from the CPU side later.

The restrict keyword tells the shader that this buffer is only going to be accessed from one place in this shader.
In other words, we won't bind this buffer in another set or binding index. 
This is important as it allows the shader compiler to optimize the shader code. Always use restrict when you can.

This is an unsized buffer, which means it can be any size. 
So we need to be careful not to read from an index larger than the size of the buffer.
*/

// Explicacao do que e std430 https://www.khronos.org/opengl/wiki/Interface_Block_(GLSL)
/*
std140: This layout alleviates the need to query the offsets for definitions. The rules of std140 layout explicitly
	state the layout arrangement of any interface block declared with this layout. 
	This also means that such an interface block can be shared across programs, much like shared.
	The only downside to this layout type is that the rules for packing elements into arrays/structs can introduce a lot of unnecessary padding.
	
	The rules for std140 layout are covered quite well in the OpenGL specification (OpenGL 4.5, Section 7.6.2.2, page 137).
	Among the most important is the fact that arrays of types are not necessarily tightly packed. 
	An array of floats in such a block will not be the equivalent to an array of floats in C/C++. 
	The array stride (the bytes between array elements) is always rounded up to the size of a vec4 (ie: 16-bytes). 
	So arrays will only match their C/C++ definitions if the type is a multiple of 16 bytes

	Warning: Implementations sometimes get the std140 layout wrong for vec3 components. You are advised to manually pad your
	 structures/arrays out and avoid using vec3 at all.

std430: This layout works like std140, except with a few optimizations in the alignment and strides for arrays and
	structs of scalars and vector elements (except for vec3 elements, which remain unchanged from std140). 
	Specifically, they are no longer rounded up to a multiple of 16 bytes. So an array of `float`s will match with a C++ array of `float`s.

	Note that this layout can only be used with shader storage blocks, not uniform blocks. 
*/
//layout(set = 0, binding = 0, std430) restrict buffer MyDataBuffer {
    //vec4 data[];
//}
//my_data_buffer;

// Prepare memory for the image, which will be both read and written to
// `restrict` is used to tell the compiler that the memory will only be accessed
// by the `heightmap` variable.

// https://vkguide.dev/docs/chapter-4/descriptors/
layout(rgba8, set = 0, binding = 0) uniform restrict readonly image2D _grid_previous;
layout(rgba8, set = 0, binding = 1) uniform restrict writeonly image2D _grid_current;

// Our push PushConstant
layout(push_constant, std430) uniform Params {
	uint width;
	uint height;
	uint mousex;
	uint mousey;
} _params;

ivec2 coord(uint x, uint y) {
	return ivec2((x+1) % _params.width,
				(y+1) % _params.height);
}

// The code we want to execute in each invocation
void main() {
	// Grab the current pixel's position from the ID of this specific invocation ("thread").
	// https://registry.khronos.org/OpenGL-Refpages/gl4/html/gl_GlobalInvocationID.xhtml
    uvec3 id = gl_GlobalInvocationID;
	
	if(id.x >= _params.width || id.y >= _params.height) return;
	// Store the pixel back into the image.
	// WARNING: make sure you are writing to the same coordinate that you read from.
	// If you don't, you may end up writing to a pixel, before that pixel is read
	// by a different invocation and cause errors.
	float neighCount = 0;
	neighCount += imageLoad(_grid_previous, coord(id.x-1,id.y-1)).r;
	neighCount += imageLoad(_grid_previous, coord(id.x-1,id.y)).r;
	neighCount += imageLoad(_grid_previous, coord(id.x-1,id.y+1)).r;

	neighCount += imageLoad(_grid_previous, coord(id.x,id.y-1)).r;
	float cell = imageLoad(_grid_previous, coord(id.x,id.y)).r;
	neighCount += imageLoad(_grid_previous, coord(id.x,id.y+1)).r;

	neighCount += imageLoad(_grid_previous, coord(id.x+1,id.y-1)).r;
	neighCount += imageLoad(_grid_previous, coord(id.x+1,id.y)).r;
	neighCount += imageLoad(_grid_previous, coord(id.x+1,id.y+1)).r;
	
	//  mais que 3 (4,5,6,7,8,9) morre
	//  ou menos que 2 (1,0) morre
	if(neighCount > 3.1 || neighCount < 1.9) {
		cell = 0.0;
	} else if(neighCount > 2.9) { //  3 vive
		cell = 1.0;
	}
	//  2 continua o valor atual

	// mouse
	//if(id.x >= _params.mousex -5 && id.x <= _params.mousex + 5 && id.y >= _params.mousey -5 && id.y <= _params.mousey + 5) {
	if(id.x + id.y == _params.mousex) {
		cell = 1.0;
	}

	vec4 newpixel = vec4(cell,cell,cell,1.0);

	imageStore(_grid_current, coord(id.x,id.y), newpixel);
}