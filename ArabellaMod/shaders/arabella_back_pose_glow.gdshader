shader_type canvas_item;

uniform vec4 glow_color : source_color = vec4(0.95, 0.08, 0.16, 0.96);
uniform float glow_size : hint_range(0.0, 14.0, 0.1) = 8.0;
uniform float glow_opacity : hint_range(0.0, 1.0, 0.01) = 0.55;

varying vec4 node_modulate;

void vertex() {
	node_modulate = COLOR;
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	vec2 radius = TEXTURE_PIXEL_SIZE * glow_size;
	vec2 inner = radius * 0.40;
	vec2 diagonal = radius * 0.70710678;
	float halo = 0.0;

	halo += texture(TEXTURE, UV + vec2( radius.x, 0.0)).a;
	halo += texture(TEXTURE, UV + vec2(-radius.x, 0.0)).a;
	halo += texture(TEXTURE, UV + vec2(0.0,  radius.y)).a;
	halo += texture(TEXTURE, UV + vec2(0.0, -radius.y)).a;
	halo += texture(TEXTURE, UV + vec2( diagonal.x,  diagonal.y)).a;
	halo += texture(TEXTURE, UV + vec2(-diagonal.x,  diagonal.y)).a;
	halo += texture(TEXTURE, UV + vec2( diagonal.x, -diagonal.y)).a;
	halo += texture(TEXTURE, UV + vec2(-diagonal.x, -diagonal.y)).a;

	halo += texture(TEXTURE, UV + vec2( inner.x, 0.0)).a * 1.5;
	halo += texture(TEXTURE, UV + vec2(-inner.x, 0.0)).a * 1.5;
	halo += texture(TEXTURE, UV + vec2(0.0,  inner.y)).a * 1.5;
	halo += texture(TEXTURE, UV + vec2(0.0, -inner.y)).a * 1.5;
	halo = clamp(halo / 14.0 * (1.0 - source.a) * 3.0, 0.0, 1.0);

	float glow_alpha = pow(halo, 0.85) * glow_opacity * glow_color.a;
	vec3 final_color = mix(glow_color.rgb, source.rgb, source.a);
	COLOR = vec4(final_color, max(source.a, glow_alpha)) * node_modulate;
}
