shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D image : source_color;

varying vec3 _color;

void vertex() {
	//float vid = float(VERTEX_ID);
	//vec2 coords = vec2((mod(vid,512.0))/512.0,(vid / 512.0)/512.0);
	//float pixel = texture(image,coords).r;
	int id = VERTEX_ID;
	ivec2 size = textureSize(image, 0);
	//ivec2 size = ivec2(32,32);
	ivec2 tex_id = ivec2(id % size.x,id / size.x);
	vec3 v = texelFetch(image,tex_id,0).rgb;

	//if(length(v) > 0.0)
	//{ 
		VERTEX = v;
	//}
	_color = v;
}

void fragment() {
	// Place fragment code here.
	//float pixel = mod(texture(image,UV.xy).r,50.0)/50.0;

	ALBEDO = _color;
}
