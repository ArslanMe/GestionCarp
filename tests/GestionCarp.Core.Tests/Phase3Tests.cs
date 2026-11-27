using System.Text;
using GestionCarp.Core.Compare;
using GestionCarp.Core.Duplicates;
using GestionCarp.Core.Metadata;
using GestionCarp.Core.Rename;
using GestionCarp.Core.Rename.Rules;

namespace GestionCarp.Core.Tests;

public class CompareTests
{
    [Fact]
    public void ComparesTwoFolders()
    {
        using var t = new TempFolder();
        t.File("A/igual.txt", "hola");
        t.File("B/igual.txt", "hola");
        t.File("A/distinto.txt", "uno");
        t.File("B/distinto.txt", "dos!");
        t.File("A/mismo-tamano.txt", "abc");
        t.File("B/mismo-tamano.txt", "xyz");
        t.File("A/solo-a.txt");
        t.File("B/Sub/solo-b.txt");
        t.File("A/Sub/comun.txt", "x");
        t.File("B/Sub/comun.txt", "x");
        t.File("A/raro", "archivo");
        t.Dir("B/raro");

        var quick = FolderComparer.Compare(t.Combine("A"), t.Combine("B"), new CompareOptions());
        var byPath = quick.ToDictionary(i => i.RelativePath);
        Assert.Equal(CompareStatus.Equal, byPath["igual.txt"].Status);
        Assert.Equal(CompareStatus.Different, byPath["distinto.txt"].Status);
        Assert.Equal(CompareStatus.Equal, byPath["mismo-tamano.txt"].Status); // solo nombre y tamaño
        Assert.Equal(CompareStatus.OnlyInLeft, byPath["solo-a.txt"].Status);
        Assert.Equal(CompareStatus.OnlyInRight, byPath["Sub/solo-b.txt"].Status);
        Assert.Equal(CompareStatus.Different, byPath["Sub"].Status); // contiene diferencias
        Assert.Equal(CompareStatus.TypeMismatch, byPath["raro"].Status);
        Assert.True(quick.FindIndex(i => i.RelativePath == "Sub") < quick.FindIndex(i => i.RelativePath == "Sub/comun.txt"));

        var exact = FolderComparer.Compare(t.Combine("A"), t.Combine("B"), new CompareOptions { Mode = CompareMode.Content });
        Assert.Equal(CompareStatus.Different, exact.Single(i => i.RelativePath == "mismo-tamano.txt").Status);
    }

    [Fact]
    public void CopiesMissingWithoutOverwritingAndUndoes()
    {
        using var t = new TempFolder();
        t.File("A/falta.txt", "contenido");
        t.File("A/Nueva/dentro.txt", "x");
        t.File("A/existe.txt", "versión A");
        t.File("B/existe.txt", "versión B");

        var result = FolderCopier.CopyMissing(new[]
        {
            (t.Combine("A/falta.txt"), t.Combine("B/falta.txt")),
            (t.Combine("A/Nueva"), t.Combine("B/Nueva")),
            (t.Combine("A/existe.txt"), t.Combine("B/existe.txt")),
        }, "copiar");
        Assert.True(result.Success, result.Error);
        Assert.Equal("contenido", File.ReadAllText(t.Combine("B/falta.txt")));
        Assert.Equal("x", File.ReadAllText(t.Combine("B/Nueva/dentro.txt")));
        Assert.Equal("versión B", File.ReadAllText(t.Combine("B/existe.txt"))); // nunca se sobrescribe

        var history = new UndoHistory();
        history.Push(result.Batch!);
        var undo = history.UndoLast();
        Assert.True(undo.Success);
        Assert.Equal(new[] { "existe.txt" }, t.Names("B"));
        Assert.True(File.Exists(t.Combine("A/falta.txt")));
    }
}

public class DuplicateTests
{
    [Fact]
    public void FindsDuplicatesByContent()
    {
        using var t = new TempFolder();
        string big = new string('a', 100_000);
        t.File("uno/foto.jpg", big);
        t.File("dos/copia de foto.jpg", big);
        t.File("dos/otra.jpg", new string('a', 99_999) + "b"); // mismo tamaño y comienzo, distinto final
        t.File("tres/nota.txt", "igual");
        t.File("tres/nota (1).txt", "igual");
        t.File("tres/vacio1.txt", "");
        t.File("tres/vacio2.txt", "");
        t.File("tres/unico.txt", "único");

        var groups = DuplicateFinder.Find(new[] { t.Path }, new DuplicateOptions());
        Assert.Equal(2, groups.Count);
        Assert.Equal(100_000, groups[0].Size); // ordenados por espacio desperdiciado
        Assert.Equal(new[] { "copia de foto.jpg", "foto.jpg" }, groups[0].Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n));
        Assert.Equal(2, groups[1].Files.Count);

        var onlyJpg = DuplicateFinder.Find(new[] { t.Path }, new DuplicateOptions { FilePatterns = { "*.jpg" } });
        Assert.Single(onlyJpg);
    }
}

public class MetadataTests
{
    private static byte[] Atom(string type, params byte[][] content)
    {
        var body = content.SelectMany(c => c).ToArray();
        var result = new byte[8 + body.Length];
        BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(result.Length)).CopyTo(result, 0);
        Encoding.Latin1.GetBytes(type).CopyTo(result, 4);
        body.CopyTo(result, 8);
        return result;
    }

    private static byte[] BigEndian(uint value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    public static byte[] Jpeg()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        void Entry(ushort tag, ushort type, uint count, uint value) { w.Write(tag); w.Write(type); w.Write(count); w.Write(value); }
        w.Write("II"u8); w.Write((ushort)42); w.Write(8u);
        w.Write((ushort)3);
        Entry(0x010F, 2, 6, 92); Entry(0x0110, 2, 14, 98); Entry(0x8769, 4, 1, 50); w.Write(0u);
        w.Write((ushort)3);
        Entry(0x9003, 2, 20, 112); Entry(0xA002, 4, 1, 4000); Entry(0xA003, 3, 1, 3000); w.Write(0u);
        w.Write("Canon\0"u8); w.Write("Canon EOS 80D\0"u8); w.Write("2023:07:15 18:30:05\0"u8);
        byte[] tiff = ms.ToArray();

        int length = 2 + 6 + tiff.Length;
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length }
            .Concat("Exif\0\0"u8.ToArray()).Concat(tiff).Concat(new byte[] { 0xFF, 0xD9 }).ToArray();
    }

    private static byte[] Id3Frame(string id, byte encoding, byte[] text, bool syncsafe)
    {
        int size = text.Length + 1;
        byte[] sizeBytes = syncsafe
            ? new[] { (byte)((size >> 21) & 0x7F), (byte)((size >> 14) & 0x7F), (byte)((size >> 7) & 0x7F), (byte)(size & 0x7F) }
            : BigEndian((uint)size);
        return Encoding.ASCII.GetBytes(id).Concat(sizeBytes).Concat(new byte[] { 0, 0, encoding }).Concat(text).ToArray();
    }

    private static byte[] Id3(byte version, params byte[][] frames)
    {
        var body = frames.SelectMany(f => f).Concat(new byte[32]).ToArray();
        int size = body.Length;
        var header = new byte[] { (byte)'I', (byte)'D', (byte)'3', version, 0, 0,
            (byte)((size >> 21) & 0x7F), (byte)((size >> 14) & 0x7F), (byte)((size >> 7) & 0x7F), (byte)(size & 0x7F) };
        return header.Concat(body).Concat(new byte[500]).ToArray();
    }

    [Fact]
    public void ReadsJpegExif()
    {
        using var t = new TempFolder();
        string path = t.Combine("IMG_0001.jpg");
        File.WriteAllBytes(path, Jpeg());
        var meta = MetadataReader.Read(path);
        Assert.Equal(new DateTime(2023, 7, 15, 18, 30, 5), meta.DateTaken);
        Assert.Equal("Canon EOS 80D", meta.Camera);
        Assert.Equal((4000, 3000), (meta.Width, meta.Height));
    }

    [Fact]
    public void ReadsMp3Id3v23AndV24()
    {
        using var t = new TempFolder();
        string v23 = t.Combine("a.mp3");
        File.WriteAllBytes(v23, Id3(3,
            Id3Frame("TIT2", 0, Encoding.Latin1.GetBytes("Hola Mundo"), false),
            Id3Frame("TPE1", 1, Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Artista Ñ")).ToArray(), false),
            Id3Frame("TRCK", 0, "3/12"u8.ToArray(), false),
            Id3Frame("TYER", 0, "2021"u8.ToArray(), false),
            Id3Frame("TCON", 0, "(17)Rock"u8.ToArray(), false)));
        var meta = MetadataReader.Read(v23);
        Assert.Equal(("Hola Mundo", "Artista Ñ", 3, 2021, "Rock"), (meta.Title, meta.Artist, meta.Track, meta.Year, meta.Genre));

        string v24 = t.Combine("b.mp3");
        File.WriteAllBytes(v24, Id3(4,
            Id3Frame("TIT2", 3, Encoding.UTF8.GetBytes("Canción"), true),
            Id3Frame("TDRC", 3, "2019-05-01"u8.ToArray(), true)));
        meta = MetadataReader.Read(v24);
        Assert.Equal(("Canción", 2019), (meta.Title, meta.Year));
    }

    [Fact]
    public void ReadsFlac()
    {
        var streamInfo = new byte[34];
        streamInfo[10] = 0x0A; streamInfo[11] = 0xC4; streamInfo[12] = 0x42; streamInfo[13] = 0xF0;
        BigEndian(44100u * 65).CopyTo(streamInfo, 14);
        var comments = new MemoryStream();
        var w = new BinaryWriter(comments);
        w.Write(4); w.Write("test"u8);
        var entries = new[] { "TITLE=Tema", "ARTIST=Grupo", "TRACKNUMBER=7" };
        w.Write(entries.Length);
        foreach (var e in entries) { var b = Encoding.UTF8.GetBytes(e); w.Write(b.Length); w.Write(b); }
        var vorbis = comments.ToArray();

        var file = "fLaC"u8.ToArray()
            .Concat(new byte[] { 0x00, 0, 0, 34 }).Concat(streamInfo)
            .Concat(new byte[] { 0x84, 0, (byte)(vorbis.Length >> 8), (byte)vorbis.Length }).Concat(vorbis).ToArray();
        using var t = new TempFolder();
        string path = t.Combine("x.flac");
        File.WriteAllBytes(path, file);

        var meta = MetadataReader.Read(path);
        Assert.Equal(("Tema", "Grupo", 7), (meta.Title, meta.Artist, meta.Track));
        Assert.Equal(TimeSpan.FromSeconds(65), meta.Duration);
    }

    [Fact]
    public void ReadsMp4()
    {
        var epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var created = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var mvhd = new byte[100];
        BigEndian((uint)(created - epoch).TotalSeconds).CopyTo(mvhd, 4);
        BigEndian(1000).CopyTo(mvhd, 12);
        BigEndian(125_500).CopyTo(mvhd, 16);
        var tkhd = new byte[84];
        BigEndian(1920u << 16).CopyTo(tkhd, 76);
        BigEndian(1080u << 16).CopyTo(tkhd, 80);
        byte[] Data(byte[] payload) => Atom("data", new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }, payload);

        var moov = Atom("moov",
            Atom("mvhd", mvhd),
            Atom("trak", Atom("tkhd", tkhd)),
            Atom("udta", Atom("meta", new byte[4], Atom("ilst",
                Atom("©nam", Data(Encoding.UTF8.GetBytes("Mi vídeo"))),
                Atom("©ART", Data("Autor"u8.ToArray())),
                Atom("trkn", Data(new byte[] { 0, 0, 0, 5, 0, 10, 0, 0 }))))));
        var file = Atom("ftyp", "isom"u8.ToArray()).Concat(Atom("mdat", new byte[64])).Concat(moov).ToArray();

        using var t = new TempFolder();
        string path = t.Combine("v.mp4");
        File.WriteAllBytes(path, file);
        var meta = MetadataReader.Read(path);
        Assert.Equal(("Mi vídeo", "Autor", 5), (meta.Title, meta.Artist, meta.Track));
        Assert.Equal((1920, 1080), (meta.Width, meta.Height));
        Assert.Equal(TimeSpan.FromSeconds(125.5), meta.Duration);
        Assert.Equal(created.ToLocalTime(), meta.DateTaken);
    }

    [Fact]
    public void UnknownOrBrokenFilesGiveEmptyMetadata()
    {
        using var t = new TempFolder();
        Assert.Same(MediaMetadata.Empty, MetadataReader.Read(t.File("roto.jpg", "no es un jpeg")) is { DateTaken: null } ? MediaMetadata.Empty : null);
        Assert.Null(MetadataReader.Read(t.File("x.mp3", "ID3")).Title);
        Assert.Null(MetadataReader.Read(t.Combine("no-existe.flac")).Title);
    }

    [Fact]
    public void MetadataRuleRenamesWithTemplate()
    {
        using var t = new TempFolder();
        string photo = t.Combine("IMG_0001.jpg");
        File.WriteAllBytes(photo, Jpeg());
        RenameContext Ctx(string p) => new(0, 0, 1, p, false);

        var rule = new MetadataRule { Template = "{fecha:yyyy-MM-dd HH.mm.ss} {camara} ({resolucion})" };
        Assert.Equal("2023-07-15 18.30.05 Canon EOS 80D (4000x3000).jpg", rule.Apply("IMG_0001.jpg", Ctx(photo)));

        var music = new MetadataRule { Template = "{pista:00} - {artista} - {titulo}" };
        var error = Assert.Throws<RenameRuleException>(() => music.Apply("IMG_0001.jpg", Ctx(photo)));
        Assert.Contains("pista", error.Message);

        music.SkipMissing = true;
        Assert.Equal("IMG_0001.jpg", music.Apply("IMG_0001.jpg", Ctx(photo))); // sin datos: se conserva el nombre

        string song = t.Combine("track.mp3");
        File.WriteAllBytes(song, Id3(3, Id3Frame("TIT2", 0, Encoding.Latin1.GetBytes("Mi/Canción: en vivo"), false),
            Id3Frame("TPE1", 0, "Grupo"u8.ToArray(), false), Id3Frame("TRCK", 0, "4"u8.ToArray(), false)));
        music.SkipMissing = false;
        Assert.Equal("04 - Grupo - Mi-Canción- en vivo.mp3", music.Apply("track.mp3", Ctx(song)));
    }
}
