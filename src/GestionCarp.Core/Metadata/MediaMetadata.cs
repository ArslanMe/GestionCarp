namespace GestionCarp.Core.Metadata;

/// <summary>Datos internos de un archivo multimedia. Lo que no se conoce queda en null.</summary>
public sealed record MediaMetadata
{
    public static readonly MediaMetadata Empty = new();

    /// <summary>Fecha en que se tomó la foto (EXIF) o se grabó el vídeo/audio.</summary>
    public DateTime? DateTaken { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? Genre { get; init; }
    public int? Track { get; init; }
    public int? Year { get; init; }
    public TimeSpan? Duration { get; init; }

    /// <summary>"Canon EOS 80D" sin repetir la marca si el modelo ya la incluye.</summary>
    public string? Camera
    {
        get
        {
            string make = (CameraMake ?? "").Trim();
            string model = (CameraModel ?? "").Trim();
            if (model.Length == 0) return make.Length == 0 ? null : make;
            if (make.Length == 0 || model.StartsWith(make.Split(' ')[0], StringComparison.OrdinalIgnoreCase)) return model;
            return make + " " + model;
        }
    }
}
