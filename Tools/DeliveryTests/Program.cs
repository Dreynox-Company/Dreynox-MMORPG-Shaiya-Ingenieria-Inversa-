using System.IO.Compression;
using System.Text;
using Dreynox.Delivery;
int count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAIL " + label);
    count++; Console.WriteLine("PASS " + label);
}
void Reject(Action action, string label)
{
    bool failed = false;
    try { action(); } catch (InvalidDataException) { failed = true; }
    Check(failed, label);
}
foreach (string name in new[] { "../outside", "/root", "C:/outside", "a\\b", "a//b", "a/./b", "CON.txt", "com1", "a/aux", "a:stream", "file. ", "NUL.txt", "bad\nname" })
    Reject(() => VerifiedPlayerArchive.SafeRelativeName(name), "reject unsafe path " + name.Replace('\n', ' '));
Check(VerifiedPlayerArchive.SafeRelativeName("DreynoxMmorpg-Map1_Data/Managed/Game.dll").EndsWith("Game.dll"), "normal Unity path accepted");
string root = Path.Combine(Path.GetTempPath(), "DreynoxDeliveryTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    byte[] valid = Archive(false, false, false, null);
    string destination = Path.Combine(root, "valid"); Directory.CreateDirectory(destination);
    int progress = 0;
    using (var stream = new MemoryStream(valid)) VerifiedPlayerArchive.ExtractAndVerify(stream, destination, p => progress = p);
    Check(progress == 100 && File.Exists(Path.Combine(destination, VerifiedPlayerArchive.GameExecutable)), "complete payload extracted and checked");
    using (var stream = new MemoryStream(valid)) VerifiedPlayerArchive.VerifyInstalled(stream, destination);
    Check(true, "cache verified against embedded original manifest");
    string unexpected = Path.Combine(destination, "untrusted-plugin.dll");
    File.WriteAllText(unexpected, "not in the embedded manifest");
    Reject(() => { using (var stream = new MemoryStream(valid)) VerifiedPlayerArchive.VerifyInstalled(stream, destination); }, "unlisted installed plugin rejected");
    File.Delete(unexpected);
    File.WriteAllText(Path.Combine(destination, "UnityPlayer.dll"), "changed");
    Reject(() => { using (var stream = new MemoryStream(valid)) VerifiedPlayerArchive.VerifyInstalled(stream, destination); }, "modified cached payload rejected");
    File.WriteAllText(Path.Combine(destination, VerifiedPlayerArchive.ManifestName), "attacker manifest");
    Reject(() => { using (var stream = new MemoryStream(valid)) VerifiedPlayerArchive.VerifyInstalled(stream, destination); }, "cache manifest cannot redefine expected binary hashes");
    foreach (var test in new[] {
        ("corrupt", Archive(true,false,false,null)), ("duplicate", Archive(false,true,false,null)),
        ("unsigned-extra", Archive(false,false,false,"extra.dll")), ("missing-unity", Archive(false,false,true,null)),
        ("path-escape", Archive(false,false,false,"../outside.txt")) })
    {
        string folder = Path.Combine(root, test.Item1); Directory.CreateDirectory(folder);
        Reject(() => { using (var stream = new MemoryStream(test.Item2)) VerifiedPlayerArchive.ExtractAndVerify(stream, folder, null); }, test.Item1 + " rejected");
    }
    Check(!File.Exists(Path.Combine(root, "outside.txt")), "rejected path never writes outside staging");
}
finally { Directory.Delete(root, true); }
count += DeliveryMetadataCases.Run();
Console.WriteLine("VERIFIED PLAYER DELIVERY OK: " + count + " checks (synthetic archive fixtures, not game execution)");
static byte[] Archive(bool corrupt, bool duplicate, bool missingUnity, string extra)
{
    var files = new Dictionary<string, byte[]> {
        {VerifiedPlayerArchive.GameExecutable, Encoding.UTF8.GetBytes("TEST-FIXTURE-NOT-AN-EXECUTABLE")},
        {"UnityPlayer.dll", Encoding.UTF8.GetBytes("TEST-FIXTURE-NOT-A-DLL")},
        {"DreynoxMmorpg-Map1_Data/globalgamemanagers", new byte[] {1,2,3,4}} };
    if (missingUnity) files.Remove("UnityPlayer.dll");
    var manifest = new StringBuilder();
    foreach (var pair in files)
    { using var data = new MemoryStream(pair.Value); manifest.Append(VerifiedPlayerArchive.Sha256(data)).Append("  ").Append(pair.Key).Append('\n'); }
    using var output = new MemoryStream();
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
    {
        foreach (var pair in files)
        {
            using var entry = zip.CreateEntry(pair.Key).Open();
            byte[] value = corrupt && pair.Key == "UnityPlayer.dll" ? new byte[] {4,5,6} : pair.Value;
            entry.Write(value);
        }
        if (duplicate) { using var entry = zip.CreateEntry("unityplayer.DLL").Open(); entry.WriteByte(4); }
        if (extra != null) { using var entry = zip.CreateEntry(extra).Open(); entry.WriteByte(1); }
        using var writer = new StreamWriter(zip.CreateEntry(VerifiedPlayerArchive.ManifestName).Open(), new UTF8Encoding(false));
        writer.Write(manifest.ToString());
    }
    return output.ToArray();
}
