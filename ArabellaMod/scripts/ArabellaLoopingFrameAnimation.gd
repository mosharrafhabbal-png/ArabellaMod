extends Sprite2D

## 商店、篝火与选人场景共用的逐帧播放器。
## 首帧同步显示、后续帧后台加载；可配置循环，或单次播放后缓慢缩放并停住。
## 后续替换素材时只需在对应 tscn 中修改文件夹和帧数。

@export_dir var animation_folder: String
@export_range(1, 10000, 1) var frame_count: int = 1
@export_range(1.0, 120.0, 1.0) var frames_per_second: float = 45.0
## 选人动画可以用这三个参数做“前快后慢”；默认 1.0 时仍为匀速播放。
@export_range(0.1, 4.0, 0.05) var start_speed_multiplier: float = 1.0
@export_range(0.1, 4.0, 0.05) var end_speed_multiplier: float = 1.0
@export_range(0.2, 5.0, 0.05) var slowdown_curve: float = 1.0
@export var loop_animation: bool = true
@export var shrink_after_playback: bool = false
@export_range(1.0, 120.0, 0.5) var shrink_duration_seconds: float = 30.0
@export_range(0.5, 1.0, 0.01) var final_scale_multiplier: float = 0.9

var _frames: Array[Texture2D] = []
var _frame_index: int = 0
var _elapsed_seconds: float = 0.0
var _pending_path: String = ""
var _next_frame_to_request: int = 1
var _is_ready_to_play: bool = false
var _playback_finished: bool = false
var _initial_scale: Vector2


func _ready() -> void:
	_initial_scale = scale
	var first_path := _frame_path(0)
	var first_texture := ResourceLoader.load(
		first_path,
		"Texture2D",
		ResourceLoader.CACHE_MODE_REUSE
	) as Texture2D
	if first_texture == null:
		push_warning("Arabella 场景动画缺少首帧：%s" % first_path)
		return

	_frames.append(first_texture)
	texture = first_texture
	_is_ready_to_play = true

	# 首帧立即显示，其余大图逐张放到后台线程读取，避免选中角色时主线程
	# 同步加载全部帧，也避免同时请求30张大纹理造成瞬时显存与线程压力。
	_request_next_frame()


func _process(delta: float) -> void:
	if _playback_finished:
		return

	# Keep collecting the following frames while playback is already running.
	_collect_threaded_frames()
	if not _is_ready_to_play:
		return

	_elapsed_seconds += delta

	# 游戏偶尔掉帧时追上正确的动画时间，不让播放速度永久变慢。
	while not _frames.is_empty():
		var seconds_per_frame := _seconds_for_current_frame()
		if _elapsed_seconds < seconds_per_frame:
			break
		_elapsed_seconds -= seconds_per_frame
		if _frame_index >= _frames.size() - 1:
			# The next frame is still loading. Hold the current frame without
			# accumulating a large backlog that would make loaded frames flash by.
			if not _pending_path.is_empty() or _next_frame_to_request < frame_count:
				_elapsed_seconds = minf(_elapsed_seconds, seconds_per_frame)
				break
			if loop_animation:
				_frame_index = 0
				texture = _frames[_frame_index]
				continue
			_finish_playback()
			break

		_frame_index += 1
		texture = _frames[_frame_index]


func _collect_threaded_frames() -> void:
	if _pending_path.is_empty():
		_is_ready_to_play = true
		return

	var status := ResourceLoader.load_threaded_get_status(_pending_path)
	if status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		return
	if status != ResourceLoader.THREAD_LOAD_LOADED:
		push_warning("Arabella 场景动画后台加载失败：%s" % _pending_path)
		_pending_path = ""
		_request_next_frame()
		return

	var frame_texture := ResourceLoader.load_threaded_get(_pending_path) as Texture2D
	_pending_path = ""
	if frame_texture != null:
		_frames.append(frame_texture)
	_request_next_frame()


func _request_next_frame() -> void:
	if _next_frame_to_request >= frame_count:
		_is_ready_to_play = _pending_path.is_empty()
		return

	var texture_path := _frame_path(_next_frame_to_request)
	_next_frame_to_request += 1
	var error := ResourceLoader.load_threaded_request(
		texture_path,
		"Texture2D",
		true,
		ResourceLoader.CACHE_MODE_REUSE
	)
	if error != OK:
		push_warning("Arabella 场景动画无法请求帧：%s" % texture_path)
		_request_next_frame()
		return
	_pending_path = texture_path


func _finish_playback() -> void:
	_playback_finished = true
	_elapsed_seconds = 0.0
	if not shrink_after_playback:
		return

	var target_scale := _initial_scale * final_scale_multiplier
	var tween := create_tween()
	tween.set_trans(Tween.TRANS_SINE)
	tween.set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(self, "scale", target_scale, shrink_duration_seconds)


func _frame_path(frame_index: int) -> String:
	return "%s/frame_30_%06d.png" % [animation_folder, frame_index]


func _seconds_for_current_frame() -> float:
	if _frames.size() <= 1:
		return 1.0 / frames_per_second

	var progress := float(_frame_index) / float(_frames.size() - 1)
	var curved_progress := pow(progress, slowdown_curve)
	var speed_multiplier := lerpf(
		start_speed_multiplier,
		end_speed_multiplier,
		curved_progress
	)
	return 1.0 / (frames_per_second * maxf(speed_multiplier, 0.1))


func get_configured_frame_count() -> int:
	return frame_count
