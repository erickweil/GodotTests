shader_type spatial;
//render_mode unshaded, cull_disabled;

uniform sampler2D image : source_color;

varying vec3 _color;

void vertex() {
	//float vid = float(VERTEX_ID);
	//vec2 coords = vec2((mod(vid,512.0))/512.0,(vid / 512.0)/512.0);
	//float pixel = texture(image,coords).r;
	int id = VERTEX_ID;
	ivec2 size = textureSize(image, 0);

	int v_id = id * 2 + 0;
	vec3 v = texelFetch(image, ivec2(v_id % size.x,v_id / size.x),0).rgb;

	int n_id = id * 2 + 1;
	vec3 n = texelFetch(image, ivec2(n_id % size.x,n_id / size.x),0).rgb;

	VERTEX = v;
	NORMAL = n;
	_color = vec3(UV,1.0);
}

void fragment() {
	// Place fragment code here.
	//float pixel = mod(texture(image,UV.xy).r,50.0)/50.0;

	float dist = min(min(abs(_color.x - 0.0),abs(_color.x - 1.0)),min(abs(_color.y - 0.0),abs(_color.y - 1.0)));
	dist = clamp((dist * dist)*50.0,0.15,1.0);

	ALBEDO = vec3(dist,dist*0.5 + 0.5,dist);
}
