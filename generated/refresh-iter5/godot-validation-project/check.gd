extends SceneTree

const EXPECTED_SOCKETS := ["socket_eject", "socket_hand_l", "socket_hand_r", "socket_muzzle"]
const EXPECTED_CLIPS := ["idle", "fire", "reload", "inspect"]

func _initialize() -> void:
    var paths: Array[String] = []
    _collect("res://assets/weapons", paths)
    paths.sort()
    var result := {"godot": Engine.get_version_info().string, "glb_count": paths.size(), "assets": [], "errors": []}
    for path in paths:
        var packed = ResourceLoader.load(path, "PackedScene")
        if packed == null or not (packed is PackedScene):
            result.errors.append("Not a PackedScene: " + path)
            continue
        var root: Node = packed.instantiate()
        var stack: Array[Node] = [root]
        var animations: Array[String] = []
        var sockets: Array[String] = []
        var socket_directions: Dictionary = {}
        var materials: Array[String] = []
        var textures := 0
        var meshes := 0
        var surfaces := 0
        while not stack.is_empty():
            var node: Node = stack.pop_back()
            if node is AnimationPlayer:
                var player := node as AnimationPlayer
                for anim in player.get_animation_list():
                    if not animations.has(anim):
                        animations.append(anim)
                    var clip: Animation = player.get_animation(anim)
                    if clip == null or clip.length <= 0.0 or clip.get_track_count() == 0:
                        result.errors.append("Empty animation " + anim + ": " + path)
            if node.name.to_lower().begins_with("socket_"):
                sockets.append(str(node.name))
                if node is Node3D:
                    var socket_dir: Vector3 = -(node as Node3D).transform.basis.z.normalized()
                    socket_directions[str(node.name)] = [snappedf(socket_dir.x, 0.0001), snappedf(socket_dir.y, 0.0001), snappedf(socket_dir.z, 0.0001)]
            if node is MeshInstance3D and node.mesh != null:
                meshes += 1
                for surface in range(node.mesh.get_surface_count()):
                    surfaces += 1
                    var material = node.get_active_material(surface)
                    if material != null:
                        var mat_name: String = str(material.resource_name) if material.resource_name != "" else material.get_class()
                        if not materials.has(mat_name):
                            materials.append(mat_name)
                        if material is BaseMaterial3D and (material as BaseMaterial3D).albedo_texture != null:
                            textures += 1
            for child in node.get_children():
                stack.append(child)
        animations.sort()
        sockets.sort()
        materials.sort()
        var item = {"path": path, "animations": animations, "sockets": sockets, "socket_direction_minus_z": socket_directions, "mesh_instances": meshes, "surfaces": surfaces, "materials": materials, "textured_material_surfaces": textures}
        result.assets.append(item)
        if meshes == 0 or surfaces == 0:
            result.errors.append("No rendered mesh surfaces: " + path)
        if materials.is_empty() or textures == 0:
            result.errors.append("No textured active material: " + path)
        if sockets != EXPECTED_SOCKETS:
            result.errors.append("Socket IDs differ from exact four-ID contract: " + path + " -> " + str(sockets))
        for required in EXPECTED_CLIPS:
            if not animations.has(required):
                result.errors.append("Animation " + required + " missing: " + path)
        var expected_muzzle: Vector3 = Vector3(0, 1, 0) if path.contains("/grenade_") else Vector3(0, 0, -1)
        var expected_directions := [["socket_muzzle", expected_muzzle], ["socket_eject", Vector3(1, 0, 0)], ["socket_hand_r", Vector3(0, 0, -1)], ["socket_hand_l", Vector3(0, 0, -1)]]
        for pair in expected_directions:
            var actual := Vector3.ZERO
            if socket_directions.has(pair[0]):
                var values: Array = socket_directions[pair[0]]
                actual = Vector3(values[0], values[1], values[2])
            if actual.distance_to(pair[1]) > 0.01:
                result.errors.append("Socket orientation changed: " + str(pair[0]) + " in " + path + " actual=" + str(actual) + " expected=" + str(pair[1]))
        root.free()
    var json := JSON.stringify(result, "  ")
    print("VALIDATION_JSON=" + json)
    var f := FileAccess.open("res://godot-validation.json", FileAccess.WRITE)
    f.store_string(json)
    f.close()
    quit(1 if result.errors.size() > 0 or paths.size() != 39 else 0)

func _collect(dir_path: String, output: Array[String]) -> void:
    var dir := DirAccess.open(dir_path)
    if dir == null:
        return
    for file in dir.get_files():
        if file.to_lower().ends_with(".glb"):
            output.append(dir_path.path_join(file))
    for subdir in dir.get_directories():
        _collect(dir_path.path_join(subdir), output)
