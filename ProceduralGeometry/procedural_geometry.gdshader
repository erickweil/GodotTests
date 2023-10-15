shader_type spatial;
//render_mode unshaded, cull_disabled;

uniform sampler2D image : source_color;

varying vec3 _color;

void vertex() {
	int id = VERTEX_ID;
	ivec2 size = textureSize(image, 0);
	
	// Testing
	_color = VERTEX;
	
	// using texelFetch to prevent sampling and rounding errors when using normalized coordinates
	// so the floats returned are the exact values stored in the texture
	// Vertex
	int v_id = id * 2 + 0;
	VERTEX = texelFetch(image, ivec2(v_id % size.x,v_id / size.x),0).rgb;

	// Normal
	int n_id = id * 2 + 1;
	NORMAL = texelFetch(image, ivec2(n_id % size.x,n_id / size.x),0).rgb;
}

void fragment() {
	//ALBEDO = vec3(1,1,1);
	ALBEDO = _color;
}
