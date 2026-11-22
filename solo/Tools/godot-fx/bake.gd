# Renders every job in jobs.json to raw supersampled frames.
# Run windowed (headless has no renderer):
#   godot --path solo/Tools/godot-fx --script res://bake.gd -- --raw <dir> [--only <substr>]
# Each frame is written as <raw>/<job id>__<frame>.png at (frame size * ss).
extends SceneTree

var _types := {}
var _errors := 0

func _init() -> void:
	_run.call_deferred()

func _arg(name: String, def: String) -> String:
	var a := OS.get_cmdline_user_args()
	for i in a.size() - 1:
		if a[i] == name:
			return a[i + 1]
	return def

func _run() -> void:
	var raw := _arg("--raw", "")
	var only := _arg("--only", "")
	if raw == "":
		push_error("--raw <dir> required")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(raw)
	var f := FileAccess.open("res://jobs.json", FileAccess.READ)
	if f == null:
		push_error("jobs.json missing (run jobs.py first)")
		quit(3)
		return
	var jobs: Array = JSON.parse_string(f.get_as_text())
	var count := 0
	for job in jobs:
		var id: String = job["id"]
		if only != "" and id.find(only) < 0:
			continue
		var ss: int = int(job.get("ss", 4))
		var fw: int = int(job["w"])
		var fh: int = int(job["h"])
		var n: int = int(job["frames"])
		var vp := SubViewport.new()
		vp.size = Vector2i(fw * ss, fh * ss)
		vp.transparent_bg = true
		vp.disable_3d = true
		vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(vp)
		var rect := ColorRect.new()
		rect.size = Vector2(fw * ss, fh * ss)
		var mat := ShaderMaterial.new()
		mat.shader = load("res://shaders/%s.gdshader" % job["shader"])
		rect.material = mat
		vp.add_child(rect)
		mat.set_shader_parameter("fsize", Vector2(fw, fh))
		mat.set_shader_parameter("frames", n)
		mat.set_shader_parameter("seed", float(job.get("seed", 1.0)))
		_types.clear()
		for u in mat.shader.get_shader_uniform_list():
			_types[u["name"]] = u["type"]
		var params: Dictionary = job.get("params", {})
		for k in params:
			_apply(mat, k, params[k])
		var loop: bool = bool(job.get("loop", false))
		var per: Array = job.get("frame_params", [])
		for i in n:
			var tt := 0.0
			if loop:
				tt = float(i) / float(n)
			elif n > 1:
				tt = float(i) / float(n - 1)
			mat.set_shader_parameter("frame", i)
			mat.set_shader_parameter("t", tt)
			if i < per.size():
				var fp: Dictionary = per[i]
				for k in fp:
					_apply(mat, k, fp[k])
			await RenderingServer.frame_post_draw
			await RenderingServer.frame_post_draw
			var img := vp.get_texture().get_image()
			img.save_png("%s/%s__%d.png" % [raw, id, i])
		vp.queue_free()
		count += 1
	print("baked jobs: ", count, " errors: ", _errors)
	quit(1 if _errors > 0 else 0)

func _apply(mat: ShaderMaterial, k: String, v) -> void:
	if v is Array:
		if v.size() == 2:
			mat.set_shader_parameter(k, Vector2(v[0], v[1]))
		elif v.size() == 3:
			mat.set_shader_parameter(k, Vector3(v[0], v[1], v[2]))
		else:
			mat.set_shader_parameter(k, Vector4(v[0], v[1], v[2], v[3]))
	elif not _types.has(k):
		push_error("unknown uniform " + k)
		_errors += 1
	elif _types[k] == TYPE_INT:
		mat.set_shader_parameter(k, int(v))
	else:
		mat.set_shader_parameter(k, v)
