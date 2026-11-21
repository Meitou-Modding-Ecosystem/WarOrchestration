using System.Text;
using Meitou.Data.Fcs;

namespace StructureImport;

/// <summary>
/// Imports every model of a mod's <c>Structures</c> folder into the mod's deployed folder: the files under <c>structures/</c> and the records in
/// <c>&lt;Mod&gt;.mod</c>.
/// </summary>
public static class Importer
{
    /// <summary>
    /// The folder of the deployed mod the files go to; the game finds them by bare name wherever they are.
    /// </summary>
    public const string AssetFolder = "structures";

    /// <summary>
    /// Above this many triangles a model is reported as heavy (the game's largest buildings have about this many).
    /// </summary>
    const int HeavyTriangles = 150_000;

    /// <summary>
    /// Imports the <c>.glb</c> / <c>.gltf</c> files of <paramref name="models"/> into the mod folder <paramref name="mod"/> (its name is the
    /// mod's), writing <see cref="StructureIds.FileName"/> beside the models. Returns one line per model for the log.
    /// </summary>
    public static List<string> ImportFolder(string models, string mod)
    {
        mod = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mod));
        string modName = Path.GetFileName(mod);
        string assets = Path.Combine(mod, AssetFolder);
        if (Directory.Exists(assets)) Directory.Delete(assets, recursive: true);
        Directory.CreateDirectory(assets);

        var ids = StructureIds.Load(models);
        var records = new List<FcsRecord>();
        var log = new List<string>();
        foreach (string model in ModelFiles(models))
        {
            var block = new RecordIds(ids.BlockOf(Path.GetFileNameWithoutExtension(model)), modName + ".mod");
            var (structureRecords, line) = ImportNamed(model, assets, FilePrefix(modName, model), block);
            records.AddRange(structureRecords);
            log.Add(line);
        }

        ids.Save(models);
        ModFileMerger.Merge(Path.Combine(mod, modName + ".mod"), records);
        return log;
    }

    /// <summary>
    /// The model files of a folder, in name order.
    /// </summary>
    public static IEnumerable<string> ModelFiles(string folder) =>
        Directory.EnumerateFiles(folder)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".glb" or ".gltf")
            .Order(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <see cref="ImportModel"/>, a failure's message naming the model (a glTF reader's own messages don't).
    /// </summary>
    static (List<FcsRecord> Records, string Line) ImportNamed(string model, string assets, string prefix, RecordIds ids)
    {
        try
        {
            return ImportModel(model, assets, prefix, ids);
        }
        catch (Exception e) when (!e.Message.Contains(model, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{model}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Imports one model: its files into <paramref name="assets"/> (named from <paramref name="prefix"/>), its records with ids from
    /// <paramref name="ids"/>, and a log line.
    /// </summary>
    static (List<FcsRecord> Records, string Line) ImportModel(string model, string assets, string prefix, RecordIds ids)
    {
        var spec = StructureSpec.ForModel(model);
        var meshes = GlbScene.Load(model);
        if (meshes.Count > RecordIds.MaxParts)
            throw new InvalidDataException($"{model}: {meshes.Count} materials, at most {RecordIds.MaxParts}; merge some in Blender");

        MeshMath.Place(meshes, MeshMath.Scale(meshes, spec.Height), spec.Yaw, spec.Sink * MeshMath.UnitsPerMetre);
        var (_, max) = MeshMath.Bounds(meshes);
        var parts = meshes.Select((mesh, i) => WritePart(mesh, i, meshes, spec, assets, prefix)).ToList();

        string name = spec.DisplayName(model);
        var records = StructureRecords.Build(spec, name, max.Y, parts, ids);
        if (spec.Research != "none") records.Add(StructureRecords.Unlock(Research(spec.Research), ids.Building));

        return (records, Summary(model, name, meshes, max.Y, spec));
    }

    /// <summary>
    /// Writes part <paramref name="index"/>'s mesh and textures; the first part also carries the whole model's collision.
    /// </summary>
    static PartFiles WritePart(SurfaceMesh mesh, int index, List<SurfaceMesh> all, StructureSpec spec, string assets, string prefix)
    {
        string stem = all.Count == 1 ? prefix : $"{prefix}_{index + 1}";
        OgreMeshWriter.Write(mesh, MeshMath.Tangents(mesh), Path.Combine(assets, stem + ".mesh"));
        var textures = TextureBaker.Bake(mesh.Look, spec.TextureSize);
        var (diffuse, normal, metal) = TextureBaker.Write(textures, assets, stem);
        string collision = index == 0 ? WriteCollision(all, spec, Path.Combine(assets, prefix + "_col.xml")) : "";
        int? threshold = mesh.Look.AlphaCutoff is { } cut ? (int)MathF.Round(cut * 255) : null;
        return new PartFiles(stem + ".mesh", collision, diffuse, normal, metal, threshold, mesh.Look.Name);
    }

    /// <summary>
    /// Writes the model's collision as <paramref name="spec"/> asks; returns its file name, "" for none.
    /// </summary>
    static string WriteCollision(List<SurfaceMesh> meshes, StructureSpec spec, string path)
    {
        switch (spec.Collision)
        {
            case "none":
                return "";
            case "box":
                var (min, max) = MeshMath.Bounds(meshes);
                CollisionWriter.WriteBox(min, max, path);
                break;
            default:
                CollisionWriter.WriteMesh(Simplifier.Reduce(Simplifier.Weld(meshes), spec.CollisionTriangles), path);
                break;
        }

        return Path.GetFileName(path);
    }

    /// <summary>
    /// The research string id a setting names.
    /// </summary>
    static string Research(string setting) => setting == "start" ? StructureRecords.DefaultStart : setting;

    /// <summary>
    /// The start of every file name of a model: mod and model names, lower case, letters and digits only (bare names are shared by every mod).
    /// </summary>
    static string FilePrefix(string modName, string model)
    {
        static string Clean(string s) => new([.. s.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
        return $"{Clean(modName)}_{Clean(Path.GetFileNameWithoutExtension(model))}";
    }

    /// <summary>
    /// The log line of an imported model.
    /// </summary>
    static string Summary(string model, string name, List<SurfaceMesh> meshes, float height, StructureSpec spec)
    {
        int triangles = meshes.Sum(m => m.Indices.Count / 3);
        var line = new StringBuilder($"{Path.GetFileName(model)} -> \"{name}\": {meshes.Count} part(s), {triangles} triangles, ");
        line.Append($"{height / MeshMath.UnitsPerMetre:0.##} m tall, collision {spec.Collision}, unlocked by {spec.Research}");
        if (triangles > HeavyTriangles) line.Append($" (heavy: consider reducing it below {HeavyTriangles} triangles in Meshy or Blender)");
        return line.ToString();
    }
}
