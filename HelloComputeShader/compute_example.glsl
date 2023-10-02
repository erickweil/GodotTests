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
// 64x1x1 = 64 local invocations per workgroup.
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;


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
layout(set = 0, binding = 0, std430) restrict buffer MyDataBuffer {
    float data[];
}
my_data_buffer;

// Share data between computer shader invocations
shared float sharedData[64];

// The code we want to execute in each invocation
void main() {
	// Grab the current pixel's position from the ID of this specific invocation ("thread").
	// https://registry.khronos.org/OpenGL-Refpages/gl4/html/gl_GlobalInvocationID.xhtml
    //ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
	//uint bufferIndex = coords.y*128 + coords.x;
	//float factor = bufferIndex / (128.0*128.0);

	uint bufferIndex = gl_GlobalInvocationID.x;
	float factor = bufferIndex * 1.0;

	sharedData[gl_LocalInvocationIndex] = factor;

	/*  https://registry.khronos.org/OpenGL-Refpages/gl4/html/barrier.xhtml
		For any given static instance of barrier in a compute shader, all invocations within a single work group must enter
		 it before any are allowed to continue beyond it. This ensures that values written by one invocation prior to a given 
		 static instance of barrier can be safely read by other invocations after their call to the same static instance of barrier.
	*/
	// Even though this slow things down, Without this barrier, the values can be trash when reading below
	barrier();

	// Now we can read from the shared data
	float sum = 0.0;
	for (int j = 0; j < 64; j++)
	{
		sum = max(sharedData[j], sum);
	}

    my_data_buffer.data[bufferIndex] = sum;
	
}