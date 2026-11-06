using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// Builds Assets/Scenes/Dining.unity from the CAD GLBs (CAD/GLB, copied into Assets/CAD).
/// Menu: BlockTower > Build Dining Scene. Headless:
/// Unity -batchmode -projectPath <this project> -executeMethod SceneBuilder.Build -quit
public static class SceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Dining.unity";
    const float TableHeight = 0.73f;  // CAD/src/table.py
    const float ChairX = 0.32f;       // CAD/src/dining_set.py: two chairs evenly between the legs
    const float ChairZ = 0.00824f;    // tucked until the backrest meets the table edge
    const float BulbBelowCeiling = 0.71f;  // CAD/src/pendant_light.py: globe centre
    const float RoomHalfLength = 2.5f;     // CAD/src/room.py: far wall face (CAD -Y lands at Unity +Z)
    const float SideboardHeight = 0.76f;   // CAD/src/sideboard.py

    [MenuItem("BlockTower/Build Dining Scene")]
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        // Textures: Poly Haven (CC0), real sizes from their metadata. Hex = the CAD part colour it replaces
        var oak = Wood("oak_veneer_01", 1.83f, Palette.RawWood, 0.35f);
        var walnut = Wood("american_walnut_veneer", 1f, Palette.OiledWalnut, 0.4f);
        var trim = Plain("Trim", Palette.Trim, 0.55f);

        // Room 4 x 5 m, ceiling 2.4 m (CAD/src/room.py): door behind the start, pendant over the table
        Dress(Place("room", Vector3.zero, 0f),
            ("#9A7650", Wood("laminate_floor_02", 1.7f, Palette.RawWood, 0.45f)),
            ("#E9E4DA", Wallpaper()),  // walls and ceiling are one mesh (same CAD colour); papered ceilings are usual here
            ("#F4F2EE", trim));
        // sideboard and picture against the far wall (CAD/src/sideboard.py, wall_decor.py), facing the table
        Dress(Place("sideboard", new Vector3(0f, 0f, RoomHalfLength), 180f), ("#5A3B26", walnut));
        // Assets/CAD/wall_decor.glb is CAD/GLB/wall_decor.glb with the print painted in Blender/Props.blend
        Place("wall_decor", new Vector3(0f, 0f, RoomHalfLength), 180f);
        // stands in for bounce: the pale walls light the tower's sides, which the pendant only grazes
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Palette.AmbientSky;
        RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
        RenderSettings.ambientGroundColor = Palette.AmbientGround;
        var warm = Palette.WarmLight;
        var pendant = Place("pendant_light", new Vector3(0f, 2.4f, 0f), 0f);
        var bulbPos = new Vector3(0f, 2.4f - BulbBelowCeiling, 0f);
        // GLB part names don't survive import: the bulb is the smallest part around the globe centre
        MeshRenderer bulb = null;
        foreach (var r in pendant.GetComponentsInChildren<MeshRenderer>())
            if (r.bounds.Contains(bulbPos) && (bulb == null || r.bounds.size.x < bulb.bounds.size.x))
                bulb = r;
        bulb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;  // the lights sit inside it
        var glow = new Material(Shader.Find("Standard")) { color = warm };
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", warm * 3f);
        glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;  // the default (emissive is black) drops _EMISSION on save
        AssetDatabase.CreateAsset(glow, "Assets/CAD/BulbGlow.mat");
        bulb.sharedMaterial = glow;
        var lamp = new GameObject("Pendant Spot").AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.transform.SetPositionAndRotation(bulbPos, Quaternion.Euler(90f, 0f, 0f));
        lamp.spotAngle = 110f;       // shade rim: atan(190 / 140) = 54 deg half angle
        lamp.innerSpotAngle = 70f;
        lamp.range = 4f;
        lamp.intensity = 2.2f;
        lamp.color = warm;
        lamp.shadows = LightShadows.Soft;
        lamp.shadowNearPlane = 0.05f;
        // what spills from the shade's top and bounces off the table and walls
        var spill = new GameObject("Pendant Spill").AddComponent<Light>();
        spill.type = LightType.Point;
        spill.transform.position = bulbPos;
        spill.range = 5f;
        spill.intensity = 0.6f;
        spill.color = warm;
        // short-range: lights the shade's inside and the socket so the lamp reads as on from the seats
        var halo = new GameObject("Bulb Halo").AddComponent<Light>();
        halo.type = LightType.Point;
        halo.transform.position = bulbPos;
        halo.range = 0.45f;
        halo.intensity = 3f;
        halo.color = warm;

        Props();

        DressAll(Place("table", Vector3.zero, 0f), oak);
        foreach (float x in new[] { -ChairX, ChairX })
            foreach (int side in new[] { -1, 1 })
            {
                var chair = Place("chair", new Vector3(x, 0f, -side * ChairZ), side > 0 ? 0f : 180f);
                DressAll(chair, oak);
                chair.AddComponent<Rigidbody>().isKinematic = true;  // slides by script, pushes blocks it bumps
                chair.AddComponent<DiningChair>();
            }

        var tower = new GameObject("Tower").AddComponent<TowerSpawner>();
        tower.transform.position = new Vector3(0f, TableHeight, 0f);
        tower.blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CAD/tower_block.glb");
        // levels alternate pale maple (oak lifted toward it, finer grain) and dark walnut
        tower.blockMaterial = Wood("oak_veneer_01", 0.6f, Palette.MapleBlock, 0.25f, "BlockWood");
        tower.blockMaterialOdd = BlockWalnut();

        var player = new GameObject("Player");
        // just inside the door (CAD/src/room.py: north wall, x = +1.2 m here), facing the table
        var door = new Vector3(1.2f, 0f, -RoomHalfLength);
        // 0.8 in, clear of the plant under the clock
        player.transform.SetPositionAndRotation(door + new Vector3(0f, 0f, 0.8f), Quaternion.LookRotation(-door - new Vector3(0f, 0f, 0.8f)));
        var capsule = player.AddComponent<CharacterController>();
        capsule.radius = 0.34f;
        capsule.height = 1.92f;
        capsule.center = new Vector3(0f, 0.96f, 0f);
        capsule.skinWidth = 0.02f;
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.SetParent(player.transform, false);
        cam.nearClipPlane = 0.01f;  // the tower is a hand's length away
        cam.gameObject.AddComponent<AudioListener>();
        PostFx(cam);
        var pc = player.AddComponent<PlayerCharacter>();
        pc.view = cam;
        pc.handModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hand/RightHand.fbx");
        pc.leftHandModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hand/LeftHand.fbx");  // a headset's tracked left hand

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("SceneBuilder: saved " + ScenePath);
    }

    /// Triplanar textured material (Assets/Resources/Triplanar.shader), saved under Assets/Materials
    static Material Wood(string texture, float size, Color tint, float smoothness, string name = null)
    {
        var m = new Material(Shader.Find("BlockTower/Triplanar"));
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texture}_diff.jpg"));
        m.SetFloat("_Size", size);
        m.SetColor("_Color", tint);
        m.SetFloat("_Glossiness", smoothness);
        return Save(m, name ?? texture);
    }

    /// The tower's odd levels: oiled walnut like the sideboard, at the blocks' finer grain
    public static Material BlockWalnut() => Wood("american_walnut_veneer", 0.6f, Palette.OiledWalnut, 0.3f, "BlockWalnut");

    /// Warm-white embossed silk wallpaper (ambientCG Wallpaper001A, CC0): the grey plaster made the room dull
    public static Material Wallpaper() => Wood("wallpaper_001a", 1.2f, Palette.Wallpaper, 0.15f);

    /// Corner shading, a glow round the bulb and a light grade, so the room reads as a game rather than a raw render
    public static void PostFx(Camera cam)
    {
        cam.allowHDR = true;  // bloom needs the bulb brighter than white
        var layer = cam.gameObject.AddComponent<PostProcessLayer>();
        layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset"));
        layer.volumeLayer = ~0;
        layer.volumeTrigger = cam.transform;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        var ao = profile.AddSettings<AmbientOcclusion>();
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(0.6f);
        var bloom = profile.AddSettings<Bloom>();
        bloom.intensity.Override(1.2f);
        bloom.threshold.Override(1.1f);
        bloom.softKnee.Override(0.6f);
        var grade = profile.AddSettings<ColorGrading>();
        grade.tonemapper.Override(Tonemapper.Neutral);  // ACES darkened the ambient-lit walls
        grade.postExposure.Override(0.6f);
        grade.saturation.Override(10f);
        grade.contrast.Override(8f);
        grade.temperature.Override(3f);
        profile.AddSettings<Vignette>().intensity.Override(0.25f);
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(profile, "Assets/Materials/RoomPostFx.asset");
        foreach (var settings in profile.settings)
        {
            settings.enabled.Override(true);
            AssetDatabase.AddObjectToAsset(settings, profile);  // the effects are sub-assets or they're lost on reload
        }
        AssetDatabase.SaveAssets();
        var volume = new GameObject("Post FX").AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static Material Plain(string name, Color color, float smoothness)
    {
        var m = new Material(Shader.Find("Standard")) { color = color };
        m.SetFloat("_Glossiness", smoothness);
        return Save(m, name);
    }

    static Material Save(Material m, string name)
    {
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(m, $"Assets/Materials/{name}.mat");
        return m;
    }

    /// Room dressing in Assets/Props, all our own: shapes in CAD/src (<name>.py), looks in Blender/Props.blend.
    /// Kept to the walls and corners, clear of the walk round the table (chairs on the +-Z sides, door at x +1.2 on the -Z wall). Menu run on the open scene: BlockTower > Add Props
    [MenuItem("BlockTower/Add Props")]
    public static void Props()
    {
        const float wallX = 2f;
        Prop("picture_frame_a", new Vector3(wallX, 1.45f, 0f), -90f, Vector3.right);
        Prop("wall_clock", new Vector3(-1f, 1.9f, -RoomHalfLength), 0f, Vector3.back);
        Prop("picture_frame_b", new Vector3(-wallX, 1.45f, 0f), 90f, Vector3.left);
        // the far right corner beside the sideboard; the peace lily spreads 1.09 m, so its centre 0.56 off both walls
        Prop("potted_plant", new Vector3(wallX - 0.56f, 0f, RoomHalfLength - 0.56f), 0f, Vector3.down, solid: true);
        // under the clock by the door: a snake plant (CAD/src/snake_plant.py), slim, so a different plant from the corner
        Prop("snake_plant", new Vector3(-1f, 0f, -RoomHalfLength + 0.2f), 0f, Vector3.down, solid: true);
        // on the sideboard top, clear of the vase at x +0.42 (wall_decor.py)
        Prop("bowl", new Vector3(-0.45f, SideboardHeight, RoomHalfLength - 0.22f), 0f, Vector3.down);
        Prop("succulent", new Vector3(0.12f, SideboardHeight, RoomHalfLength - 0.22f), 0f, Vector3.down);
    }

    /// Places a prop so the face of its bounds toward `against` (down: its base; a wall's direction: its back) sits
    /// at `at`; solid ones get a box the players walk round
    static void Prop(string name, Vector3 at, float yaw, Vector3 against, bool solid = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Props/{name}.glb");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);
        var shift = at - (b.center + Vector3.Scale(b.extents, against));
        go.transform.position += shift;
        if (!solid)
            return;
        var box = new GameObject("Blocker").AddComponent<BoxCollider>();
        box.transform.SetParent(go.transform, true);
        box.transform.position = b.center + shift;
        box.size = b.size;
    }

    /// Swaps each part's imported material for the one mapped to its CAD colour; unmapped parts keep theirs
    static void Dress(GameObject go, params (string hex, Material material)[] map)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var c = r.sharedMaterial.color;
            foreach (var (hex, material) in map)
            {
                // glTFast hands back the CAD colour as sRGB
                ColorUtility.TryParseHtmlString(hex, out var cad);
                if (Near(c, cad))
                {
                    r.sharedMaterial = material;
                    break;
                }
            }
        }
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

    static void DressAll(GameObject go, Material material)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterial = material;
    }

    /// Instances a CAD GLB with exact mesh collision on every mesh
    static GameObject Place(string name, Vector3 position, float yaw)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CAD/{name}.glb");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        foreach (var mesh in go.GetComponentsInChildren<MeshFilter>())
            mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        return go;
    }
}
