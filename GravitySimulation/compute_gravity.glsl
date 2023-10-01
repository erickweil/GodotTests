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

// then entire struct or is each member that must be multiple of 16 bytes?
// https://www.reddit.com/r/opengl/comments/12inqn3/how_does_glsl_alignment_work/
// https://github.com/KhronosGroup/Vulkan-Guide/blob/main/chapters/shader_memory_layout.adoc
// http://www.catb.org/esr/structure-packing/
// https://registry.khronos.org/vulkan/specs/1.3-extensions/html/vkspec.html#interfaces-resources-standard-layout
struct GObj {
	// Basically even if vec3, it will be aligned to match 4 float leaving 4 empty bytes
	vec3 pos; // 16
	float ax;
	vec3 upos; // 32
	float ay;
	vec3 vel; // 48
	float az;
	// Here color and mass together span the same as one vec4
	vec3 color; // 64 (color alpha is mass)
	float mass;
};

layout(set = 0, binding = 0, std430) restrict buffer MyObjects {
    GObj data[];
}
_objects;

// The code we want to execute in each invocation
void main() {
	// Grab the current pixel's position from the ID of this specific invocation ("thread").
	// https://registry.khronos.org/OpenGL-Refpages/gl4/html/gl_GlobalInvocationID.xhtml
    uint id = gl_GlobalInvocationID.x;
	GObj obj = _objects.data[id];
	vec3 pos = obj.pos;
	vec3 vel = obj.vel;
	vec3 accel = vec3(obj.ax,obj.ay,obj.az);

	float dt = 0.016666667;
	vel = vel + accel * dt;
	pos = pos + vel * dt;

	obj.upos = obj.pos;
	obj.pos = pos;
	obj.vel = vel;
	obj.ax = 0;
	obj.ay = 0;
	obj.az = 0;

    _objects.data[id] = obj;
}