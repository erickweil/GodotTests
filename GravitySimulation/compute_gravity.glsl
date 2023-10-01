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
_object_buffer;

layout(rgba8, set = 0, binding = 1) uniform restrict writeonly image2D _output_tex;

// Our push PushConstant
layout(push_constant, std430) uniform Params {
	uint width;
	uint height;
	uint nObjects;
	float mousex;
	float mousey;
	float mousez;
} _params;

// The code we want to execute in each invocation
void main() {
	// Grab the current pixel's position from the ID of this specific invocation ("thread").
	// https://registry.khronos.org/OpenGL-Refpages/gl4/html/gl_GlobalInvocationID.xhtml
    uint id = gl_GlobalInvocationID.x;
	GObj obj = _object_buffer.data[id];
	vec3 pos = obj.pos;
	vec3 vel = obj.vel;
	float mass = obj.mass;
	vec3 accel = vec3(0,0,0);

	float dt = 0.0016666667;
	float G = 0.01;
	
	// calculate force against mouse
	vec3 mouse = vec3(_params.mousex,_params.mousey,_params.mousez);
	// Calculate the distance between the two objects
	vec3 delta = mouse - pos;
	float sqrDist = dot(delta,delta);
	// Calculate the gravitational force magnitude between the two objects
	float forceMagnitude = (G * mass * 100.0f) / max(0.01,sqrDist);
	vec3 forceDirection = normalize(delta);

	// Calculate the gravitational force vector
	vec3 gravitationalForce = forceMagnitude * forceDirection;

	accel += gravitationalForce;

	// apply the force
	// a = F / m
	accel = accel / mass;
	
	vel = vel + accel * dt;
	pos = pos + vel * dt;

	if(pos.x > 5.0 || pos.x < -5.0) vel.x = -vel.x*0.99;
	if(pos.y > 5.0 || pos.y < -5.0) vel.y = -vel.y*0.99;
	if(pos.z > 5.0 || pos.z < -5.0) vel.z = -vel.z*0.99;

	pos = vec3(clamp(pos.x,0.0,1.0),clamp(pos.y,0.0,1.0),clamp(pos.z,0.0,1.0));

	obj.upos = obj.pos;
	obj.pos = pos;
	obj.vel = vel;

    _object_buffer.data[id] = obj;

	//uint tex_x = (id * 4) % _params.width;
	//uint tex_y = (id * 4) / _params.width;
	//imageStore(_output_tex, ivec2(tex_x,tex_y), vec4(obj.pos,1.0));
	//imageStore(_output_tex, ivec2(tex_x+1,tex_y), vec4(obj.upos,1.0));
	//imageStore(_output_tex, ivec2(tex_x+2,tex_y), vec4(obj.vel,1.0));
	//imageStore(_output_tex, ivec2(tex_x+3,tex_y), vec4(obj.color,1.0));

	ivec2 texcoord = ivec2(
		clamp(obj.pos.x * _params.width,0,_params.width-1),
		clamp(obj.pos.y * _params.width,0,_params.height-1));
	imageStore(_output_tex, texcoord, vec4(obj.color,1.0));
}