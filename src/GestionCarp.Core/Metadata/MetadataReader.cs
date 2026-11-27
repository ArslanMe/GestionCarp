using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace GestionCarp.Core.Metadata;

/// <summary>
/// Lee metadatos sin librerías externas: EXIF de JPEG, PNG, ID3 de MP3, FLAC y MP4/M4A/MOV.
/// Nunca lanza excepciones: si no puede leer algo, lo deja vacío.
/// </summary>
public static class MetadataReader
{
    private static readonly ConcurrentDictionary<(string, long, DateTime), MediaMetadata> Cache = new();

    public static MediaMetadata Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return MediaMetadata.Empty;
            var key = (path, info.Length, info.LastWriteTimeUtc);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var result = ReadUncached(path);
            if (Cache.Count > 20_000) Cache.Clear();
            Cache[key] = result;
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return MediaMetadata.Empty;
        }
    }

    private static MediaMetadata ReadUncached(string path)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".jpe" => ReadJpeg(path),
                ".png" => ReadPng(path),
                ".mp3" => ReadMp3(path),
                ".flac" => ReadFlac(path),
                ".mp4" or ".m4a" or ".m4v" or ".mov" or ".m4b" or ".3gp" => ReadMp4(path),
                _ => MediaMetadata.Empty,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or IndexOutOfRangeException or EndOfStreamException or OverflowException or FormatException)
        {
            return MediaMetadata.Empty;
        }
    }

    // =====================================================================
    // JPEG / EXIF
    // =====================================================================

    private static MediaMetadata ReadJpeg(string path)
    {
        using var fs = File.OpenRead(path);
        var r = new BinaryReader(fs);
        if (r.ReadByte() != 0xFF || r.ReadByte() != 0xD8) return MediaMetadata.Empty;

        MediaMetadata meta = MediaMetadata.Empty;
        int? sofWidth = null, sofHeight = null;
        while (fs.Position < fs.Length - 4)
        {
            if (r.ReadByte() != 0xFF) break;
            byte marker = r.ReadByte();
            while (marker == 0xFF) marker = r.ReadByte();
            if (marker == 0xD9 || marker == 0xDA) break; // fin o comienzo de la imagen
            int length = (r.ReadByte() << 8) | r.ReadByte();
            if (length < 2) break;
            long next = fs.Position + length - 2;

            if (marker == 0xE1)
            {
                var data = r.ReadBytes(length - 2);
                if (data.Length > 14 && Encoding.ASCII.GetString(data, 0, 4) == "Exif")
                    meta = ParseTiff(data.AsSpan(6).ToArray());
            }
            else if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                r.ReadByte(); // precisión
                sofHeight = (r.ReadByte() << 8) | r.ReadByte();
                sofWidth = (r.ReadByte() << 8) | r.ReadByte();
            }
            fs.Position = next;
        }

        return meta with { Width = meta.Width ?? sofWidth, Height = meta.Height ?? sofHeight };
    }

    /// <summary>Lee el bloque TIFF de EXIF (IFD0 y la sub-IFD Exif).</summary>
    internal static MediaMetadata ParseTiff(byte[] tiff)
    {
        bool little = tiff[0] == 'I';
        int U16(int o) => little ? tiff[o] | (tiff[o + 1] << 8) : (tiff[o] << 8) | tiff[o + 1];
        int U32(int o) => little
            ? tiff[o] | (tiff[o + 1] << 8) | (tiff[o + 2] << 16) | (tiff[o + 3] << 24)
            : (tiff[o] << 24) | (tiff[o + 1] << 16) | (tiff[o + 2] << 8) | tiff[o + 3];

        var values = new Dictionary<int, object>();
        void ReadIfd(int offset, bool followExif)
        {
            if (offset <= 0 || offset + 2 > tiff.Length) return;
            int count = U16(offset);
            for (int i = 0; i < count; i++)
            {
                int e = offset + 2 + i * 12;
                if (e + 12 > tiff.Length) return;
                int tag = U16(e), type = U16(e + 2), n = U32(e + 4);
                if (type == 2) // texto ASCII
                {
                    int start = n <= 4 ? e + 8 : U32(e + 8);
                    if (start >= 0 && start + n <= tiff.Length)
                        values[tag] = Encoding.ASCII.GetString(tiff, start, n).TrimEnd('\0', ' ');
                }
                else if (type == 3) values[tag] = U16(e + 8);
                else if (type == 4) values[tag] = U32(e + 8);
            }
            if (followExif && values.TryGetValue(0x8769, out var exif) && exif is int exifOffset)
                ReadIfd(exifOffset, false);
        }
        ReadIfd(U32(4), true);

        string? Text(int tag) => values.TryGetValue(tag, out var v) && v is string s && s.Length > 0 ? s : null;
        int? Number(int tag) => values.TryGetValue(tag, out var v) && v is int i && i > 0 ? i : null;

        return new MediaMetadata
        {
            DateTaken = ParseExifDate(Text(0x9003) ?? Text(0x9004) ?? Text(0x0132)),
            CameraMake = Text(0x010F),
            CameraModel = Text(0x0110),
            Width = Number(0xA002),
            Height = Number(0xA003),
        };
    }

    private static DateTime? ParseExifDate(string? text)
        => text is not null && DateTime.TryParseExact(text.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;

    // =====================================================================
    // PNG
    // =====================================================================

    private static MediaMetadata ReadPng(string path)
    {
        using var fs = File.OpenRead(path);
        var header = new byte[24];
        if (fs.Read(header, 0, 24) < 24 || header[1] != 'P' || header[2] != 'N' || header[3] != 'G') return MediaMetadata.Empty;
        return new MediaMetadata { Width = BigEndian(header, 16), Height = BigEndian(header, 20) };
    }

    private static int BigEndian(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    // =====================================================================
    // MP3 (ID3v2.2/2.3/2.4 y ID3v1)
    // =====================================================================

    private static MediaMetadata ReadMp3(string path)
    {
        using var fs = File.OpenRead(path);
        var tags = new Dictionary<string, string>();
        var header = new byte[10];
        if (fs.Read(header, 0, 10) == 10 && header[0] == 'I' && header[1] == 'D' && header[2] == '3')
        {
            int version = header[3];
            int size = (header[6] << 21) | (header[7] << 14) | (header[8] << 7) | header[9];
            var data = new byte[size];
            int read = fs.ReadAtLeast(data, size, throwOnEndOfStream: false);
            ParseId3v2(data.AsSpan(0, read).ToArray(), version, (header[5] & 0x40) != 0, tags);
        }

        if (fs.Length >= 128)
        {
            var v1 = new byte[128];
            fs.Position = fs.Length - 128;
            if (fs.Read(v1, 0, 128) == 128 && v1[0] == 'T' && v1[1] == 'A' && v1[2] == 'G')
            {
                string Field(int o, int n) => Encoding.Latin1.GetString(v1, o, n).TrimEnd('\0', ' ');
                tags.TryAdd("TITLE", Field(3, 30));
                tags.TryAdd("ARTIST", Field(33, 30));
                tags.TryAdd("ALBUM", Field(63, 30));
                tags.TryAdd("YEAR", Field(93, 4));
                if (v1[125] == 0 && v1[126] != 0) tags.TryAdd("TRACK", v1[126].ToString(CultureInfo.InvariantCulture));
            }
        }

        return FromTags(tags);
    }

    internal static void ParseId3v2(byte[] data, int version, bool extendedHeader, Dictionary<string, string> tags)
    {
        int pos = 0;
        if (extendedHeader && version >= 3 && data.Length >= 4)
        {
            int ext = version == 4
                ? (data[0] << 21) | (data[1] << 14) | (data[2] << 7) | data[3]
                : BigEndian(data, 0) + 4;
            pos = Math.Max(0, ext);
        }

        int idLength = version == 2 ? 3 : 4;
        int headerLength = version == 2 ? 6 : 10;
        while (pos + headerLength <= data.Length)
        {
            string id = Encoding.ASCII.GetString(data, pos, idLength);
            if (id[0] == '\0') break;
            int size = version switch
            {
                2 => (data[pos + 3] << 16) | (data[pos + 4] << 8) | data[pos + 5],
                4 => (data[pos + 4] << 21) | (data[pos + 5] << 14) | (data[pos + 6] << 7) | data[pos + 7],
                _ => BigEndian(data, pos + 4),
            };
            int start = pos + headerLength;
            if (size <= 0 || start + size > data.Length) break;

            string? key = id switch
            {
                "TIT2" or "TT2" => "TITLE",
                "TPE1" or "TP1" => "ARTIST",
                "TALB" or "TAL" => "ALBUM",
                "TRCK" or "TRK" => "TRACK",
                "TYER" or "TYE" or "TDRC" => "YEAR",
                "TCON" or "TCO" => "GENRE",
                _ => null,
            };
            if (key is not null) tags.TryAdd(key, DecodeId3Text(data.AsSpan(start, size)));
            pos = start + size;
        }
    }

    private static string DecodeId3Text(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 1) return "";
        var body = frame[1..];
        string text = frame[0] switch
        {
            1 => Encoding.Unicode.GetString(StripBom(body, out var bigEndian)) is var s && bigEndian
                ? Encoding.BigEndianUnicode.GetString(body[2..]) : s,
            2 => Encoding.BigEndianUnicode.GetString(body),
            3 => Encoding.UTF8.GetString(body),
            _ => Encoding.Latin1.GetString(body),
        };
        return text.Split('\0')[0].Trim();
    }

    private static ReadOnlySpan<byte> StripBom(ReadOnlySpan<byte> data, out bool bigEndian)
    {
        bigEndian = data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF;
        return data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || bigEndian) ? data[2..] : data;
    }

    // =====================================================================
    // FLAC (STREAMINFO y comentarios Vorbis)
    // =====================================================================

    private static MediaMetadata ReadFlac(string path)
    {
        using var fs = File.OpenRead(path);
        var r = new BinaryReader(fs);
        if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "fLaC") return MediaMetadata.Empty;

        var tags = new Dictionary<string, string>();
        TimeSpan? duration = null;
        bool last = false;
        while (!last && fs.Position < fs.Length)
        {
            byte blockHeader = r.ReadByte();
            last = (blockHeader & 0x80) != 0;
            int type = blockHeader & 0x7F;
            int length = (r.ReadByte() << 16) | (r.ReadByte() << 8) | r.ReadByte();
            var block = r.ReadBytes(length);

            if (type == 0 && block.Length >= 18)
            {
                int sampleRate = (block[10] << 12) | (block[11] << 4) | (block[12] >> 4);
                long samples = ((long)(block[13] & 0x0F) << 32) | ((long)block[14] << 24) | ((long)block[15] << 16) | ((long)block[16] << 8) | block[17];
                if (sampleRate > 0 && samples > 0) duration = TimeSpan.FromSeconds((double)samples / sampleRate);
            }
            else if (type == 4)
            {
                ParseVorbisComments(block, tags);
            }
        }
        return FromTags(tags) with { Duration = duration };
    }

    internal static void ParseVorbisComments(byte[] block, Dictionary<string, string> tags)
    {
        int pos = 0;
        int LittleEndian() { int v = block[pos] | (block[pos + 1] << 8) | (block[pos + 2] << 16) | (block[pos + 3] << 24); pos += 4; return v; }
        int vendor = LittleEndian();
        pos += vendor;
        int count = LittleEndian();
        for (int i = 0; i < count && pos + 4 <= block.Length; i++)
        {
            int len = LittleEndian();
            if (len < 0 || pos + len > block.Length) break;
            string comment = Encoding.UTF8.GetString(block, pos, len);
            pos += len;
            int eq = comment.IndexOf('=');
            if (eq <= 0) continue;
            string key = comment[..eq].ToUpperInvariant() switch
            {
                "TRACKNUMBER" => "TRACK",
                "DATE" => "YEAR",
                var k => k,
            };
            tags.TryAdd(key, comment[(eq + 1)..].Trim());
        }
    }

    // =====================================================================
    // MP4 / M4A / MOV
    // =====================================================================

    private static MediaMetadata ReadMp4(string path)
    {
        using var fs = File.OpenRead(path);
        byte[]? moov = FindTopLevelAtom(fs, "moov");
        return moov is null ? MediaMetadata.Empty : ParseMoov(moov);
    }

    private static byte[]? FindTopLevelAtom(Stream fs, string wanted)
    {
        var header = new byte[16];
        while (fs.Position + 8 <= fs.Length)
        {
            long start = fs.Position;
            if (fs.Read(header, 0, 8) < 8) return null;
            long size = (uint)BigEndian(header, 0);
            string type = Encoding.ASCII.GetString(header, 4, 4);
            int headerSize = 8;
            if (size == 1)
            {
                if (fs.Read(header, 8, 8) < 8) return null;
                size = ((long)(uint)BigEndian(header, 8) << 32) | (uint)BigEndian(header, 12);
                headerSize = 16;
            }
            else if (size == 0) size = fs.Length - start;
            if (size < headerSize) return null;

            if (type == wanted)
            {
                long contentSize = size - headerSize;
                if (contentSize > 64 * 1024 * 1024) return null;
                var content = new byte[contentSize];
                return fs.ReadAtLeast(content, (int)contentSize, throwOnEndOfStream: false) == contentSize ? content : null;
            }
            fs.Position = start + size;
        }
        return null;
    }

    /// <summary>Recorre los átomos hijos de un bloque: (tipo, inicio del contenido, longitud).</summary>
    private static IEnumerable<(string Type, int Start, int Length)> Children(byte[] data, int start, int end)
    {
        int pos = start;
        while (pos + 8 <= end)
        {
            int size = BigEndian(data, pos);
            string type = Encoding.Latin1.GetString(data, pos + 4, 4);
            if (size < 8 || pos + size > end) yield break;
            yield return (type, pos + 8, size - 8);
            pos += size;
        }
    }

    internal static MediaMetadata ParseMoov(byte[] moov)
    {
        DateTime? created = null;
        TimeSpan? duration = null;
        int? width = null, height = null;
        var tags = new Dictionary<string, string>();
        var epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var (type, start, length) in Children(moov, 0, moov.Length))
        {
            if (type == "mvhd" && length >= 20)
            {
                int version = moov[start];
                long creation, timescale, dur;
                if (version == 1 && length >= 32)
                {
                    creation = ((long)(uint)BigEndian(moov, start + 4) << 32) | (uint)BigEndian(moov, start + 8);
                    timescale = (uint)BigEndian(moov, start + 20);
                    dur = ((long)(uint)BigEndian(moov, start + 24) << 32) | (uint)BigEndian(moov, start + 28);
                }
                else
                {
                    creation = (uint)BigEndian(moov, start + 4);
                    timescale = (uint)BigEndian(moov, start + 12);
                    dur = (uint)BigEndian(moov, start + 16);
                }
                if (creation > 0) created = epoch.AddSeconds(creation).ToLocalTime();
                if (timescale > 0 && dur > 0) duration = TimeSpan.FromSeconds((double)dur / timescale);
            }
            else if (type == "trak")
            {
                foreach (var (t, s, l) in Children(moov, start, start + length))
                {
                    if (t != "tkhd" || l < 84) continue;
                    int offset = moov[s] == 1 ? 88 : 76;
                    if (s + offset + 8 > moov.Length) continue;
                    int w = (int)((uint)BigEndian(moov, s + offset) >> 16);
                    int h = (int)((uint)BigEndian(moov, s + offset + 4) >> 16);
                    if (w > 0 && h > 0 && w * h > (width ?? 0) * (height ?? 0)) { width = w; height = h; }
                }
            }
            else if (type == "udta")
            {
                foreach (var (t, s, l) in Children(moov, start, start + length))
                {
                    if (t != "meta") continue;
                    // "meta" es una caja completa: 4 bytes de versión/indicadores antes de los hijos.
                    foreach (var (t2, s2, l2) in Children(moov, s + 4, s + l))
                    {
                        if (t2 != "ilst") continue;
                        foreach (var (item, s3, l3) in Children(moov, s2, s2 + l2))
                            ReadIlstItem(moov, item, s3, l3, tags);
                    }
                }
            }
        }

        return FromTags(tags) with { DateTaken = created, Duration = duration, Width = width, Height = height };
    }

    private static void ReadIlstItem(byte[] moov, string item, int start, int length, Dictionary<string, string> tags)
    {
        foreach (var (t, s, l) in Children(moov, start, start + length))
        {
            if (t != "data" || l < 8) continue;
            var payload = moov.AsSpan(s + 8, l - 8);
            switch (item)
            {
                case "©nam": tags.TryAdd("TITLE", Encoding.UTF8.GetString(payload)); break;
                case "©ART": tags.TryAdd("ARTIST", Encoding.UTF8.GetString(payload)); break;
                case "aART": tags.TryAdd("ARTIST", Encoding.UTF8.GetString(payload)); break;
                case "©alb": tags.TryAdd("ALBUM", Encoding.UTF8.GetString(payload)); break;
                case "©day": tags.TryAdd("YEAR", Encoding.UTF8.GetString(payload)); break;
                case "©gen": tags.TryAdd("GENRE", Encoding.UTF8.GetString(payload)); break;
                case "trkn" when payload.Length >= 4:
                    tags.TryAdd("TRACK", ((payload[2] << 8) | payload[3]).ToString(CultureInfo.InvariantCulture));
                    break;
            }
        }
    }

    // =====================================================================

    private static MediaMetadata FromTags(Dictionary<string, string> tags)
    {
        string? Get(string key) => tags.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        return new MediaMetadata
        {
            Title = Get("TITLE"),
            Artist = Get("ARTIST"),
            Album = Get("ALBUM"),
            Genre = CleanGenre(Get("GENRE")),
            Track = LeadingNumber(Get("TRACK")),
            Year = Year(Get("YEAR")),
        };
    }

    private static string? CleanGenre(string? genre)
    {
        if (genre is null) return null;
        // ID3 antiguo guarda "(17)Rock" o solo "(17)".
        string cleaned = System.Text.RegularExpressions.Regex.Replace(genre, @"^\(\d+\)", "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static int? LeadingNumber(string? text)
    {
        if (text is null) return null;
        int end = 0;
        while (end < text.Length && char.IsAsciiDigit(text[end])) end++;
        return end > 0 && int.TryParse(text[..end], NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : null;
    }

    private static int? Year(string? text)
    {
        var n = LeadingNumber(text);
        return n is >= 1000 and <= 9999 ? n : null;
    }
}
