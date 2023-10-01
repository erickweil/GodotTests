#[vertex]
#version 450

layout(location = 0) in vec3 pos;
layout(location = 1) in vec4 color;

layout(location = 2) out vec4 v_color;

void main() {
    gl_Position = vec4(pos.xy, 1.0, 1.0);

    //gl_Position = vec4(vert.xy, 1.0, 1.0);


    v_color = color.yxzw;
}
#[fragment]
#version 450

layout(location = 0) out vec4 outColor;

layout(location = 2) in vec4 v_color;

void main() {
    outColor = v_color;
}