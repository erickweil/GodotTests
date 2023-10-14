#[compute]
#version 450

// Multiply each component to calculate how many invocations per workgroup
// 8x1x1 = 8 local invocations per workgroup.
layout(local_size_x = 8, local_size_y = 8, local_size_z = 8) in;

// https://vkguide.dev/docs/chapter-4/descriptors/
layout(rgba32f, set = 0, binding = 0) uniform restrict writeonly image2D _output_tex;

// https://stackoverflow.com/questions/56682438/opengl-glsl-atomic-counter-in-vulkan
// https://www.intel.com/content/www/us/en/developer/articles/technical/opengl-performance-tips-atomic-counter-buffers-versus-shader-storage-buffer-objects.html
layout(std430, set = 0, binding = 1) restrict buffer CounterBuffer {
    uint count;
}
_counter_buff;

// Our push PushConstant
layout(push_constant, std430) uniform Params {
	uint texWidth;
	uint texHeight;
	float cubeSize;
	uint maxQuads;
	float offx;
	float offy;
	float offz;
	float pad1;
} _params;

void storeVertex(vec3 v, vec3 n, uint index) {
	uint v_index = index * 2 + 0;
	uint n_index = index * 2 + 1;

	ivec2 v_id = ivec2(v_index % _params.texWidth, v_index / _params.texWidth);
	imageStore(_output_tex, v_id, vec4(v.x,v.y,v.z,1));

	ivec2 n_id = ivec2(n_index % _params.texWidth, n_index / _params.texWidth);
	imageStore(_output_tex, n_id, vec4(n.x,n.y,n.z,1));
}

// void addTriangle(vec3 pos0, vec3 pos1, vec3 pos2) {
// 	// atomicAdd performs an atomic addition of data to the contents of mem and returns the original contents of mem from before the addition occurred.
// 	uint triangleIndex = atomicAdd(_counter_buff.count, 1);

// 	// Garantir nao dar overflow
// 	if(triangleIndex > _params.maxTriangles) {
// 		return;
// 	}

// 	storeV3(pos0, triangleIndex*3 + 0);
// 	storeV3(pos1, triangleIndex*3 + 1);
// 	storeV3(pos2, triangleIndex*3 + 2);
// }

void addQuad(vec3 pos0, vec3 pos1, vec3 pos2, vec3 pos3, vec3 normal) {
	//addTriangle(pos0,pos1,pos2);
	//addTriangle(pos2,pos3,pos0);

	// atomicAdd performs an atomic addition of data to the contents of mem and returns the original contents of mem from before the addition occurred.
	uint quadIndex = atomicAdd(_counter_buff.count, 1);

	if(quadIndex > _params.maxQuads) {
 		return;
 	}

 	storeVertex(pos0, normal, quadIndex*4 + 0);
 	storeVertex(pos1, normal, quadIndex*4 + 1);
 	storeVertex(pos2, normal, quadIndex*4 + 2);
	storeVertex(pos3, normal, quadIndex*4 + 3);
}

// https://iquilezles.org/articles/distfunctions/
float sdTorus(vec3 p, vec2 t) {
	vec2 q = vec2(length(p.xz) - t.x, p.y);
	return length(q) - t.y;
}

float calcDe(vec3 p) {
	//return sdTorus(p - vec3(_params.offx,_params.offy,_params.offz), vec2(0.32,0.10));
	return sdTorus(p, vec2(0.32 - _params.offx,0.10 - _params.offz));
}

// The code we want to execute in each invocation
void main() {
	uvec3 id = gl_GlobalInvocationID;
	// map from int indices to float [0,1]
	vec3 pos = ((vec3(id.x,id.y,id.z) + vec3(0.5,0.5,0.5)) / _params.cubeSize) - vec3(0.5,0.5,0.5);

	// Distance Estimator of a Torus
	float dist = calcDe(pos);

	// only output quads if center dist is less than zero
	if(dist <= 0) {
		float off = 0.5 / _params.cubeSize;
		float off2 = off * 2.0;

		// y+
		if(calcDe(pos + vec3(0,off2,0)) > 0.0) {
		addQuad(
			vec3(-off, off,-off) + pos,
			vec3( off, off,-off) + pos,
			vec3( off, off, off) + pos,
			vec3(-off, off, off) + pos, vec3(0,1,0));
		}

		// y-
		if(calcDe(pos + vec3(0,-off2,0)) > 0.0) {
		addQuad(
			vec3(-off,-off, off) + pos,
			vec3( off,-off, off) + pos,
			vec3( off,-off,-off) + pos,
			vec3(-off,-off,-off) + pos, vec3(0,-1,0));
		}

		// x+
		if(calcDe(pos + vec3(off2,0,0)) > 0.0) {
		addQuad(
			vec3( off, off, off) + pos,
			vec3( off, off,-off) + pos,
			vec3( off,-off,-off) + pos, 
			vec3( off,-off, off) + pos,
			vec3(1,0,0));
		}

		// x-
		if(calcDe(pos + vec3(-off2,0,0)) > 0.0) {
		addQuad(
			vec3(-off, off,-off) + pos,
			vec3(-off, off, off) + pos,
			vec3(-off,-off, off) + pos, 
			vec3(-off,-off,-off) + pos,
			vec3(-1,0,0));
		}

		// z+
		if(calcDe(pos + vec3(0,0,off2)) > 0.0) {
		addQuad(
			vec3( off,-off, off) + pos,
			vec3(-off,-off, off) + pos, 
			vec3(-off, off, off) + pos,
			vec3( off, off, off) + pos,
			vec3(0,0,1));
		}

		// z-
		if(calcDe(pos + vec3(0,0,-off2)) > 0.0) {
		addQuad(
			vec3( off, off,-off) + pos,
			vec3(-off, off,-off) + pos, 
			vec3(-off,-off,-off) + pos,
			vec3( off,-off,-off) + pos,
			vec3(0,0,-1));
		}
	}
}