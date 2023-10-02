#[compute]
#version 450

// to prevent GPU crash by never having a infinite loop
#define MAX_ITERATIONS 4096*2

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
// https://github.com/KhronosGroup/Vulkan-Samples/blob/2ce8856b7e20c28467f0b2f86429d3f54d7821a5/shaders/compute_nbody/particle_calculate.comp
void main() {
	// Grab the current pixel's position from the ID of this specific invocation ("thread").
	// https://registry.khronos.org/OpenGL-Refpages/gl4/html/gl_GlobalInvocationID.xhtml
    uint id = gl_GlobalInvocationID.x;
	GObj obj = _object_buffer.data[id];
	vec3 pos = obj.pos;
	vec3 vel = obj.vel;
	float mass = obj.mass;

	float G = 0.0001;
	vec3 accel = vec3(0,0,0);
	for(int i = 0; i < _params.nObjects; i++) {
		if(i >= MAX_ITERATIONS) break;
		
		vec3 otherPos;
		float otherMass;
		if(i != id) {
			GObj other = _object_buffer.data[i];
			otherPos = other.pos;
			otherMass = other.mass;
		} else {
			otherPos = vec3(_params.mousex,_params.mousey,_params.mousez); 
			otherMass = 1000.0;
		}

		// Calculate the distance between the two objects
		vec3 delta = otherPos - pos;
		float sqrDist = dot(delta,delta);
		// Calculate the gravitational force magnitude between the two objects
		float forceMagnitude = (G * mass * otherMass) / max(0.01,sqrDist);
		vec3 forceDirection = normalize(delta);

		// Calculate the gravitational force vector
		vec3 gravitationalForce = forceMagnitude * forceDirection;

		accel += gravitationalForce;
	}

	obj.ax = accel.x;
	obj.ay = accel.y;
	obj.az = accel.z;

    _object_buffer.data[id] = obj;
}