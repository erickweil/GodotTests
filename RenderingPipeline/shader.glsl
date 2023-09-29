#[vertex]
#version 450

layout(location = 0) in vec3 pos;

layout(location = 1) out vec4 v_color;

void main() {
    gl_Position = vec4(pos.xyz, 1.0);
    v_color = vec4(pos.xyz + vec3(0.5,0.5,0.5), 1.0);
}
#[fragment]
#version 450

layout(location = 0) out vec4 outColor;

layout(location = 1) in vec4 v_color;

void main() {
    outColor = v_color;
}